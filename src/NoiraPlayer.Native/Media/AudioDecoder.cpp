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
#include <libswresample/swresample.h>
}
#pragma warning(pop)

namespace
{
    constexpr AVRational HundredNanosecondTimeBase{1, 10000000};
    constexpr int OutputSampleRate = 48000;
    constexpr int OutputChannelCount = 2;
    constexpr AVSampleFormat OutputSampleFormat = AV_SAMPLE_FMT_FLT;

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

    SwrContext* CreateResampler(AVCodecContext const* codecContext)
    {
        if (codecContext == nullptr ||
            codecContext->sample_rate <= 0 ||
            codecContext->sample_fmt == AV_SAMPLE_FMT_NONE)
        {
            throw winrt::hresult_error(E_FAIL, L"FFmpeg audio stream does not have a usable sample format.");
        }

        AVChannelLayout inputLayout{};
        AVChannelLayout outputLayout{};
        SwrContext* resampler = nullptr;

        try
        {
            if (codecContext->ch_layout.nb_channels > 0)
            {
                auto result = av_channel_layout_copy(&inputLayout, &codecContext->ch_layout);
                if (result < 0)
                {
                    throw CreateFfmpegError("av_channel_layout_copy", result);
                }
            }
            else
            {
                av_channel_layout_default(&inputLayout, OutputChannelCount);
            }

            av_channel_layout_default(&outputLayout, OutputChannelCount);

            auto result = swr_alloc_set_opts2(
                &resampler,
                &outputLayout,
                OutputSampleFormat,
                OutputSampleRate,
                &inputLayout,
                codecContext->sample_fmt,
                codecContext->sample_rate,
                0,
                nullptr);
            if (result < 0)
            {
                throw CreateFfmpegError("swr_alloc_set_opts2", result);
            }

            result = swr_init(resampler);
            if (result < 0)
            {
                throw CreateFfmpegError("swr_init", result);
            }

            av_channel_layout_uninit(&inputLayout);
            av_channel_layout_uninit(&outputLayout);
            return resampler;
        }
        catch (...)
        {
            av_channel_layout_uninit(&inputLayout);
            av_channel_layout_uninit(&outputLayout);
            if (resampler != nullptr)
            {
                swr_free(&resampler);
            }

            throw;
        }
    }

