#pragma once

#include <cstdint>
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <memory>
#include <optional>
#include <vector>
#include <wrl/client.h>

#include "DolbyVisionConfiguration.h"
#include "DxgiColorSpaceMapper.h"

struct AVCodecContext;
struct AVBufferRef;
struct AVPacket;
struct AVStream;

namespace winrt::NoiraPlayer::Native::implementation
{
    class FfmpegMediaSource;

    enum class VideoHdrKind
    {
        None,
        Hdr10,
        Hlg
    };

    struct DecodedVideoFrame
    {
        Microsoft::WRL::ComPtr<ID3D11Texture2D> Texture;
        uint32_t TextureArrayIndex{0};
        uint32_t Width{0};
        uint32_t Height{0};
        uint32_t DisplayWidth{0};
        uint32_t DisplayHeight{0};
        DXGI_FORMAT Format{DXGI_FORMAT_UNKNOWN};
        VideoHdrKind HdrKind{VideoHdrKind::None};
        VideoColorMetadata ColorMetadata{};
        bool UsesBt709Matrix{true};
        bool IsFullRange{false};
        std::optional<DXGI_HDR_METADATA_HDR10> Hdr10Metadata;
        int64_t PositionTicks{0};
        std::vector<uint8_t> BgraPixels;
        uint32_t BgraStride{0};
    };

    class VideoDecoder
    {
    public:
        void Open(
            FfmpegMediaSource& mediaSource,
            int32_t selectedVideoStreamIndex,
            ID3D11Device* d3dDevice,
            ID3D11DeviceContext* d3dContext);
        std::optional<DecodedVideoFrame> TryReadFrame();
        void Seek(int64_t positionTicks);
        void Close() noexcept;

    private:
        FfmpegMediaSource* m_mediaSource{nullptr};
        AVCodecContext* m_codecContext{nullptr};
        AVBufferRef* m_hardwareDeviceContext{nullptr};
        int m_hardwarePixelFormat{-1};
        int32_t m_videoStreamIndex{-1};
        uint32_t m_width{0};
        uint32_t m_height{0};
        int64_t m_positionTicks{0};
        std::optional<DolbyVisionConfiguration> m_dolbyVisionConfiguration;
        bool m_decoderDraining{false};
        bool m_open{false};

        void ApplyDolbyVisionConfigurationSideData(
            uint8_t const* sideData,
            size_t sideDataSize,
            wchar_t const* source);
        void InspectDolbyVisionStreamSideData(::AVStream const* stream);
        void InspectDolbyVisionPacketSideData(AVPacket const* packet);
    };
}
