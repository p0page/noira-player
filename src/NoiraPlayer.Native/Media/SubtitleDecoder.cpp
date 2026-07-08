#include "pch.h"
#include "SubtitleDecoder.h"

#include <memory>
#include <string>

#pragma warning(push)
#pragma warning(disable : 4244 4819)
extern "C"
{
#include <libavcodec/avcodec.h>
#include <libavcodec/codec.h>
#include <libavformat/avformat.h>
#include <libavutil/avutil.h>
#include <libavutil/error.h>
#include <libavutil/mathematics.h>
}
#pragma warning(pop)

namespace
{
    constexpr AVRational HundredNanosecondTimeBase{1, 10000000};
    constexpr AVRational AvTimeBase{1, AV_TIME_BASE};
    constexpr int64_t TicksPerMillisecond = 10000;
    constexpr int64_t DefaultSubtitleDurationTicks = 50000000;
    constexpr uint32_t MaxSubtitlePacketsPerPass = 8;

    struct AvPacketDeleter
    {
        void operator()(AVPacket* packet) const noexcept
        {
            av_packet_free(&packet);
        }
    };

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

    std::wstring ToWideString(char const* text)
    {
        if (text == nullptr || text[0] == '\0')
        {
            return {};
        }

        auto value = winrt::to_hstring(std::string(text));
        return std::wstring(value.c_str(), value.size());
    }

    std::string StripAssFields(std::string text)
    {
        auto dialoguePrefix = std::string("Dialogue:");
        if (text.rfind(dialoguePrefix, 0) == 0)
        {
            text.erase(0, dialoguePrefix.size());
            if (!text.empty() && text.front() == ' ')
            {
                text.erase(0, 1);
            }
        }

        auto commaCount = 0;
        for (size_t index = 0; index < text.size(); ++index)
        {
            if (text[index] == ',')
            {
                ++commaCount;
                if (commaCount == 9)
                {
                    return text.substr(index + 1);
                }
            }
        }

        return text;
    }

    std::string NormalizeAssText(std::string text)
    {
        text = StripAssFields(std::move(text));

        std::string normalized;
        normalized.reserve(text.size());
        auto inOverrideTag = false;
        for (size_t index = 0; index < text.size(); ++index)
        {
            auto character = text[index];
            if (character == '{')
            {
                inOverrideTag = true;
                continue;
            }

            if (character == '}')
            {
                inOverrideTag = false;
                continue;
            }

            if (inOverrideTag)
            {
                continue;
            }

            if (character == '\\' && index + 1 < text.size())
            {
                auto next = text[index + 1];
                if (next == 'N' || next == 'n')
                {
                    normalized.push_back('\n');
                    ++index;
                    continue;
                }

                if (next == 'h')
                {
                    normalized.push_back(' ');
                    ++index;
                    continue;
                }
            }

            normalized.push_back(character);
        }

        return normalized;
    }

    std::optional<std::wstring> TryCreateSubtitleText(AVSubtitle const& subtitle)
    {
        std::wstring text;
        for (auto index = 0u; index < subtitle.num_rects; ++index)
        {
            auto rect = subtitle.rects[index];
            if (rect == nullptr)
            {
                continue;
            }

            std::wstring rectText;
            if (rect->type == SUBTITLE_TEXT)
            {
                rectText = ToWideString(rect->text);
            }
            else if (rect->type == SUBTITLE_ASS && rect->ass != nullptr)
            {
                rectText = ToWideString(NormalizeAssText(rect->ass).c_str());
            }

            if (rectText.empty())
            {
                continue;
            }

            if (!text.empty())
            {
                text.push_back(L'\n');
            }

            text.append(rectText);
        }

        if (text.empty())
        {
            return std::nullopt;
        }

        return text;
    }

    int64_t GetPacketBaseTicks(AVPacket const* packet, AVSubtitle const& subtitle, AVStream const* stream)
    {
        if (subtitle.pts != AV_NOPTS_VALUE)
        {
            return av_rescale_q(subtitle.pts, AvTimeBase, HundredNanosecondTimeBase);
        }

        auto timestamp = packet->pts;
        if (timestamp == AV_NOPTS_VALUE)
        {
            timestamp = packet->dts;
        }

        if (timestamp == AV_NOPTS_VALUE)
        {
            return 0;
        }

        return av_rescale_q(timestamp, stream->time_base, HundredNanosecondTimeBase);
    }

    winrt::NoiraPlayer::Native::implementation::DecodedSubtitleCue CreateSubtitleCue(
        AVPacket const* packet,
        AVSubtitle const& subtitle,
        AVStream const* stream,
        std::wstring text)
    {
        auto baseTicks = GetPacketBaseTicks(packet, subtitle, stream);
        auto startTicks = baseTicks + static_cast<int64_t>(subtitle.start_display_time) * TicksPerMillisecond;
        auto endTicks = baseTicks + static_cast<int64_t>(subtitle.end_display_time) * TicksPerMillisecond;
        if (endTicks <= startTicks)
        {
            if (packet->duration > 0)
            {
                endTicks = baseTicks + av_rescale_q(packet->duration, stream->time_base, HundredNanosecondTimeBase);
            }

            if (endTicks <= startTicks)
            {
                endTicks = startTicks + DefaultSubtitleDurationTicks;
            }
        }

        return { std::move(text), startTicks, endTicks };
    }
}

