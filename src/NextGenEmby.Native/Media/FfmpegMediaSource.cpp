#include "pch.h"
#include "FfmpegMediaSource.h"
#include "HttpMediaInput.h"

#include <string>

#pragma warning(push)
#pragma warning(disable : 4244 4819)
extern "C"
{
#include <libavcodec/packet.h>
#include <libavformat/avformat.h>
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
            auto source = winrt::to_string(url);
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

    void FfmpegMediaSource::RegisterStream(int32_t streamIndex)
    {
        if (streamIndex < 0)
        {
            throw winrt::hresult_invalid_argument(L"FFmpeg stream index cannot be negative.");
        }

        m_activeStreams.insert(streamIndex);
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