    winrt::NoiraPlayer::Native::implementation::AudioSampleFormat MapSampleFormat(AVSampleFormat sampleFormat)
    {
        switch (av_get_packed_sample_fmt(sampleFormat))
        {
        case AV_SAMPLE_FMT_U8:
            return winrt::NoiraPlayer::Native::implementation::AudioSampleFormat::UInt8;
        case AV_SAMPLE_FMT_S16:
            return winrt::NoiraPlayer::Native::implementation::AudioSampleFormat::Int16;
        case AV_SAMPLE_FMT_S32:
            return winrt::NoiraPlayer::Native::implementation::AudioSampleFormat::Int32;
        case AV_SAMPLE_FMT_FLT:
            return winrt::NoiraPlayer::Native::implementation::AudioSampleFormat::Float;
        case AV_SAMPLE_FMT_DBL:
            return winrt::NoiraPlayer::Native::implementation::AudioSampleFormat::Double;
        default:
            return winrt::NoiraPlayer::Native::implementation::AudioSampleFormat::Unknown;
        }
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

    std::vector<uint8_t> ConvertFrameToPcm(AVFrame const* frame, SwrContext* resampler)
    {
        if (frame == nullptr || resampler == nullptr || frame->nb_samples <= 0)
        {
            return {};
        }

        auto inputSampleRate = frame->sample_rate > 0 ? frame->sample_rate : OutputSampleRate;
        auto delayedSamples = swr_get_delay(resampler, inputSampleRate);
        auto outputSampleCount = av_rescale_rnd(
            delayedSamples + frame->nb_samples,
            OutputSampleRate,
            inputSampleRate,
            AV_ROUND_UP);
        if (outputSampleCount <= 0)
        {
            return {};
        }

        auto bytesPerSample = av_get_bytes_per_sample(OutputSampleFormat);
        auto outputBytes = outputSampleCount * OutputChannelCount * bytesPerSample;
        std::vector<uint8_t> pcmData(static_cast<size_t>(outputBytes));
        uint8_t* outputPlanes[] = { pcmData.data() };
        auto inputPlanes = const_cast<uint8_t const**>(frame->extended_data);

        auto convertedSamples = swr_convert(
            resampler,
            outputPlanes,
            static_cast<int>(outputSampleCount),
            inputPlanes,
            frame->nb_samples);
        if (convertedSamples < 0)
        {
            throw CreateFfmpegError("swr_convert", convertedSamples);
        }

        pcmData.resize(static_cast<size_t>(convertedSamples) * OutputChannelCount * bytesPerSample);
        return pcmData;
    }

    winrt::NoiraPlayer::Native::implementation::DecodedAudioFrame CreateDecodedAudioFrame(
        AVFrame const* frame,
        AVStream const* stream,
        SwrContext* resampler)
    {
        winrt::NoiraPlayer::Native::implementation::DecodedAudioFrame decodedFrame;
        decodedFrame.PcmData = ConvertFrameToPcm(frame, resampler);
        decodedFrame.SampleRate = OutputSampleRate;
        decodedFrame.ChannelCount = OutputChannelCount;
        decodedFrame.SampleCount = decodedFrame.PcmData.empty()
            ? 0
            : static_cast<uint32_t>(
                decodedFrame.PcmData.size() /
                (OutputChannelCount * av_get_bytes_per_sample(OutputSampleFormat)));
        decodedFrame.Format = MapSampleFormat(OutputSampleFormat);
        decodedFrame.PositionTicks = GetFramePositionTicks(frame, stream);
        return decodedFrame;
    }

    std::optional<winrt::NoiraPlayer::Native::implementation::DecodedAudioFrame> TryReceiveFrame(
        AVCodecContext* codecContext,
        AVFrame* frame,
        AVStream const* stream,
        SwrContext* resampler)
    {
        auto receiveResult = avcodec_receive_frame(codecContext, frame);
        if (receiveResult == 0)
        {
            auto decodedFrame = CreateDecodedAudioFrame(frame, stream, resampler);
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

namespace winrt::NoiraPlayer::Native::implementation
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
        SwrContext* resampler = nullptr;
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

            resampler = CreateResampler(codecContext);
            mediaSource.RegisterStream(audioStreamIndex.value());
            m_mediaSource = &mediaSource;
            m_codecContext = codecContext;
            m_resampler = resampler;
            m_audioStreamIndex = audioStreamIndex.value();
            codecContext = nullptr;
            resampler = nullptr;
        }
        catch (...)
        {
            if (codecContext != nullptr)
            {
                avcodec_free_context(&codecContext);
            }

            if (resampler != nullptr)
            {
                swr_free(&resampler);
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
        if (!m_open ||
            m_mediaSource == nullptr ||
            m_codecContext == nullptr ||
            m_resampler == nullptr ||
            m_audioStreamIndex < 0)
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
            return publishFrame(TryReceiveFrame(m_codecContext, frame.get(), audioStream, m_resampler));
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
                    auto drainedFrame = TryReceiveFrame(m_codecContext, frame.get(), audioStream, m_resampler);
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

            auto decodedFrame = TryReceiveFrame(m_codecContext, frame.get(), audioStream, m_resampler);
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
        return publishFrame(TryReceiveFrame(m_codecContext, frame.get(), audioStream, m_resampler));
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

        if (m_resampler != nullptr)
        {
            swr_close(m_resampler);
            auto result = swr_init(m_resampler);
            if (result < 0)
            {
                throw CreateFfmpegError("swr_init", result);
            }
        }

        m_decoderDraining = false;
    }

    void AudioDecoder::Close() noexcept
    {
        if (m_mediaSource != nullptr && m_audioStreamIndex >= 0)
        {
            m_mediaSource->UnregisterStream(m_audioStreamIndex);
        }

        if (m_codecContext != nullptr)
        {
            avcodec_free_context(&m_codecContext);
        }

        if (m_resampler != nullptr)
        {
            swr_free(&m_resampler);
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
