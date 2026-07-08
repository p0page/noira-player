#include "pch.h"
#include "FfmpegMediaSource.h"
#include "HttpMediaInput.h"
#include "../NativePlaybackDiagnostics.h"

#include <cctype>
#include <string>

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
}

namespace winrt::NextGenEmby::Native::implementation
{
    void FfmpegMediaSource::Open(winrt::hstring const& url)
    {
        HttpMediaInput input;
        input.Open(url);

        Close();

        auto networkResult = avformat_network_init();
        if (networkResult < 0)
        {
            throw CreateFfmpegError("avformat_network_init", networkResult);
        }

        AVFormatContext* formatContext = nullptr;
        try
        {
            auto source = ConvertFileUriToLocalPath(winrt::to_string(url));
            auto result = avformat_open_input(&formatContext, source.c_str(), nullptr, nullptr);
            if (result < 0)
            {
                throw CreateFfmpegError("avformat_open_input", result);
            }

            result = avformat_find_stream_info(formatContext, nullptr);
            if (result < 0)
            {
                throw CreateFfmpegError("avformat_find_stream_info", result);
            }

            auto formatName = formatContext->iformat != nullptr && formatContext->iformat->name != nullptr
                ? winrt::to_hstring(formatContext->iformat->name)
                : winrt::hstring{};
            AppendNativePlaybackDiagnostic(
                L"FfmpegMediaSource.Open streamCount=" +
                std::to_wstring(formatContext->nb_streams) +
                L" format=" +
                std::wstring(formatName));
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

    void FfmpegMediaSource::Close() noexcept
    {
        ClearPacketQueues();
        m_activeStreams.clear();

        if (m_formatContext != nullptr)
        {
            avformat_close_input(&m_formatContext);
        }

        m_url.clear();
        m_avformatVersion = 0;
        m_open = false;
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
    }

    void FfmpegMediaSource::UnregisterStream(int32_t streamIndex) noexcept
    {
        m_activeStreams.erase(streamIndex);

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
            while ((readResult = av_read_frame(m_formatContext, scratchPacket)) >= 0)
            {
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

            if (readResult == AVERROR_EOF)
            {
                return false;
            }

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

    void FfmpegMediaSource::Seek(int32_t streamIndex, int64_t timestamp)
    {
        if (!m_open || m_formatContext == nullptr)
        {
            return;
        }

        auto result = av_seek_frame(m_formatContext, streamIndex, timestamp, AVSEEK_FLAG_BACKWARD);
        if (result < 0)
        {
            throw CreateFfmpegError("av_seek_frame", result);
        }

        ClearPacketQueues();
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
        av_packet_move_ref(packet, queuedPacket);
        av_packet_free(&queuedPacket);
        return true;
    }

    bool FfmpegMediaSource::ShouldQueueStream(int32_t streamIndex) const
    {
        return m_activeStreams.find(streamIndex) != m_activeStreams.end();
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
    }
}
