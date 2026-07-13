#include "pch.h"
#include "FfmpegMediaSource.h"
#include "HttpMediaInput.h"
#include "../NativePlaybackDiagnostics.h"

#include <algorithm>
#include <cctype>
#include <chrono>
#include <cstdlib>
#include <string>
#include <string_view>

#pragma warning(push)
#pragma warning(disable : 4244 4819)
extern "C"
{
#include <libavcodec/codec_id.h>
#include <libavcodec/packet.h>
#include <libavformat/avformat.h>
#include <libavutil/channel_layout.h>
#include <libavutil/dict.h>
#include <libavutil/avutil.h>
#include <libavutil/error.h>
}
#pragma warning(pop)

namespace
{
    using SteadyClock = std::chrono::steady_clock;
    constexpr int64_t OpenTimeoutMilliseconds = 30'000;
    constexpr int64_t ReadTimeoutMilliseconds = 20'000;
    constexpr uint64_t MaxSwitchPacketCacheBytesPerStream = 8ULL * 1024ULL * 1024ULL;
    constexpr size_t MaxSwitchPacketCachePacketsPerStream = 4096;
    constexpr int64_t MaxSwitchPacketCacheDurationTicks = 30LL * 10'000'000LL;
    constexpr int64_t SwitchPacketCoverageToleranceTicks = 250LL * 10'000LL;
    constexpr AVRational HundredNanosecondTimeBase{1, 10'000'000};

    int64_t SteadyClockNanoseconds() noexcept
    {
        return std::chrono::duration_cast<std::chrono::nanoseconds>(
            SteadyClock::now().time_since_epoch()).count();
    }

    int64_t ElapsedMilliseconds(SteadyClock::time_point startedAt) noexcept
    {
        return std::chrono::duration_cast<std::chrono::milliseconds>(
            SteadyClock::now() - startedAt).count();
    }

    std::string GetFfmpegErrorMessage(int errorCode)
    {
        char buffer[AV_ERROR_MAX_STRING_SIZE]{};
        if (av_strerror(errorCode, buffer, sizeof(buffer)) < 0)
        {
            return "Unknown FFmpeg error " + std::to_string(errorCode);
        }

        return buffer;
    }

    winrt::hresult_error CreateFfmpegError(char const* operation, int errorCode)
    {
        auto message = std::string(operation) + " failed: " + GetFfmpegErrorMessage(errorCode);
        return winrt::hresult_error(E_FAIL, winrt::to_hstring(message));
    }

    bool IsValidRational(AVRational value)
    {
        return value.num > 0 && value.den > 0;
    }

    double ToFrameRate(AVRational value)
    {
        return IsValidRational(value)
            ? static_cast<double>(value.num) / static_cast<double>(value.den)
            : 0.0;
    }

    double SelectFrameRate(AVStream const* stream)
    {
        if (stream == nullptr)
        {
            return 0.0;
        }

        auto average = ToFrameRate(stream->avg_frame_rate);
        if (average > 0.0)
        {
            return average;
        }

        return ToFrameRate(stream->r_frame_rate);
    }

    uint64_t ReadAvioTransportBytes(AVFormatContext const* formatContext) noexcept
    {
        if (formatContext == nullptr || formatContext->pb == nullptr || formatContext->pb->bytes_read <= 0)
        {
            return 0;
        }

        return static_cast<uint64_t>(formatContext->pb->bytes_read);
    }

    uint64_t TransportByteDelta(
        uint64_t before,
        uint64_t after,
        wchar_t const* phase) noexcept
    {
        if (after >= before)
        {
            return after - before;
        }

        winrt::NoiraPlayer::Native::implementation::AppendNativePlaybackDiagnostic(
            std::wstring(L"FfmpegMediaSource transport byte counter regressed phase=") +
            phase +
            L" before=" + std::to_wstring(before) +
            L" after=" + std::to_wstring(after));
        return 0;
    }

    int64_t RescaleToTicks(int64_t value, AVRational timeBase) noexcept
    {
        return value == AV_NOPTS_VALUE || !IsValidRational(timeBase)
            ? 0
            : av_rescale_q(value, timeBase, HundredNanosecondTimeBase);
    }

    int64_t SelectLogicalDurationTicks(AVFormatContext const* formatContext) noexcept
    {
        if (formatContext == nullptr)
        {
            return 0;
        }

        int64_t durationTicks = 0;
        for (auto streamIndex = uint32_t{0}; streamIndex < formatContext->nb_streams; ++streamIndex)
        {
            auto stream = formatContext->streams[streamIndex];
            auto codecParameters = stream == nullptr ? nullptr : stream->codecpar;
            if (stream == nullptr || codecParameters == nullptr || stream->duration <= 0 ||
                (codecParameters->codec_type != AVMEDIA_TYPE_AUDIO &&
                    codecParameters->codec_type != AVMEDIA_TYPE_VIDEO))
            {
                continue;
            }

            durationTicks = (std::max)(
                durationTicks,
                RescaleToTicks(stream->duration, stream->time_base));
        }

        if (durationTicks == 0 && formatContext->duration > 0)
        {
            durationTicks = RescaleToTicks(formatContext->duration, AV_TIME_BASE_Q);
        }

        return durationTicks;
    }

    std::string MapHdrKind(AVColorTransferCharacteristic transfer)
    {
        switch (transfer)
        {
        case AVCOL_TRC_SMPTE2084:
            return "Hdr10";
        case AVCOL_TRC_ARIB_STD_B67:
            return "Hlg";
        default:
            return "Sdr";
        }
    }

