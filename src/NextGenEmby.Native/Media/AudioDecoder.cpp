#include "pch.h"
#include "AudioDecoder.h"

#include <memory>
#include <string>

#pragma warning(push)
#pragma warning(disable : 4244 4819)
extern "C"
{
#include <libavcodec/avcodec.h>
#include <libavcodec/codec.h>
#include <libavformat/avformat.h>
#include <libavutil/channel_layout.h>
#include <libavutil/error.h>
#include <libavutil/frame.h>
#include <libavutil/mathematics.h>
#include <libavutil/samplefmt.h>
}
#pragma warning(pop)

namespace
{
    constexpr AVRational HundredNanosecondTimeBase{1, 10000000};

    struct AvFrameDeleter
    {
        void operator()(AVFrame* frame) const noexcept
        {
            av_frame_free(&frame);
        }
    };

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

    winrt::NextGenEmby::Native::implementation::AudioSampleFormat MapSampleFormat(AVSampleFormat sampleFormat)
    {
        switch (av_get_packed_sample_fmt(sampleFormat))
        {
        case AV_SAMPLE_FMT_U8:
            return winrt::NextGenEmby::Native::implementation::AudioSampleFormat::UInt8;
        case AV_SAMPLE_FMT_S16:
            return winrt::NextGenEmby::Native::implementation::AudioSampleFormat::Int16;
        case AV_SAMPLE_FMT_S32:
            return winrt::NextGenEmby::Native::implementation::AudioSampleFormat::Int32;
        case AV_SAMPLE_FMT_FLT:
            return winrt::NextGenEmby::Native::implementation::AudioSampleFormat::Float;
        case AV_SAMPLE_FMT_DBL:
            return winrt::NextGenEmby::Native::implementation::AudioSampleFormat::Double;
        default:
            return winrt::NextGenEmby::Native::implementation::AudioSampleFormat::Unknown;
        }
    }

    uint32_t GetChannelCount(AVFrame const* frame)
    {
        if (frame->ch_layout.nb_channels > 0)
        {
            return static_cast<uint32_t>(frame->ch_layout.nb_channels);
        }

        return 0;
    }

    int64_t GetFramePositionTicks(AVFrame const* frame, AVStream const* stream)
    {
        auto timestamp = frame->best_effort_timestamp;
        if (timestamp == AV_NOPTS_VALUE)
        {
            timestamp = frame->pts;
        }

        if (timestamp == AV_NOPTS_VALUE)
        {
            return 0;
        }

        return av_rescale_q(timestamp, stream->time_base, HundredNanosecondTimeBase);
    }

    winrt::NextGenEmby::Native::implementation::DecodedAudioFrame CreateDecodedAudioFrame(
        AVFrame const* frame,
        AVStream const* stream)
    {
        winrt::NextGenEmby::Native::implementation::DecodedAudioFrame decodedFrame;
        decodedFrame.SampleRate = frame->sample_rate > 0 ? static_cast<uint32_t>(frame->sample_rate) : 0;
        decodedFrame.ChannelCount = GetChannelCount(frame);
        decodedFrame.SampleCount = frame->nb_samples > 0 ? static_cast<uint32_t>(frame->nb_samples) : 0;
        decodedFrame.Format = MapSampleFormat(static_cast<AVSampleFormat>(frame->format));
        decodedFrame.PositionTicks = GetFramePositionTicks(frame, stream);
        return decodedFrame;
    }

    std::optional<winrt::NextGenEmby::Native::implementation::DecodedAudioFrame> TryReceiveFrame(
        AVCodecContext* codecContext,
        AVFrame* frame,
        AVStream const* stream)
    {
        auto receiveResult = avcodec_receive_frame(codecContext, frame);
        if (receiveResult == 0)
        {
            auto decodedFrame = CreateDecodedAudioFrame(frame, stream);
            av_frame_unref(frame);
            return decodedFrame;
        }

        if (receiveResult == AVERROR(EAGAIN) || receiveResult == AVERROR_EOF)
        {
            return std::nullopt;
        }

        throw CreateFfmpegError("avcodec_receive_frame", receiveResult);
    }
}

