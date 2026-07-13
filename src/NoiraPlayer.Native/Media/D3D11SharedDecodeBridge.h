#pragma once

#include <cstdint>

#include <d3d11_4.h>
#include <wrl/client.h>

namespace winrt::NoiraPlayer::Native::implementation
{
    class D3D11SharedDecodeBridge
    {
    public:
        bool Initialize(ID3D11Device* renderDevice, ID3D11DeviceContext* renderContext) noexcept;
        void Close() noexcept;

        ID3D11Device* DecoderDevice() const noexcept { return m_decoderDevice.Get(); }
        ID3D11DeviceContext* DecoderContext() const noexcept { return m_decoderContext.Get(); }
        UINT DecoderTextureMiscFlags() const noexcept { return D3D11_RESOURCE_MISC_SHARED; }

        bool ExportFrame(
            ID3D11Texture2D* decoderTexture,
            Microsoft::WRL::ComPtr<ID3D11Texture2D>& renderTexture,
            uint64_t& fenceValue) noexcept;
        bool WaitForFrame(uint64_t fenceValue) noexcept;

    private:
        Microsoft::WRL::ComPtr<ID3D11Device> m_renderDevice;
        Microsoft::WRL::ComPtr<ID3D11DeviceContext4> m_renderContext;
        Microsoft::WRL::ComPtr<ID3D11Device> m_decoderDevice;
        Microsoft::WRL::ComPtr<ID3D11DeviceContext> m_decoderContext;
        Microsoft::WRL::ComPtr<ID3D11DeviceContext4> m_decoderContext4;
        Microsoft::WRL::ComPtr<ID3D11Fence> m_decoderFence;
        Microsoft::WRL::ComPtr<ID3D11Fence> m_renderFence;
        Microsoft::WRL::ComPtr<ID3D11Texture2D> m_cachedDecoderTexture;
        Microsoft::WRL::ComPtr<ID3D11Texture2D> m_cachedRenderTexture;
        uint64_t m_nextFenceValue{0};
    };
}