    std::string MapVideoRange(
        AVColorTransferCharacteristic transfer,
        AVColorRange range)
    {
        switch (transfer)
        {
        case AVCOL_TRC_SMPTE2084:
            return "HDR10";
        case AVCOL_TRC_ARIB_STD_B67:
            return "HLG";
        default:
            return range == AVCOL_RANGE_JPEG ? "PC" : "SDR";
        }
    }

    std::string MapColorPrimaries(AVColorPrimaries primaries)
    {
        switch (primaries)
        {
        case AVCOL_PRI_BT709:
            return "bt709";
        case AVCOL_PRI_BT2020:
            return "bt2020";
        case AVCOL_PRI_SMPTE170M:
            return "smpte170m";
        case AVCOL_PRI_SMPTE240M:
            return "smpte240m";
        default:
            return "";
        }
    }

    std::string MapColorTransfer(AVColorTransferCharacteristic transfer)
    {
        switch (transfer)
        {
        case AVCOL_TRC_BT709:
            return "bt709";
        case AVCOL_TRC_SMPTE2084:
            return "smpte2084";
        case AVCOL_TRC_ARIB_STD_B67:
            return "arib-std-b67";
        case AVCOL_TRC_SMPTE170M:
            return "smpte170m";
        case AVCOL_TRC_IEC61966_2_1:
            return "iec61966-2-1";
        default:
            return "";
        }
    }

    std::string MapColorSpace(AVColorSpace colorSpace)
    {
        switch (colorSpace)
        {
        case AVCOL_SPC_BT709:
            return "bt709";
        case AVCOL_SPC_BT2020_NCL:
            return "bt2020nc";
        case AVCOL_SPC_BT2020_CL:
            return "bt2020c";
        case AVCOL_SPC_SMPTE170M:
            return "smpte170m";
        case AVCOL_SPC_SMPTE240M:
            return "smpte240m";
        default:
            return "";
        }
    }

    std::string GetCodecName(AVCodecID codecId)
    {
        return codecId == AV_CODEC_ID_NONE ? "" : avcodec_get_name(codecId);
    }

    std::string MapStreamKind(AVMediaType mediaType)
    {
        switch (mediaType)
        {
        case AVMEDIA_TYPE_VIDEO:
            return "Video";
        case AVMEDIA_TYPE_AUDIO:
            return "Audio";
        case AVMEDIA_TYPE_SUBTITLE:
            return "Subtitle";
        default:
            return "";
        }
    }

    std::string GetMetadataValue(AVDictionary* metadata, char const* key)
    {
        auto entry = av_dict_get(metadata, key, nullptr, 0);
        return entry != nullptr && entry->value != nullptr
            ? entry->value
            : "";
    }

    int32_t GetAudioChannelCount(AVCodecParameters const* codecpar)
    {
        if (codecpar == nullptr || codecpar->codec_type != AVMEDIA_TYPE_AUDIO)
        {
            return 0;
        }

        return codecpar->ch_layout.nb_channels > 0
            ? codecpar->ch_layout.nb_channels
            : 0;
    }

    std::string GetAudioChannelLayout(AVCodecParameters const* codecpar)
    {
        if (codecpar == nullptr ||
            codecpar->codec_type != AVMEDIA_TYPE_AUDIO ||
            codecpar->ch_layout.nb_channels <= 0)
        {
            return "";
        }

        char buffer[128]{};
        return av_channel_layout_describe(&codecpar->ch_layout, buffer, sizeof(buffer)) >= 0
            ? buffer
            : "";
    }

    int HexValue(char value)
    {
        if (value >= '0' && value <= '9')
        {
            return value - '0';
        }

        if (value >= 'a' && value <= 'f')
        {
            return value - 'a' + 10;
        }

        if (value >= 'A' && value <= 'F')
        {
            return value - 'A' + 10;
        }

        return -1;
    }

    std::string ConvertFileUriToLocalPath(std::string const& source)
    {
        auto path = source;
        if (path.rfind("file:///", 0) == 0)
        {
            path = path.substr(8);
        }
        else if (path.rfind("file://", 0) == 0)
        {
            path = path.substr(7);
        }
        else
        {
            return source;
        }

        std::string decoded;
        decoded.reserve(path.size());
        for (auto index = size_t{0}; index < path.size(); ++index)
        {
            if (path[index] == '%' && index + 2 < path.size())
            {
                auto high = HexValue(path[index + 1]);
                auto low = HexValue(path[index + 2]);
                if (high >= 0 && low >= 0)
                {
                    decoded.push_back(static_cast<char>((high << 4) + low));
                    index += 2;
                    continue;
                }
            }

            decoded.push_back(path[index] == '/' ? '\\' : path[index]);
        }

        return decoded;
    }

    bool IsHttpSource(std::string const& source)
    {
        if (source.size() < 7)
        {
            return false;
        }

        auto hasPrefix = [&source](char const* prefix)
        {
            auto prefixLength = std::char_traits<char>::length(prefix);
            return source.size() >= prefixLength &&
                std::equal(
                    prefix,
                    prefix + prefixLength,
                    source.begin(),
                    [](char expected, char actual)
                    {
                        return std::tolower(static_cast<unsigned char>(expected)) ==
                            std::tolower(static_cast<unsigned char>(actual));
                    });
        };

        return hasPrefix("http://") || hasPrefix("https://");
    }

    bool IsInstrumentedAvioEnabled() noexcept
    {
        char* value = nullptr;
        size_t valueLength = 0;
        if (_dupenv_s(&value, &valueLength, "NOIRAPLAYER_NATIVE_INSTRUMENTED_AVIO") != 0)
        {
            return false;
        }

        auto enabled = value != nullptr && std::string_view(value) == "1";
        std::free(value);
        return enabled;
    }

