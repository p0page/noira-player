#include "pch.h"
#include "VideoDecoder.h"
#include "HttpMediaInput.h"

#pragma warning(push)
#pragma warning(disable : 4244 4819)
extern "C"
{
#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
#include <libavutil/error.h>
#include <libavutil/mathematics.h>
}
#pragma warning(pop)

namespace
{
    constexpr AVRational HundredNanosecondTimeBase{1, 10000000};

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

    int32_t FindVideoStreamIndex(AVFormatContext* formatContext, int32_t selectedVideoStreamIndex)
    {
        if (selectedVideoStreamIndex >= 0 &&
            static_cast<uint32_t>(selectedVideoStreamIndex) < formatContext->nb_streams)
        {
            auto stream = formatContext->streams[selectedVideoStreamIndex];
            if (stream != nullptr && stream->codecpar != nullptr &&
                stream->codecpar->codec_type == AVMEDIA_TYPE_VIDEO)
            {
                return selectedVideoStreamIndex;
            }
        }

        auto bestStream = av_find_best_stream(formatContext, AVMEDIA_TYPE_VIDEO, -1, -1, nullptr, 0);
        if (bestStream < 0)
        {
            throw CreateFfmpegError("av_find_best_stream", bestStream);
        }

        return bestStream;
    }
}

namespace winrt::NextGenEmby::Native::implementation
{
    void VideoDecoder::Open(winrt::hstring const& url, int32_t selectedVideoStreamIndex)
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
        AVCodecContext* codecContext = nullptr;

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

            auto videoStreamIndex = FindVideoStreamIndex(formatContext, selectedVideoStreamIndex);
            auto videoStream = formatContext->streams[videoStreamIndex];
            auto decoder = avcodec_find_decoder(videoStream->codecpar->codec_id);
            if (decoder == nullptr)
            {
                throw winrt::hresult_error(E_FAIL, L"FFmpeg video decoder is not available for the selected stream.");
            }

            codecContext = avcodec_alloc_context3(decoder);
            if (codecContext == nullptr)
            {
                throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not allocate FFmpeg video decoder context.");
            }

            result = avcodec_parameters_to_context(codecContext, videoStream->codecpar);
            if (result < 0)
            {
                throw CreateFfmpegError("avcodec_parameters_to_context", result);
            }

            result = avcodec_open2(codecContext, decoder, nullptr);
            if (result < 0)
            {
                throw CreateFfmpegError("avcodec_open2", result);
            }

            m_formatContext = formatContext;
            m_codecContext = codecContext;
            m_videoStreamIndex = videoStreamIndex;
            m_width = codecContext->width > 0 ? static_cast<uint32_t>(codecContext->width) : 0;
            m_height = codecContext->height > 0 ? static_cast<uint32_t>(codecContext->height) : 0;
            formatContext = nullptr;
            codecContext = nullptr;
        }
        catch (...)
        {
            if (codecContext != nullptr)
            {
                avcodec_free_context(&codecContext);
            }

            if (formatContext != nullptr)
            {
                avformat_close_input(&formatContext);
            }

            Close();
            throw;
        }

        m_url = url;
        m_avformatVersion = static_cast<uint32_t>(avformat_version());
        m_positionTicks = 0;
        m_open = true;
    }

    std::optional<DecodedVideoFrame> VideoDecoder::TryReadFrame()
    {
        if (!m_open)
        {
            return std::nullopt;
        }

        return std::nullopt;
    }

    void VideoDecoder::Seek(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Seek position cannot be negative.");
        }

        m_positionTicks = positionTicks;
        if (m_formatContext != nullptr && m_codecContext != nullptr && m_videoStreamIndex >= 0)
        {
            auto videoStream = m_formatContext->streams[m_videoStreamIndex];
            auto timestamp = av_rescale_q(positionTicks, HundredNanosecondTimeBase, videoStream->time_base);
            auto result = av_seek_frame(m_formatContext, m_videoStreamIndex, timestamp, AVSEEK_FLAG_BACKWARD);
            if (result < 0)
            {
                throw CreateFfmpegError("av_seek_frame", result);
            }

            avcodec_flush_buffers(m_codecContext);
        }
    }

    void VideoDecoder::Close() noexcept
    {
        if (m_codecContext != nullptr)
        {
            avcodec_free_context(&m_codecContext);
        }

        if (m_formatContext != nullptr)
        {
            avformat_close_input(&m_formatContext);
        }

        m_url.clear();
        m_videoStreamIndex = -1;
        m_width = 0;
        m_height = 0;
        m_avformatVersion = 0;
        m_positionTicks = 0;
        m_open = false;
    }
}
