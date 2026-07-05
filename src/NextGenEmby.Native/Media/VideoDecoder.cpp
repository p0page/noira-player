#include "pch.h"
#include "VideoDecoder.h"
#include "FfmpegMediaSource.h"

#include <algorithm>
#include <cmath>
#include <limits>

#pragma warning(push)
#pragma warning(disable : 4244 4819)
extern "C"
{
#include <libavcodec/avcodec.h>
#include <libavcodec/codec.h>
#include <libavformat/avformat.h>
#include <libavutil/buffer.h>
#include <libavutil/error.h>
#include <libavutil/frame.h>
#include <libavutil/hwcontext.h>
#include <libavutil/hwcontext_d3d11va.h>
#include <libavutil/mastering_display_metadata.h>
#include <libavutil/mathematics.h>
#include <libavutil/pixfmt.h>
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

    struct AvBufferRefDeleter
    {
        void operator()(AVBufferRef* buffer) const noexcept
        {
            av_buffer_unref(&buffer);
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

    AVPixelFormat FindD3D11HardwarePixelFormat(AVCodec const* decoder)
    {
        for (auto index = 0;; ++index)
        {
            auto config = avcodec_get_hw_config(decoder, index);
            if (config == nullptr)
            {
                return AV_PIX_FMT_NONE;
            }

            if ((config->methods & AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX) != 0 &&
                config->device_type == AV_HWDEVICE_TYPE_D3D11VA)
            {
                return config->pix_fmt;
            }
        }
    }

    AVBufferRef* TryCreateD3D11HardwareDevice(ID3D11Device* device, ID3D11DeviceContext* context)
    {
        if (device == nullptr)
        {
            return nullptr;
        }

        std::unique_ptr<AVBufferRef, AvBufferRefDeleter> hardwareDevice(
            av_hwdevice_ctx_alloc(AV_HWDEVICE_TYPE_D3D11VA));
        if (!hardwareDevice)
        {
            return nullptr;
        }

        auto hardwareContext = reinterpret_cast<AVHWDeviceContext*>(hardwareDevice->data);
        auto d3d11Context = static_cast<AVD3D11VADeviceContext*>(hardwareContext->hwctx);
        d3d11Context->device = device;
        d3d11Context->device->AddRef();

        if (context != nullptr)
        {
            d3d11Context->device_context = context;
            d3d11Context->device_context->AddRef();
        }

        auto result = av_hwdevice_ctx_init(hardwareDevice.get());
        if (result < 0)
        {
            return nullptr;
        }

        return hardwareDevice.release();
    }

    AVPixelFormat SelectHardwarePixelFormat(AVCodecContext* context, AVPixelFormat const* formats)
    {
        auto requestedFormat = AV_PIX_FMT_NONE;
        if (context != nullptr && context->opaque != nullptr)
        {
            requestedFormat = static_cast<AVPixelFormat>(*static_cast<int*>(context->opaque));
        }

        for (auto candidate = formats; candidate != nullptr && *candidate != AV_PIX_FMT_NONE; ++candidate)
        {
            if (*candidate == requestedFormat)
            {
                return *candidate;
            }
        }

        return formats != nullptr ? formats[0] : AV_PIX_FMT_NONE;
    }

    AVPixelFormat GetFrameSoftwarePixelFormat(AVFrame const* frame)
    {
        auto frameFormat = static_cast<AVPixelFormat>(frame->format);
        if (frameFormat == AV_PIX_FMT_D3D11 && frame->hw_frames_ctx != nullptr)
        {
            auto framesContext = reinterpret_cast<AVHWFramesContext*>(frame->hw_frames_ctx->data);
            if (framesContext != nullptr)
            {
                return static_cast<AVPixelFormat>(framesContext->sw_format);
            }
        }

        return frameFormat;
    }

    void AttachD3D11Texture(
        winrt::NextGenEmby::Native::implementation::DecodedVideoFrame& decodedFrame,
        AVFrame const* frame)
    {
        if (static_cast<AVPixelFormat>(frame->format) != AV_PIX_FMT_D3D11 || frame->data[0] == nullptr)
        {
            return;
        }

        auto texture = reinterpret_cast<ID3D11Texture2D*>(frame->data[0]);
        texture->AddRef();
        decodedFrame.Texture.Attach(texture);
        decodedFrame.TextureArrayIndex = static_cast<uint32_t>(reinterpret_cast<intptr_t>(frame->data[1]));
    }

    uint32_t ScaleRational(AVRational value, double scale, uint32_t maximum)
    {
        if (value.den <= 0 || value.num < 0)
        {
            return 0;
        }

        auto scaled = std::llround((static_cast<double>(value.num) / value.den) * scale);
        if (scaled <= 0)
        {
            return 0;
        }

        auto clamped = std::min<int64_t>(scaled, maximum);
        return static_cast<uint32_t>(clamped);
    }

    uint16_t ScaleChromaticity(AVRational value)
    {
        return static_cast<uint16_t>(ScaleRational(value, 50000.0, 50000));
    }

    uint16_t ClampToUInt16(unsigned value)
    {
        return static_cast<uint16_t>(std::min<unsigned>(
            value,
            (std::numeric_limits<uint16_t>::max)()));
    }

    void SetChromaticity(uint16_t target[2], AVRational const source[2])
    {
        target[0] = ScaleChromaticity(source[0]);
        target[1] = ScaleChromaticity(source[1]);
    }

    std::optional<DXGI_HDR_METADATA_HDR10> TryCreateHdr10Metadata(AVFrame const* frame)
    {
        DXGI_HDR_METADATA_HDR10 metadata{};
        auto hasMetadata = false;

        auto masteringSideData = av_frame_get_side_data(frame, AV_FRAME_DATA_MASTERING_DISPLAY_METADATA);
        if (masteringSideData != nullptr && masteringSideData->data != nullptr)
        {
            auto mastering = reinterpret_cast<AVMasteringDisplayMetadata*>(masteringSideData->data);
            if (mastering->has_primaries)
            {
                SetChromaticity(metadata.RedPrimary, mastering->display_primaries[0]);
                SetChromaticity(metadata.GreenPrimary, mastering->display_primaries[1]);
                SetChromaticity(metadata.BluePrimary, mastering->display_primaries[2]);
                SetChromaticity(metadata.WhitePoint, mastering->white_point);
                hasMetadata = true;
            }

            if (mastering->has_luminance)
            {
                metadata.MaxMasteringLuminance = ScaleRational(
                    mastering->max_luminance,
                    1.0,
                    (std::numeric_limits<uint32_t>::max)());
                metadata.MinMasteringLuminance = ScaleRational(
                    mastering->min_luminance,
                    10000.0,
                    (std::numeric_limits<uint32_t>::max)());
                hasMetadata = true;
            }
        }

        auto lightSideData = av_frame_get_side_data(frame, AV_FRAME_DATA_CONTENT_LIGHT_LEVEL);
        if (lightSideData != nullptr && lightSideData->data != nullptr)
        {
            auto light = reinterpret_cast<AVContentLightMetadata*>(lightSideData->data);
            metadata.MaxContentLightLevel = ClampToUInt16(light->MaxCLL);
            metadata.MaxFrameAverageLightLevel = ClampToUInt16(light->MaxFALL);
            hasMetadata = true;
        }

        if (!hasMetadata)
        {
            return std::nullopt;
        }

        return metadata;
    }

    DXGI_FORMAT MapPixelFormat(AVPixelFormat pixelFormat)
    {
        switch (pixelFormat)
        {
        case AV_PIX_FMT_NV12:
            return DXGI_FORMAT_NV12;
        case AV_PIX_FMT_P010LE:
            return DXGI_FORMAT_P010;
        case AV_PIX_FMT_BGRA:
            return DXGI_FORMAT_B8G8R8A8_UNORM;
        case AV_PIX_FMT_RGBA:
            return DXGI_FORMAT_R8G8B8A8_UNORM;
        default:
            return DXGI_FORMAT_UNKNOWN;
        }
    }

    winrt::NextGenEmby::Native::implementation::VideoHdrKind MapHdrKind(AVColorTransferCharacteristic transfer)
    {
        switch (transfer)
        {
        case AVCOL_TRC_SMPTE2084:
            return winrt::NextGenEmby::Native::implementation::VideoHdrKind::Hdr10;
        case AVCOL_TRC_ARIB_STD_B67:
            return winrt::NextGenEmby::Native::implementation::VideoHdrKind::Hlg;
        default:
            return winrt::NextGenEmby::Native::implementation::VideoHdrKind::None;
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

    winrt::NextGenEmby::Native::implementation::DecodedVideoFrame CreateDecodedVideoFrame(
        AVFrame const* frame,
        AVStream const* stream)
    {
        winrt::NextGenEmby::Native::implementation::DecodedVideoFrame decodedFrame;
        decodedFrame.Width = frame->width > 0 ? static_cast<uint32_t>(frame->width) : 0;
        decodedFrame.Height = frame->height > 0 ? static_cast<uint32_t>(frame->height) : 0;
        decodedFrame.Format = MapPixelFormat(GetFrameSoftwarePixelFormat(frame));
        decodedFrame.HdrKind = MapHdrKind(frame->color_trc);
        decodedFrame.PositionTicks = GetFramePositionTicks(frame, stream);
        if (decodedFrame.HdrKind == winrt::NextGenEmby::Native::implementation::VideoHdrKind::Hdr10)
        {
            decodedFrame.Hdr10Metadata = TryCreateHdr10Metadata(frame);
        }

        AttachD3D11Texture(decodedFrame, frame);
        return decodedFrame;
    }

    std::optional<winrt::NextGenEmby::Native::implementation::DecodedVideoFrame> TryReceiveFrame(
        AVCodecContext* codecContext,
        AVFrame* frame,
        AVStream const* stream)
    {
        auto receiveResult = avcodec_receive_frame(codecContext, frame);
        if (receiveResult == 0)
        {
            auto decodedFrame = CreateDecodedVideoFrame(frame, stream);
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
    void VideoDecoder::Open(
        FfmpegMediaSource& mediaSource,
        int32_t selectedVideoStreamIndex,
        ID3D11Device* d3dDevice,
        ID3D11DeviceContext* d3dContext)
    {
        Close();

        AVCodecContext* codecContext = nullptr;
        AVBufferRef* hardwareDeviceContext = nullptr;

        try
        {
            auto videoStreamIndex = mediaSource.FindRequiredStream(AVMEDIA_TYPE_VIDEO, selectedVideoStreamIndex);
            auto videoStream = mediaSource.Stream(videoStreamIndex);
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

            auto hardwarePixelFormat = FindD3D11HardwarePixelFormat(decoder);
            if (hardwarePixelFormat != AV_PIX_FMT_NONE)
            {
                hardwareDeviceContext = TryCreateD3D11HardwareDevice(d3dDevice, d3dContext);
                if (hardwareDeviceContext != nullptr)
                {
                    m_hardwarePixelFormat = hardwarePixelFormat;
                    codecContext->opaque = &m_hardwarePixelFormat;
                    codecContext->get_format = SelectHardwarePixelFormat;
                    codecContext->hw_device_ctx = av_buffer_ref(hardwareDeviceContext);
                    if (codecContext->hw_device_ctx == nullptr)
                    {
                        throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not reference FFmpeg D3D11VA device context.");
                    }
                }
            }

            auto result = avcodec_parameters_to_context(codecContext, videoStream->codecpar);
            if (result < 0)
            {
                throw CreateFfmpegError("avcodec_parameters_to_context", result);
            }

            result = avcodec_open2(codecContext, decoder, nullptr);
            if (result < 0)
            {
                throw CreateFfmpegError("avcodec_open2", result);
            }

            mediaSource.RegisterStream(videoStreamIndex);
            m_mediaSource = &mediaSource;
            m_codecContext = codecContext;
            m_hardwareDeviceContext = hardwareDeviceContext;
            m_videoStreamIndex = videoStreamIndex;
            m_width = codecContext->width > 0 ? static_cast<uint32_t>(codecContext->width) : 0;
            m_height = codecContext->height > 0 ? static_cast<uint32_t>(codecContext->height) : 0;
            codecContext = nullptr;
            hardwareDeviceContext = nullptr;
        }
        catch (...)
        {
            if (codecContext != nullptr)
            {
                avcodec_free_context(&codecContext);
            }

            if (hardwareDeviceContext != nullptr)
            {
                av_buffer_unref(&hardwareDeviceContext);
            }

            Close();
            throw;
        }

        m_positionTicks = 0;
        m_decoderDraining = false;
        m_open = true;
    }

    std::optional<DecodedVideoFrame> VideoDecoder::TryReadFrame()
    {
        if (!m_open || m_mediaSource == nullptr || m_codecContext == nullptr || m_videoStreamIndex < 0)
        {
            return std::nullopt;
        }

        std::unique_ptr<AVPacket, AvPacketDeleter> packet(av_packet_alloc());
        std::unique_ptr<AVFrame, AvFrameDeleter> frame(av_frame_alloc());
        if (!packet || !frame)
        {
            throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not allocate FFmpeg decode packet or frame.");
        }

        auto videoStream = m_mediaSource->Stream(m_videoStreamIndex);
        auto publishFrame = [this](std::optional<DecodedVideoFrame> decodedFrame)
            -> std::optional<DecodedVideoFrame>
        {
            if (decodedFrame)
            {
                m_positionTicks = decodedFrame->PositionTicks;
            }

            return decodedFrame;
        };

        if (m_decoderDraining)
        {
            return publishFrame(TryReceiveFrame(m_codecContext, frame.get(), videoStream));
        }

        while (m_mediaSource->TryReadPacket(m_videoStreamIndex, packet.get()))
        {
            std::optional<DecodedVideoFrame> pendingFrame;
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
                    auto drainedFrame = TryReceiveFrame(m_codecContext, frame.get(), videoStream);
                    if (!drainedFrame)
                    {
                        av_packet_unref(packet.get());
                        throw winrt::hresult_error(
                            E_FAIL,
                            L"FFmpeg decoder could not accept a packet and produced no frame while draining.");
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

            auto decodedFrame = TryReceiveFrame(m_codecContext, frame.get(), videoStream);
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
        return publishFrame(TryReceiveFrame(m_codecContext, frame.get(), videoStream));
    }

    void VideoDecoder::Seek(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Seek position cannot be negative.");
        }

        m_positionTicks = positionTicks;
        if (m_mediaSource != nullptr && m_codecContext != nullptr && m_videoStreamIndex >= 0)
        {
            auto videoStream = m_mediaSource->Stream(m_videoStreamIndex);
            auto timestamp = av_rescale_q(positionTicks, HundredNanosecondTimeBase, videoStream->time_base);
            m_mediaSource->Seek(m_videoStreamIndex, timestamp);
            avcodec_flush_buffers(m_codecContext);
            m_decoderDraining = false;
        }
    }

    void VideoDecoder::Close() noexcept
    {
        if (m_mediaSource != nullptr && m_videoStreamIndex >= 0)
        {
            m_mediaSource->UnregisterStream(m_videoStreamIndex);
        }

        if (m_codecContext != nullptr)
        {
            avcodec_free_context(&m_codecContext);
        }

        if (m_hardwareDeviceContext != nullptr)
        {
            av_buffer_unref(&m_hardwareDeviceContext);
        }

        m_mediaSource = nullptr;
        m_hardwarePixelFormat = -1;
        m_videoStreamIndex = -1;
        m_width = 0;
        m_height = 0;
        m_positionTicks = 0;
        m_decoderDraining = false;
        m_open = false;
    }
}