    bool IsDemuxReadRecoveryEnabled() noexcept
    {
        char* value = nullptr;
        size_t valueLength = 0;
        if (_dupenv_s(&value, &valueLength, "NOIRAPLAYER_QA_DISABLE_DEMUX_READ_RECOVERY") != 0)
        {
            return true;
        }

        auto disabled = value != nullptr && std::string_view(value) == "1";
        std::free(value);
        return !disabled;
    }

    void SetFfmpegOption(AVDictionary** options, char const* name, char const* value)
    {
        auto result = av_dict_set(options, name, value, 0);
        if (result < 0)
        {
            throw CreateFfmpegError("av_dict_set", result);
        }
    }
}

namespace winrt::NoiraPlayer::Native::implementation
{
    FfmpegTransportCallSnapshot SubtractTransportCallSnapshots(
        FfmpegTransportCallSnapshot const& before,
        FfmpegTransportCallSnapshot const& after) noexcept
    {
        FfmpegTransportCallSnapshot result;
        result.Provider = after.Provider;
        result.EvidenceAvailable = before.EvidenceAvailable && after.EvidenceAvailable &&
            before.Provider == after.Provider;
        if (!result.EvidenceAvailable)
        {
            return result;
        }

        result.ReadCalls = after.ReadCalls >= before.ReadCalls ? after.ReadCalls - before.ReadCalls : 0;
        result.SeekCalls = after.SeekCalls >= before.SeekCalls ? after.SeekCalls - before.SeekCalls : 0;
        result.ReadWaitMs = after.ReadWaitMs >= before.ReadWaitMs ? after.ReadWaitMs - before.ReadWaitMs : 0.0;
        result.SeekWaitMs = after.SeekWaitMs >= before.SeekWaitMs ? after.SeekWaitMs - before.SeekWaitMs : 0.0;
        result.SeekDistanceBytes = after.SeekDistanceBytes >= before.SeekDistanceBytes
            ? after.SeekDistanceBytes - before.SeekDistanceBytes
            : 0;
        return result;
    }

