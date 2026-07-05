#pragma once

#include <cstdint>
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <string>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <wrl/client.h>

namespace winrt::NextGenEmby::Native::implementation
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
            uint32_t height);
        bool DrawBgraFrameToBackBuffer(
            uint8_t const* pixels,
            uint32_t width,
            uint32_t height,
            uint32_t stride);
        bool DrawTextOverlay(std::wstring const& text);
        bool ClearToBlack();
        bool Present();
        ID3D11Device* Device() const noexcept;
        ID3D11DeviceContext* Context() const noexcept;

    private:
        Microsoft::WRL::ComPtr<ID3D11Device> m_device;
        Microsoft::WRL::ComPtr<ID3D11DeviceContext> m_context;
        Microsoft::WRL::ComPtr<IDXGISwapChain3> m_swapChain;
        winrt::Windows::UI::Xaml::Controls::SwapChainPanel m_panel{nullptr};
    };
}