namespace winrt::NoiraPlayer::Native::implementation
{
    void SubtitleDecoder::Open(FfmpegMediaSource& mediaSource, int32_t selectedSubtitleStreamIndex)
    {
        if (selectedSubtitleStreamIndex < 0)
        {
            throw winrt::hresult_invalid_argument(L"Subtitle stream index cannot be negative.");
        }

        Close();

        auto subtitleStreamIndex = mediaSource.TryFindStream(
            AVMEDIA_TYPE_SUBTITLE,
            selectedSubtitleStreamIndex);
        if (!subtitleStreamIndex)
        {
            return;
        }

        AVCodecContext* codecContext = nullptr;
        try
        {
            auto subtitleStream = mediaSource.Stream(subtitleStreamIndex.value());
            auto decoder = avcodec_find_decoder(subtitleStream->codecpar->codec_id);
            if (decoder == nullptr)
            {
                return;
            }

            codecContext = avcodec_alloc_context3(decoder);
            if (codecContext == nullptr)
            {
                throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not allocate FFmpeg subtitle decoder context.");
            }

            auto result = avcodec_parameters_to_context(codecContext, subtitleStream->codecpar);
            if (result < 0)
            {
                throw CreateFfmpegError("avcodec_parameters_to_context", result);
            }

            result = avcodec_open2(codecContext, decoder, nullptr);
            if (result < 0)
            {
                throw CreateFfmpegError("avcodec_open2", result);
            }

            mediaSource.RegisterStream(subtitleStreamIndex.value());
            m_mediaSource = &mediaSource;
            m_codecContext = codecContext;
            m_subtitleStreamIndex = subtitleStreamIndex.value();
            codecContext = nullptr;
        }
        catch (...)
        {
            if (codecContext != nullptr)
            {
                avcodec_free_context(&codecContext);
            }

            Close();
            throw;
        }

        m_cues.clear();
        m_open = true;
    }

    void SubtitleDecoder::PumpQueuedPackets()
    {
        if (!m_open || m_mediaSource == nullptr || m_codecContext == nullptr || m_subtitleStreamIndex < 0)
        {
            return;
        }

        std::unique_ptr<AVPacket, AvPacketDeleter> packet(av_packet_alloc());
        if (!packet)
        {
            throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not allocate FFmpeg subtitle packet.");
        }

        auto subtitleStream = m_mediaSource->Stream(m_subtitleStreamIndex);
        auto packetsDecoded = uint32_t{0};
        while (packetsDecoded < MaxSubtitlePacketsPerPass &&
            m_mediaSource->TryReadQueuedPacket(m_subtitleStreamIndex, packet.get()))
        {
            AVSubtitle subtitle{};
            auto gotSubtitle = 0;
            auto result = avcodec_decode_subtitle2(
                m_codecContext,
                &subtitle,
                &gotSubtitle,
                packet.get());
            if (result < 0)
            {
                av_packet_unref(packet.get());
                throw CreateFfmpegError("avcodec_decode_subtitle2", result);
            }

            if (gotSubtitle)
            {
                try
                {
                    if (auto text = TryCreateSubtitleText(subtitle))
                    {
                        m_cues.push_back(CreateSubtitleCue(packet.get(), subtitle, subtitleStream, std::move(*text)));
                    }
                }
                catch (...)
                {
                    avsubtitle_free(&subtitle);
                    av_packet_unref(packet.get());
                    throw;
                }

                avsubtitle_free(&subtitle);
            }

            av_packet_unref(packet.get());
            ++packetsDecoded;
        }
    }

    std::optional<DecodedSubtitleCue> SubtitleDecoder::TryGetCueAt(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Subtitle position cannot be negative.");
        }

        while (!m_cues.empty() && m_cues.front().EndTicks < positionTicks)
        {
            m_cues.pop_front();
        }

        for (auto const& cue : m_cues)
        {
            if (positionTicks >= cue.StartTicks && positionTicks <= cue.EndTicks)
            {
                return cue;
            }

            if (cue.StartTicks > positionTicks)
            {
                break;
            }
        }

        return std::nullopt;
    }

    void SubtitleDecoder::Flush()
    {
        m_cues.clear();
        if (m_codecContext != nullptr)
        {
            avcodec_flush_buffers(m_codecContext);
        }
    }

    void SubtitleDecoder::Close() noexcept
    {
        if (m_mediaSource != nullptr && m_subtitleStreamIndex >= 0)
        {
            m_mediaSource->UnregisterStream(m_subtitleStreamIndex);
        }

        if (m_codecContext != nullptr)
        {
            avcodec_free_context(&m_codecContext);
        }

        m_cues.clear();
        m_mediaSource = nullptr;
        m_subtitleStreamIndex = -1;
        m_open = false;
    }

    bool SubtitleDecoder::IsOpen() const noexcept
    {
        return m_open;
    }
}