    void FfmpegMediaSource::Open(winrt::hstring const& url)
    {
        HttpMediaInput::ValidateUrl(url);

        Close();
        m_openTiming = {};
        m_readTiming = {};
        m_readRecovery = {};
        m_readRecoveryStartedNanoseconds = 0;
        m_isHttpSource = false;
        m_readRecoveryEnabled = IsDemuxReadRecoveryEnabled();
        m_interruptRequested.store(false, std::memory_order_release);

        auto networkResult = avformat_network_init();
        if (networkResult < 0)
        {
            throw CreateFfmpegError("avformat_network_init", networkResult);
        }

        AVFormatContext* formatContext = avformat_alloc_context();
        if (formatContext == nullptr)
        {
            throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not allocate FFmpeg format context.");
        }

        formatContext->interrupt_callback.callback = &FfmpegMediaSource::InterruptCallback;
        formatContext->interrupt_callback.opaque = this;
        AVDictionary* openOptions = nullptr;
        try
        {
            auto source = ConvertFileUriToLocalPath(winrt::to_string(url));
            m_isHttpSource = IsHttpSource(source);
            if (m_isHttpSource)
            {
                SetFfmpegOption(&openOptions, "reconnect", "1");
                SetFfmpegOption(&openOptions, "reconnect_on_network_error", "1");
                SetFfmpegOption(&openOptions, "reconnect_max_retries", "3");
                SetFfmpegOption(&openOptions, "reconnect_delay_max", "2");
                SetFfmpegOption(&openOptions, "reconnect_delay_total_max", "6");
                AppendNativePlaybackDiagnostic(
                    L"FfmpegMediaSource.Open HTTP reconnect policy retries=3 delayMaxSeconds=2 delayTotalMaxSeconds=6");
            }

            auto openInputStartedAt = SteadyClock::now();
            AppendNativePlaybackDiagnostic(
                L"FfmpegMediaSource.Open avformat_open_input begin sourceLength=" +
                std::to_wstring(source.size()));
            BeginBlockingIo(OpenTimeoutMilliseconds);
            auto result = 0;
            if (IsHttpSource(source) && IsInstrumentedAvioEnabled())
            {
                m_httpMediaInput = std::make_unique<HttpMediaInput>();
                m_httpMediaInput->Open(source, &openOptions, formatContext->interrupt_callback);
                m_httpMediaInput->Attach(formatContext);
                result = avformat_open_input(&formatContext, source.c_str(), nullptr, &openOptions);
            }
            else
            {
                result = avformat_open_input(&formatContext, source.c_str(), nullptr, &openOptions);
            }
            av_dict_free(&openOptions);
            if (result < 0)
            {
                AppendNativePlaybackDiagnostic(
                    L"FfmpegMediaSource.Open avformat_open_input failed result=" +
                    std::to_wstring(result) +
                    L" durationMs=" +
                    std::to_wstring(ElapsedMilliseconds(openInputStartedAt)));
                throw CreateFfmpegError("avformat_open_input", result);
            }

            m_openTiming.OpenInputDurationMs = static_cast<double>(ElapsedMilliseconds(openInputStartedAt));
            m_openTiming.OpenInputBytesRead = ReadAvioTransportBytes(formatContext);
            m_openTiming.OpenInputTransportCalls = TransportCallSnapshot();
            AppendNativePlaybackDiagnostic(
                L"FfmpegMediaSource.Open avformat_open_input end result=0 durationMs=" +
                std::to_wstring(m_openTiming.OpenInputDurationMs));

            auto const transportBytesBeforeStreamInfo = ReadAvioTransportBytes(formatContext);
            auto const transportCallsBeforeStreamInfo = TransportCallSnapshot();
            auto streamInfoStartedAt = SteadyClock::now();
            AppendNativePlaybackDiagnostic(
                L"FfmpegMediaSource.Open avformat_find_stream_info begin streamCountBefore=" +
                std::to_wstring(formatContext == nullptr ? 0 : formatContext->nb_streams));
            BeginBlockingIo(OpenTimeoutMilliseconds);
            result = avformat_find_stream_info(formatContext, nullptr);
            if (result < 0)
            {
                AppendNativePlaybackDiagnostic(
                    L"FfmpegMediaSource.Open avformat_find_stream_info failed result=" +
                    std::to_wstring(result) +
                    L" durationMs=" +
                    std::to_wstring(ElapsedMilliseconds(streamInfoStartedAt)) +
                    L" streamCountAfter=" +
                    std::to_wstring(formatContext == nullptr ? 0 : formatContext->nb_streams));
                throw CreateFfmpegError("avformat_find_stream_info", result);
            }

            m_openTiming.StreamInfoDurationMs = static_cast<double>(ElapsedMilliseconds(streamInfoStartedAt));
            m_openTiming.StreamInfoBytesRead = TransportByteDelta(
                transportBytesBeforeStreamInfo,
                ReadAvioTransportBytes(formatContext),
                L"find-stream-info");
            m_openTiming.StreamInfoTransportCalls = SubtractTransportCallSnapshots(
                transportCallsBeforeStreamInfo,
                TransportCallSnapshot());
            AppendNativePlaybackDiagnostic(
                L"FfmpegMediaSource.Open avformat_find_stream_info end result=0 durationMs=" +
                std::to_wstring(m_openTiming.StreamInfoDurationMs) +
                L" streamCountAfter=" +
                std::to_wstring(formatContext == nullptr ? 0 : formatContext->nb_streams));

            auto formatName = formatContext->iformat != nullptr && formatContext->iformat->name != nullptr
                ? winrt::to_hstring(formatContext->iformat->name)
                : winrt::hstring{};
            AppendNativePlaybackDiagnostic(
                L"FfmpegMediaSource.Open streamCount=" +
                std::to_wstring(formatContext->nb_streams) +
                    L" format=" +
                    std::wstring(formatName));

            auto containerStartTimeTicks = RescaleToTicks(
                formatContext->start_time,
                AV_TIME_BASE_Q);
            auto logicalDurationTicks = SelectLogicalDurationTicks(formatContext);
            m_timeline.Reset(containerStartTimeTicks, logicalDurationTicks);
            m_lastSeekDemuxTargetTicks = -1;
            AppendNativePlaybackDiagnostic(
                L"FfmpegMediaSource.Timeline originTicks=" +
                std::to_wstring(m_timeline.OriginTicks()) +
                L" durationTicks=" +
                std::to_wstring(m_timeline.DurationTicks()));
            for (auto streamIndex = uint32_t{0}; streamIndex < formatContext->nb_streams; ++streamIndex)
            {
                auto stream = formatContext->streams[streamIndex];
                auto codecpar = stream == nullptr ? nullptr : stream->codecpar;
                if (codecpar == nullptr)
                {
                    continue;
                }

                AppendNativePlaybackDiagnostic(
                    L"FfmpegMediaSource.Stream index=" + std::to_wstring(streamIndex) +
                    L" type=" + std::to_wstring(static_cast<int>(codecpar->codec_type)) +
                    L" codec=" + std::to_wstring(static_cast<int>(codecpar->codec_id)) +
                    L" width=" + std::to_wstring(codecpar->width) +
                    L" height=" + std::to_wstring(codecpar->height) +
                    L" format=" + std::to_wstring(codecpar->format) +
                    L" bitrate=" + std::to_wstring(codecpar->bit_rate));
            }

            m_formatContext = formatContext;
            formatContext = nullptr;
        }
        catch (...)
        {
            av_dict_free(&openOptions);
            if (formatContext != nullptr)
            {
                avformat_close_input(&formatContext);
            }

            Close();
            throw;
        }

        m_url = url;
        m_avformatVersion = static_cast<uint32_t>(avformat_version());
        m_open = true;
    }

    FfmpegOpenTimingSnapshot FfmpegMediaSource::OpenTimingSnapshot() const noexcept
    {
        return m_openTiming;
    }

    FfmpegReadTimingSnapshot FfmpegMediaSource::ReadTimingSnapshot() const noexcept
    {
        return m_readTiming;
    }

    uint64_t FfmpegMediaSource::TransportBytesRead() const noexcept
    {
        if (m_formatContext == nullptr || m_formatContext->pb == nullptr || m_formatContext->pb->bytes_read <= 0)
        {
            return 0;
        }

        return static_cast<uint64_t>(m_formatContext->pb->bytes_read);
    }

    FfmpegTransportCallSnapshot FfmpegMediaSource::TransportCallSnapshot() const noexcept
    {
        FfmpegTransportCallSnapshot result;
        if (m_httpMediaInput == nullptr)
        {
            return result;
        }

        auto snapshot = m_httpMediaInput->Snapshot();
        result.Provider = snapshot.Provider;
        result.EvidenceAvailable = snapshot.EvidenceAvailable;
        result.ReadCalls = snapshot.ReadCalls;
        result.SeekCalls = snapshot.SeekCalls;
        result.ReadWaitMs = snapshot.ReadWaitMs;
        result.SeekWaitMs = snapshot.SeekWaitMs;
        result.SeekDistanceBytes = snapshot.SeekDistanceBytes;
        return result;
    }

