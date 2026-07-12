#pragma once

#include <cstdint>
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <string>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <wrl/client.h>

#include "Media/DxgiColorSpaceMapper.h"
#include "Media/HdrToneMappingPass.h"
#include "Media/SubtitleBitmap.h"

namespace winrt::NoiraPlayer::Native::implementation
{
    class DxDeviceResources
    {
    public:
        void AttachSurface(winrt::Windows::UI::Xaml::Controls::SwapChainPanel const& panel);
        void CreateDevice();
        void CreateSwapChain(uint32_t width, uint32_t height, bool useTenBit);
        bool SetHdr10ColorSpace();
        bool SetSdrColorSpace();
        bool SetHdr10Metadata(DXGI_HDR_METADATA_HDR10 const& metadata);
        bool TryCopyToBackBuffer(ID3D11Texture2D* texture);
        bool TryProcessVideoFrameToBackBuffer(
            ID3D11Texture2D* texture,
            uint32_t arraySlice,
            uint32_t width,
            uint32_t height,
            uint32_t displayWidth,
            uint32_t displayHeight,
            VideoColorMetadata const& colorMetadata,
            bool outputHdr10,
            DXGI_HDR_METADATA_HDR10 const* hdr10Metadata);
        bool DrawBgraFrameToBackBuffer(
            uint8_t const* pixels,
            uint32_t width,
            uint32_t height,
            uint32_t displayWidth,
            uint32_t displayHeight,
            uint32_t stride);
        bool DrawTextOverlay(std::wstring const& text);
        bool DrawSubtitleBitmapOverlay(SubtitleBitmapRegion const& region);
        bool ClearToBlack();
        bool Present();
        void ObserveVideoColorMapping(VideoColorMetadata const& colorMetadata, bool outputHdr10);
        bool HasRenderTarget() const noexcept;
        ID3D11Device* Device() const noexcept;
        ID3D11DeviceContext* Context() const noexcept;
        DXGI_FORMAT SwapChainFormat() const noexcept;
        DXGI_COLOR_SPACE_TYPE SwapChainColorSpace() const noexcept;
        bool IsTenBitSwapChain() const noexcept;
        bool LastVideoProcessorConversionWasValidated() const noexcept;
        DXGI_COLOR_SPACE_TYPE LastVideoProcessorInputColorSpace() const noexcept;
        DXGI_COLOR_SPACE_TYPE LastVideoProcessorOutputColorSpace() const noexcept;
        std::wstring LastVideoProcessorConversionStatus() const;

    private:
        bool ClearBackBufferToBlack(bool present);
        bool ClearTextureToBlack(ID3D11Texture2D* texture);
        bool EnsureSubtitleOverlayResources();
        bool DrawSubtitleBitmapOverlayD3d11(SubtitleBitmapRegion const& region);
        void SetVideoProcessorConversionStatus(
            DXGI_COLOR_SPACE_TYPE inputColorSpace,
            DXGI_COLOR_SPACE_TYPE outputColorSpace,
            std::wstring status);

        Microsoft::WRL::ComPtr<ID3D11Device> m_device;
        Microsoft::WRL::ComPtr<ID3D11DeviceContext> m_context;
        Microsoft::WRL::ComPtr<IDXGISwapChain3> m_swapChain;
        winrt::Windows::UI::Xaml::Controls::SwapChainPanel m_panel{nullptr};
        DXGI_FORMAT m_swapChainFormat{DXGI_FORMAT_UNKNOWN};
        DXGI_COLOR_SPACE_TYPE m_swapChainColorSpace{DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709};
        bool m_isTenBitSwapChain{false};
        bool m_lastVideoProcessorConversionValidated{false};
        DXGI_COLOR_SPACE_TYPE m_lastVideoProcessorInputColorSpace{DXGI_COLOR_SPACE_CUSTOM};
        DXGI_COLOR_SPACE_TYPE m_lastVideoProcessorOutputColorSpace{DXGI_COLOR_SPACE_CUSTOM};
        std::wstring m_lastVideoProcessorConversionStatus{L"not-run"};
        HdrToneMappingPass m_hdrToneMappingPass;
        Microsoft::WRL::ComPtr<ID3D11VertexShader> m_subtitleVertexShader;
        Microsoft::WRL::ComPtr<ID3D11PixelShader> m_subtitlePixelShader;
        Microsoft::WRL::ComPtr<ID3D11SamplerState> m_subtitleSampler;
        Microsoft::WRL::ComPtr<ID3D11BlendState> m_subtitleBlendState;
        Microsoft::WRL::ComPtr<ID3D11Buffer> m_subtitleConstants;
        Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> m_cachedSubtitleView;
        uint64_t m_cachedSubtitleHash{0};
        uint32_t m_cachedSubtitleWidth{0};
        uint32_t m_cachedSubtitleHeight{0};
        uint32_t m_cachedSubtitleStride{0};
    };
}
