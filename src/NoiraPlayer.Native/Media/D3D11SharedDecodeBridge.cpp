#include "pch.h"
#include "D3D11SharedDecodeBridge.h"

#include <dxgi.h>
#include <utility>

namespace winrt::NoiraPlayer::Native::implementation
{
    bool D3D11SharedDecodeBridge::Initialize(
        ID3D11Device* renderDevice,
        ID3D11DeviceContext* renderContext) noexcept
    {
        Close();
        if (renderDevice == nullptr || renderContext == nullptr)
        {
            return false;
        }

        Microsoft::WRL::ComPtr<IDXGIDevice> dxgiDevice;
        Microsoft::WRL::ComPtr<IDXGIAdapter> adapter;
        if (FAILED(renderDevice->QueryInterface(IID_PPV_ARGS(&dxgiDevice))) ||
            FAILED(dxgiDevice->GetAdapter(&adapter)))
        {
            return false;
        }

        UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT | D3D11_CREATE_DEVICE_VIDEO_SUPPORT;
        auto result = D3D11CreateDevice(
            adapter.Get(),
            D3D_DRIVER_TYPE_UNKNOWN,
            nullptr,
            creationFlags,
            nullptr,
            0,
            D3D11_SDK_VERSION,
            &m_decoderDevice,
            nullptr,
            &m_decoderContext);
        if (FAILED(result))
        {
            Close();
            return false;
        }

        Microsoft::WRL::ComPtr<ID3D11Device5> decoderDevice5;
        Microsoft::WRL::ComPtr<ID3D11Device5> renderDevice5;
        if (FAILED(m_decoderDevice.As(&decoderDevice5)) ||
            FAILED(m_decoderContext.As(&m_decoderContext4)) ||
            FAILED(renderDevice->QueryInterface(IID_PPV_ARGS(&renderDevice5))) ||
            FAILED(renderContext->QueryInterface(IID_PPV_ARGS(&m_renderContext))))
        {
            Close();
            return false;
        }

        Microsoft::WRL::ComPtr<ID3D11Multithread> decoderMultithread;
        if (SUCCEEDED(m_decoderContext.As(&decoderMultithread)))
        {
            decoderMultithread->SetMultithreadProtected(TRUE);
        }

        result = decoderDevice5->CreateFence(
            0,
            D3D11_FENCE_FLAG_SHARED,
            IID_PPV_ARGS(&m_decoderFence));
        if (FAILED(result))
        {
            Close();
            return false;
        }

        HANDLE sharedFenceHandle = nullptr;
        result = m_decoderFence->CreateSharedHandle(
            nullptr,
            GENERIC_ALL,
            nullptr,
            &sharedFenceHandle);
        if (FAILED(result) || sharedFenceHandle == nullptr)
        {
            Close();
            return false;
        }

        result = renderDevice5->OpenSharedFence(
            sharedFenceHandle,
            IID_PPV_ARGS(&m_renderFence));
        CloseHandle(sharedFenceHandle);
        if (FAILED(result))
        {
            Close();
            return false;
        }

        m_renderDevice = renderDevice;
        m_nextFenceValue = 0;
        return true;
    }

    void D3D11SharedDecodeBridge::Close() noexcept
    {
        m_cachedRenderTexture.Reset();
        m_cachedDecoderTexture.Reset();
        m_renderFence.Reset();
        m_decoderFence.Reset();
        m_renderContext.Reset();
        m_decoderContext4.Reset();
        m_decoderContext.Reset();
        m_decoderDevice.Reset();
        m_renderDevice.Reset();
        m_nextFenceValue = 0;
    }

    bool D3D11SharedDecodeBridge::ExportFrame(
        ID3D11Texture2D* decoderTexture,
        Microsoft::WRL::ComPtr<ID3D11Texture2D>& renderTexture,
        uint64_t& fenceValue) noexcept
    {
        renderTexture.Reset();
        fenceValue = 0;
        if (decoderTexture == nullptr ||
            m_renderDevice == nullptr ||
            m_decoderContext4 == nullptr ||
            m_decoderFence == nullptr)
        {
            return false;
        }

        if (m_cachedDecoderTexture.Get() != decoderTexture)
        {
            Microsoft::WRL::ComPtr<IDXGIResource> sharedResource;
            HANDLE sharedTextureHandle = nullptr;
            if (FAILED(decoderTexture->QueryInterface(IID_PPV_ARGS(&sharedResource))) ||
                FAILED(sharedResource->GetSharedHandle(&sharedTextureHandle)) ||
                sharedTextureHandle == nullptr)
            {
                return false;
            }

            Microsoft::WRL::ComPtr<ID3D11Texture2D> openedTexture;
            if (FAILED(m_renderDevice->OpenSharedResource(
                sharedTextureHandle,
                IID_PPV_ARGS(&openedTexture))))
            {
                return false;
            }

            m_cachedDecoderTexture = decoderTexture;
            m_cachedRenderTexture = std::move(openedTexture);
        }

        auto nextFenceValue = ++m_nextFenceValue;
        if (FAILED(m_decoderContext4->Signal(m_decoderFence.Get(), nextFenceValue)))
        {
            return false;
        }

        renderTexture = m_cachedRenderTexture;
        fenceValue = nextFenceValue;
        return true;
    }

    bool D3D11SharedDecodeBridge::WaitForFrame(uint64_t fenceValue) noexcept
    {
        return fenceValue > 0 &&
            m_renderContext != nullptr &&
            m_renderFence != nullptr &&
            SUCCEEDED(m_renderContext->Wait(m_renderFence.Get(), fenceValue));
    }
}