    void FfmpegMediaSource::Close() noexcept
    {
        Interrupt();
        ClearPacketQueues();
        m_activeStreams.clear();
        m_switchCacheStreams.clear();
        m_seekReplayCacheEnabled = false;
        m_seekReplayVideoStreamIndex = -1;
        m_seekReplayCache.Clear();

        if (m_formatContext != nullptr)
        {
            avformat_close_input(&m_formatContext);
        }

        if (m_httpMediaInput != nullptr)
        {
            m_httpMediaInput->Close();
            m_httpMediaInput.reset();
        }

        m_url.clear();
        m_avformatVersion = 0;
        m_ioDeadlineNanoseconds.store(0, std::memory_order_release);
        m_timeline.Reset();
        m_lastSeekDemuxTargetTicks = -1;
        m_readTiming = {};
        m_readRecovery = {};
        m_readRecoveryStartedNanoseconds = 0;
        m_isHttpSource = false;
        m_readRecoveryEnabled = true;
        m_open = false;
    }

    void FfmpegMediaSource::Interrupt() noexcept
    {
        m_interruptRequested.store(true, std::memory_order_release);
    }

    int FfmpegMediaSource::InterruptCallback(void* opaque) noexcept
    {
        auto source = static_cast<FfmpegMediaSource*>(opaque);
        if (source == nullptr)
        {
            return 0;
        }

        if (source->m_interruptRequested.load(std::memory_order_acquire))
        {
            return 1;
        }

        auto deadline = source->m_ioDeadlineNanoseconds.load(std::memory_order_acquire);
        return deadline > 0 && SteadyClockNanoseconds() >= deadline ? 1 : 0;
    }

