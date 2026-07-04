#pragma once

#include <cstdint>
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <memory>
#include <optional>
#include <wrl/client.h>

namespace winrt::NextGenEmby::Native::implementation
{
    enum class VideoHdrKind
    {
        None,
        Hdr10,
        Hlg
    };

    struct DecodedVideoFrame
    {
        Microsoft::WRL::ComPtr<ID3D11Texture2D> Texture;
        uint32_t Width{0};
        uint32_t Height{0};
        DXGI_FORMAT Format{DXGI_FORMAT_UNKNOWN};
        VideoHdrKind HdrKind{VideoHdrKind::None};
        std::optional<DXGI_HDR_METADATA_HDR10> Hdr10Metadata;
        int64_t PositionTicks{0};
    };

    class VideoDecoder
    {
    public:
        void Open(winrt::hstring const& url, int32_t selectedVideoStreamIndex);
        std::optional<DecodedVideoFrame> TryReadFrame();
        void Seek(int64_t positionTicks);
        void Close() noexcept;

    private:
        winrt::hstring m_url;
        uint32_t m_avformatVersion{0};
        int64_t m_positionTicks{0};
        bool m_open{false};
    };
}