namespace winrt::NextGenEmby::Native::implementation
{
    void AudioDecoder::Open(
        FfmpegMediaSource& mediaSource,
        int32_t selectedAudioStreamIndex,
        bool hasSelection)
    {
        if (hasSelection && selectedAudioStreamIndex < 0)
        {
            throw winrt::hresult_invalid_argument(L"Audio stream index cannot be negative.");
        }

        Close();

        auto audioStreamIndex = mediaSource.TryFindStream(
            AVMEDIA_TYPE_AUDIO,
            hasSelection ? selectedAudioStreamIndex : -1);
        if (!audioStreamIndex)
        {
            return;
        }

        AVCodecContext* codecContext = nullptr;
        try
        {
            auto audioStream = mediaSource.Stream(audioStreamIndex.value());
            auto decoder = avcodec_find_decoder(audioStream->codecpar->codec_id);
            if (decoder == nullptr)
            {
                throw winrt::hresult_error(E_FAIL, L"FFmpeg audio decoder is not available for the selected stream.");
            }

            codecContext = avcodec_alloc_context3(decoder);
            if (codecContext == nullptr)
            {
                throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not allocate FFmpeg audio decoder context.");
            }

            auto result = avcodec_parameters_to_context(codecContext, audioStream->codecpar);
            if (result < 0)
            {
                throw CreateFfmpegError("avcodec_parameters_to_context", result);
            }

            result = avcodec_open2(codecContext, decoder, nullptr);
            if (result < 0)
            {
                throw CreateFfmpegError("avcodec_open2", result);
            }

            mediaSource.RegisterStream(audioStreamIndex.value());
            m_mediaSource = &mediaSource;
            m_codecContext = codecContext;
            m_audioStreamIndex = audioStreamIndex.value();
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

        m_positionTicks = 0;
        m_decoderDraining = false;
        m_open = true;
    }

    std::optional<DecodedAudioFrame> AudioDecoder::TryReadFrame()
    {
        if (!m_open || m_mediaSource == nullptr || m_codecContext == nullptr || m_audioStreamIndex < 0)
        {
            return std::nullopt;
        }

        std::unique_ptr<AVPacket, AvPacketDeleter> packet(av_packet_alloc());
        std::unique_ptr<AVFrame, AvFrameDeleter> frame(av_frame_alloc());
        if (!packet || !frame)
        {
            throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not allocate FFmpeg audio packet or frame.");
        }

        auto audioStream = m_mediaSource->Stream(m_audioStreamIndex);
        auto publishFrame = [this](std::optional<DecodedAudioFrame> decodedFrame)
            -> std::optional<DecodedAudioFrame>
        {
            if (decodedFrame)
            {
                m_positionTicks = decodedFrame->PositionTicks;
            }

            return decodedFrame;
        };

        if (m_decoderDraining)
        {
            return publishFrame(TryReceiveFrame(m_codecContext, frame.get(), audioStream));
        }

        while (m_mediaSource->TryReadPacket(m_audioStreamIndex, packet.get()))
        {
            std::optional<DecodedAudioFrame> pendingFrame;
            while (true)
            {
                auto sendResult = avcodec_send_packet(m_codecContext, packet.get());
                if (sendResult == 0)
                {
                    av_packet_unref(packet.get());
                    break;
                }

                if (sendResult == AVERROR(EAGAIN))
                {
                    auto drainedFrame = TryReceiveFrame(m_codecContext, frame.get(), audioStream);
                    if (!drainedFrame)
                    {
                        av_packet_unref(packet.get());
                        throw winrt::hresult_error(
                            E_FAIL,
                            L"FFmpeg audio decoder could not accept a packet and produced no frame while draining.");
                    }

                    if (!pendingFrame)
                    {
                        pendingFrame = drainedFrame;
                    }

                    continue;
                }

                av_packet_unref(packet.get());
                throw CreateFfmpegError("avcodec_send_packet", sendResult);
            }

            if (pendingFrame)
            {
                return publishFrame(pendingFrame);
            }

            auto decodedFrame = TryReceiveFrame(m_codecContext, frame.get(), audioStream);
            if (decodedFrame)
            {
                return publishFrame(decodedFrame);
            }
        }

        auto flushResult = avcodec_send_packet(m_codecContext, nullptr);
        if (flushResult < 0 && flushResult != AVERROR_EOF)
        {
            throw CreateFfmpegError("avcodec_send_packet", flushResult);
        }

        m_decoderDraining = true;
        return publishFrame(TryReceiveFrame(m_codecContext, frame.get(), audioStream));
    }

    void AudioDecoder::Flush(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Audio decoder position cannot be negative.");
        }

        m_positionTicks = positionTicks;
        if (m_codecContext != nullptr)
        {
            avcodec_flush_buffers(m_codecContext);
        }

        m_decoderDraining = false;
    }

    void AudioDecoder::Close() noexcept
    {
        if (m_codecContext != nullptr)
        {
            avcodec_free_context(&m_codecContext);
        }

        m_mediaSource = nullptr;
        m_audioStreamIndex = -1;
        m_positionTicks = 0;
        m_decoderDraining = false;
        m_open = false;
    }

    bool AudioDecoder::IsOpen() const noexcept
    {
        return m_open;
    }
}