    void FfmpegMediaSource::BeginBlockingIo(int64_t timeoutMilliseconds) noexcept
    {
        auto timeout = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::milliseconds((std::max<int64_t>)(1, timeoutMilliseconds))).count();
        m_ioDeadlineNanoseconds.store(
            SteadyClockNanoseconds() + timeout,
            std::memory_order_release);
    }

    std::optional<int32_t> FfmpegMediaSource::TryFindStream(
        int mediaType,
        int32_t selectedStreamIndex) const
    {
        if (!m_open || m_formatContext == nullptr)
        {
            return std::nullopt;
        }

        if (selectedStreamIndex >= 0 &&
            static_cast<uint32_t>(selectedStreamIndex) < m_formatContext->nb_streams)
        {
            auto stream = m_formatContext->streams[selectedStreamIndex];
            if (stream != nullptr && stream->codecpar != nullptr &&
                stream->codecpar->codec_type == static_cast<AVMediaType>(mediaType))
            {
                return selectedStreamIndex;
            }
        }

        auto bestStream = av_find_best_stream(
            m_formatContext,
            static_cast<AVMediaType>(mediaType),
            -1,
            -1,
            nullptr,
            0);
        if (bestStream < 0)
        {
            return std::nullopt;
        }

        return bestStream;
    }

    int32_t FfmpegMediaSource::FindRequiredStream(int mediaType, int32_t selectedStreamIndex) const
    {
        auto streamIndex = TryFindStream(mediaType, selectedStreamIndex);
        if (!streamIndex)
        {
            throw winrt::hresult_error(E_FAIL, L"Required FFmpeg media stream is not available.");
        }

        return streamIndex.value();
    }

    AVStream* FfmpegMediaSource::Stream(int32_t streamIndex) const
    {
        if (!m_open || m_formatContext == nullptr ||
            streamIndex < 0 ||
            static_cast<uint32_t>(streamIndex) >= m_formatContext->nb_streams)
        {
            throw winrt::hresult_invalid_argument(L"FFmpeg stream index is not available.");
        }

        auto stream = m_formatContext->streams[streamIndex];
        if (stream == nullptr || stream->codecpar == nullptr)
        {
            throw winrt::hresult_error(E_FAIL, L"FFmpeg stream metadata is not available.");
        }

        return stream;
    }

    std::optional<FfmpegVideoStreamSnapshot> FfmpegMediaSource::BestVideoStreamSnapshot() const
    {
        auto streamIndex = TryFindStream(AVMEDIA_TYPE_VIDEO, -1);
        if (!streamIndex)
        {
            return std::nullopt;
        }

        auto stream = Stream(streamIndex.value());
        auto codecpar = stream->codecpar;
        FfmpegVideoStreamSnapshot snapshot{};
        snapshot.StreamIndex = streamIndex.value();
        snapshot.Codec = GetCodecName(codecpar->codec_id);
        snapshot.Width = codecpar->width > 0 ? static_cast<uint32_t>(codecpar->width) : 0;
        snapshot.Height = codecpar->height > 0 ? static_cast<uint32_t>(codecpar->height) : 0;
        snapshot.FrameRate = SelectFrameRate(stream);
        snapshot.HdrKind = MapHdrKind(codecpar->color_trc);
        snapshot.VideoRange = MapVideoRange(codecpar->color_trc, codecpar->color_range);
        snapshot.ColorPrimaries = MapColorPrimaries(codecpar->color_primaries);
        snapshot.ColorTransfer = MapColorTransfer(codecpar->color_trc);
        snapshot.ColorSpace = MapColorSpace(codecpar->color_space);
        return snapshot;
    }

    std::vector<FfmpegStreamSnapshot> FfmpegMediaSource::StreamSnapshots() const
    {
        std::vector<FfmpegStreamSnapshot> snapshots;
        if (!m_open || m_formatContext == nullptr)
        {
            return snapshots;
        }

        for (auto streamIndex = uint32_t{0}; streamIndex < m_formatContext->nb_streams; ++streamIndex)
        {
            auto stream = m_formatContext->streams[streamIndex];
            auto codecpar = stream == nullptr ? nullptr : stream->codecpar;
            if (stream == nullptr || codecpar == nullptr)
            {
                continue;
            }

            auto kind = MapStreamKind(codecpar->codec_type);
            if (kind.empty())
            {
                continue;
            }

            FfmpegStreamSnapshot snapshot{};
            snapshot.StreamIndex = static_cast<int32_t>(streamIndex);
            snapshot.Kind = kind;
            snapshot.Codec = GetCodecName(codecpar->codec_id);
            snapshot.Language = GetMetadataValue(stream->metadata, "language");
            snapshot.ChannelLayout = GetAudioChannelLayout(codecpar);
            snapshot.Channels = GetAudioChannelCount(codecpar);
            snapshot.IsDefault = (stream->disposition & AV_DISPOSITION_DEFAULT) != 0;
            snapshot.IsForced = (stream->disposition & AV_DISPOSITION_FORCED) != 0;
            if (codecpar->codec_type == AVMEDIA_TYPE_VIDEO)
            {
                snapshot.RealFrameRate = ToFrameRate(stream->r_frame_rate);
                snapshot.AverageFrameRate = ToFrameRate(stream->avg_frame_rate);
            }

            snapshots.push_back(std::move(snapshot));
        }

        return snapshots;
    }

    void FfmpegMediaSource::RegisterStream(int32_t streamIndex)
    {
        if (streamIndex < 0)
        {
            throw winrt::hresult_invalid_argument(L"FFmpeg stream index cannot be negative.");
        }

        m_activeStreams.insert(streamIndex);
        RefreshSeekReplayCacheConfiguration();
    }

    void FfmpegMediaSource::UnregisterStream(int32_t streamIndex) noexcept
    {
        m_activeStreams.erase(streamIndex);
        try
        {
            RefreshSeekReplayCacheConfiguration();
        }
        catch (...)
        {
            m_seekReplayCacheEnabled = false;
            m_seekReplayCache.Clear();
        }

        if (m_switchCacheStreams.find(streamIndex) != m_switchCacheStreams.end())
        {
            return;
        }

        auto packetQueue = m_packetQueues.find(streamIndex);
        if (packetQueue == m_packetQueues.end())
        {
            return;
        }

        for (auto packet : packetQueue->second)
        {
            av_packet_free(&packet);
        }

        m_packetQueues.erase(packetQueue);
        m_packetQueueBytes.erase(streamIndex);
    }

    void FfmpegMediaSource::ConfigureSwitchPacketCache(
        std::vector<int32_t> const& streamIndexes)
    {
        m_switchCacheStreams.clear();
        for (auto streamIndex : streamIndexes)
        {
            if (Stream(streamIndex) != nullptr)
            {
                m_switchCacheStreams.insert(streamIndex);
            }
        }
    }

    void FfmpegMediaSource::ConfigureSeekReplayCache(bool enabled, int32_t videoStreamIndex)
    {
        m_seekReplayCacheEnabled = enabled;
        m_seekReplayVideoStreamIndex = videoStreamIndex;
        RefreshSeekReplayCacheConfiguration();
    }

    FfmpegSeekReplayAttemptSnapshot FfmpegMediaSource::TryPrepareSeekReplay(
        int64_t targetPositionTicks,
        int64_t currentPositionTicks)
    {
        auto replay = m_seekReplayCache.TryBuildReplay(
            targetPositionTicks,
            currentPositionTicks);
        FfmpegSeekReplayAttemptSnapshot snapshot
        {
            replay.Enabled,
            replay.Hit,
            replay.PacketCount,
            replay.Bytes,
            replay.WindowDurationTicks,
            replay.FallbackReason
        };
        if (!replay.Hit)
        {
            return snapshot;
        }

        for (auto packet = replay.Packets.rbegin(); packet != replay.Packets.rend(); ++packet)
        {
            auto& queue = m_packetQueues[packet->StreamIndex];
            auto& bytes = m_packetQueueBytes[packet->StreamIndex];
            auto rawPacket = packet->Packet.get();
            queue.push_front(rawPacket);
            packet->Packet.release();
            if (rawPacket != nullptr && rawPacket->size > 0)
            {
                bytes += static_cast<uint64_t>(rawPacket->size);
            }
        }
        return snapshot;
    }

    FfmpegSwitchPacketCacheSnapshot FfmpegMediaSource::SwitchPacketCacheSnapshot(
        int32_t streamIndex,
        int64_t positionTicks,
        bool requirePacketAtOrAfter) const
    {
        FfmpegSwitchPacketCacheSnapshot snapshot;
        auto queue = m_packetQueues.find(streamIndex);
        if (queue == m_packetQueues.end() || queue->second.empty())
        {
            return snapshot;
        }

        snapshot.PacketCount = static_cast<uint64_t>(queue->second.size());
        auto byteCount = m_packetQueueBytes.find(streamIndex);
        snapshot.Bytes = byteCount == m_packetQueueBytes.end() ? 0 : byteCount->second;

        std::optional<int64_t> earliest;
        std::optional<int64_t> latest;
        for (auto packet : queue->second)
        {
            auto packetPosition = PacketPositionTicks(packet);
            if (!packetPosition.has_value())
            {
                continue;
            }

            earliest = !earliest.has_value()
                ? packetPosition
                : std::optional<int64_t>{(std::min)(earliest.value(), packetPosition.value())};
            latest = !latest.has_value()
                ? packetPosition
                : std::optional<int64_t>{(std::max)(latest.value(), packetPosition.value())};
        }

        if (!earliest.has_value() || earliest.value() > positionTicks + SwitchPacketCoverageToleranceTicks)
        {
            return snapshot;
        }

        snapshot.WindowDurationTicks = latest.has_value()
            ? (std::max<int64_t>)(0, latest.value() - earliest.value())
            : 0;
        snapshot.HasCoverage = !requirePacketAtOrAfter ||
            (latest.has_value() && latest.value() + SwitchPacketCoverageToleranceTicks >= positionTicks);
        return snapshot;
    }

    bool FfmpegMediaSource::TryReadPacket(int32_t streamIndex, AVPacket* packet)
    {
        if (!m_open || m_formatContext == nullptr || packet == nullptr)
        {
            return false;
        }

        if (TryTakeQueuedPacket(streamIndex, packet))
        {
            return true;
        }

        auto scratchPacket = av_packet_alloc();
        if (scratchPacket == nullptr)
        {
            throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not allocate FFmpeg packet.");
        }

        try
        {
            int readResult = 0;
            while (true)
            {
                BeginBlockingIo(ReadTimeoutMilliseconds);
                auto const readStartedAt = SteadyClock::now();
                readResult = av_read_frame(m_formatContext, scratchPacket);
                m_readTiming.ReadFrameDurationMs +=
                    std::chrono::duration<double, std::milli>(
                        SteadyClock::now() - readStartedAt).count();
                if (readResult < 0)
                {
                    auto const recoveryBefore = m_readRecovery.Snapshot();
                    if (recoveryBefore.ConsecutiveReadErrors == 0)
                    {
                        m_readRecoveryStartedNanoseconds = SteadyClockNanoseconds();
                    }

                    auto const disposition = m_readRecovery.ObserveError(
                        readResult,
                        readResult == AVERROR_EOF,
                        readResult == AVERROR(EAGAIN) || readResult == AVERROR(EINTR),
                        m_isHttpSource,
                        m_interruptRequested.load(std::memory_order_acquire),
                        m_readRecoveryEnabled);
                    m_readTiming.Recovery = m_readRecovery.Snapshot();
                    if (disposition == FfmpegReadDisposition::Retry)
                    {
                        AppendNativePlaybackDiagnostic(
                            L"FfmpegMediaSource.Read retry error=" +
                            std::to_wstring(readResult) +
                            L" consecutive=" +
                            std::to_wstring(m_readTiming.Recovery.ConsecutiveReadErrors) +
                            L" maxRetries=" +
                            std::to_wstring(FfmpegReadRecoveryState::MaxConsecutiveRetries));
                        av_packet_unref(scratchPacket);
                        continue;
                    }

                    if (disposition == FfmpegReadDisposition::EndOfStream ||
                        disposition == FfmpegReadDisposition::Interrupted)
                    {
                        av_packet_free(&scratchPacket);
                        return false;
                    }

                    break;
                }

                auto const recoveryBeforePacket = m_readRecovery.Snapshot();
                if (recoveryBeforePacket.ConsecutiveReadErrors > 0)
                {
                    auto const recoveryDurationMs = static_cast<double>((std::max<int64_t>)(
                        0,
                        SteadyClockNanoseconds() - m_readRecoveryStartedNanoseconds)) / 1'000'000.0;
                    m_readRecovery.RecordPacketRecovered(recoveryDurationMs);
                    m_readTiming.Recovery = m_readRecovery.Snapshot();
                    m_readRecoveryStartedNanoseconds = 0;
                    AppendNativePlaybackDiagnostic(
                        L"FfmpegMediaSource.Read recovered durationMs=" +
                        std::to_wstring(recoveryDurationMs) +
                        L" recoveryCount=" +
                        std::to_wstring(m_readTiming.Recovery.ReadRecoveryCount));
                }

                ++m_readTiming.PacketCount;
                if (scratchPacket->size > 0)
                {
                    m_readTiming.Bytes += static_cast<uint64_t>(scratchPacket->size);
                }

                if (auto packetPosition = PacketPositionTicks(scratchPacket))
                {
                    m_seekReplayCache.ObservePacket(scratchPacket, packetPosition.value());
                }

                if (scratchPacket->stream_index == streamIndex)
                {
                    av_packet_move_ref(packet, scratchPacket);
                    av_packet_free(&scratchPacket);
                    return true;
                }

                if (ShouldQueueStream(scratchPacket->stream_index))
                {
                    QueuePacket(scratchPacket);
                }

                av_packet_unref(scratchPacket);
            }

            av_packet_free(&scratchPacket);

            throw CreateFfmpegError("av_read_frame", readResult);
        }
        catch (...)
        {
            av_packet_free(&scratchPacket);
            throw;
        }
    }

    bool FfmpegMediaSource::TryReadQueuedPacket(int32_t streamIndex, AVPacket* packet)
    {
        if (!m_open || m_formatContext == nullptr || packet == nullptr)
        {
            return false;
        }

        return TryTakeQueuedPacket(streamIndex, packet);
    }

    FfmpegTimelineSnapshot FfmpegMediaSource::TimelineSnapshot(int32_t streamIndex) const
    {
        auto stream = Stream(streamIndex);
        return FfmpegTimelineSnapshot
        {
            m_timeline.OriginTicks(),
            stream == nullptr ? 0 : RescaleToTicks(stream->start_time, stream->time_base),
            m_timeline.DurationTicks(),
            m_lastSeekDemuxTargetTicks
        };
    }

    int64_t FfmpegMediaSource::NormalizeTimestampTicks(int64_t demuxTicks) const noexcept
    {
        return m_timeline.ToLogicalTicks(demuxTicks);
    }

    void FfmpegMediaSource::Seek(int32_t streamIndex, int64_t positionTicks)
    {
        if (!m_open || m_formatContext == nullptr)
        {
            return;
        }

        auto stream = Stream(streamIndex);
        if (stream == nullptr)
        {
            throw winrt::hresult_invalid_argument(L"Seek stream index is invalid.");
        }

        m_lastSeekDemuxTargetTicks = m_timeline.ToDemuxTicks(positionTicks);
        auto timestamp = av_rescale_q(
            m_lastSeekDemuxTargetTicks,
            HundredNanosecondTimeBase,
            stream->time_base);
        BeginBlockingIo(ReadTimeoutMilliseconds);
        auto result = av_seek_frame(m_formatContext, streamIndex, timestamp, AVSEEK_FLAG_BACKWARD);
        if (result < 0)
        {
            throw CreateFfmpegError("av_seek_frame", result);
        }

        ClearPacketQueues();
        m_seekReplayCache.Clear();
    }

    void FfmpegMediaSource::ClearPacketQueues() noexcept
    {
        for (auto& packetQueue : m_packetQueues)
        {
            for (auto packet : packetQueue.second)
            {
                av_packet_free(&packet);
            }
        }

        m_packetQueues.clear();
        m_packetQueueBytes.clear();
    }

    bool FfmpegMediaSource::TryTakeQueuedPacket(int32_t streamIndex, AVPacket* packet)
    {
        auto packetQueue = m_packetQueues.find(streamIndex);
        if (packetQueue == m_packetQueues.end() || packetQueue->second.empty())
        {
            return false;
        }

        auto queuedPacket = packetQueue->second.front();
        packetQueue->second.pop_front();
        auto queuedBytes = queuedPacket->size > 0 ? static_cast<uint64_t>(queuedPacket->size) : 0;
        auto byteCount = m_packetQueueBytes.find(streamIndex);
        if (byteCount != m_packetQueueBytes.end())
        {
            byteCount->second = byteCount->second > queuedBytes
                ? byteCount->second - queuedBytes
                : 0;
        }
        av_packet_move_ref(packet, queuedPacket);
        av_packet_free(&queuedPacket);
        return true;
    }

    bool FfmpegMediaSource::ShouldQueueStream(int32_t streamIndex) const
    {
        return m_activeStreams.find(streamIndex) != m_activeStreams.end() ||
            m_switchCacheStreams.find(streamIndex) != m_switchCacheStreams.end();
    }

    void FfmpegMediaSource::QueuePacket(AVPacket* packet)
    {
        auto queuedPacket = av_packet_alloc();
        if (queuedPacket == nullptr)
        {
            throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not allocate FFmpeg queued packet.");
        }

        av_packet_move_ref(queuedPacket, packet);
        m_packetQueues[queuedPacket->stream_index].push_back(queuedPacket);
        if (queuedPacket->size > 0)
        {
            m_packetQueueBytes[queuedPacket->stream_index] +=
                static_cast<uint64_t>(queuedPacket->size);
        }
        TrimSwitchPacketCache(queuedPacket->stream_index);
    }

    void FfmpegMediaSource::TrimSwitchPacketCache(int32_t streamIndex) noexcept
    {
        if (m_switchCacheStreams.find(streamIndex) == m_switchCacheStreams.end() ||
            m_activeStreams.find(streamIndex) != m_activeStreams.end())
        {
            return;
        }

        auto queue = m_packetQueues.find(streamIndex);
        if (queue == m_packetQueues.end())
        {
            return;
        }

        auto& packets = queue->second;
        auto& bytes = m_packetQueueBytes[streamIndex];
        while (!packets.empty())
        {
            auto frontPosition = PacketPositionTicks(packets.front());
            auto backPosition = PacketPositionTicks(packets.back());
            auto durationExceeded = frontPosition.has_value() && backPosition.has_value() &&
                backPosition.value() - frontPosition.value() > MaxSwitchPacketCacheDurationTicks;
            if (packets.size() <= MaxSwitchPacketCachePacketsPerStream &&
                bytes <= MaxSwitchPacketCacheBytesPerStream &&
                !durationExceeded)
            {
                break;
            }

            auto packet = packets.front();
            packets.pop_front();
            auto packetBytes = packet->size > 0 ? static_cast<uint64_t>(packet->size) : 0;
            bytes = bytes > packetBytes ? bytes - packetBytes : 0;
            av_packet_free(&packet);
        }
    }

    void FfmpegMediaSource::RefreshSeekReplayCacheConfiguration()
    {
        auto activeStreams = std::vector<int32_t>(
            m_activeStreams.begin(),
            m_activeStreams.end());
        m_seekReplayCache.Configure(
            m_seekReplayCacheEnabled,
            activeStreams,
            m_seekReplayVideoStreamIndex);
    }

    std::optional<int64_t> FfmpegMediaSource::PacketPositionTicks(
        AVPacket const* packet) const noexcept
    {
        if (packet == nullptr)
        {
            return std::nullopt;
        }

        auto stream = Stream(packet->stream_index);
        if (stream == nullptr)
        {
            return std::nullopt;
        }

        auto timestamp = packet->pts != AV_NOPTS_VALUE ? packet->pts : packet->dts;
        if (timestamp == AV_NOPTS_VALUE)
        {
            return std::nullopt;
        }

        return NormalizeTimestampTicks(RescaleToTicks(timestamp, stream->time_base));
    }
}
