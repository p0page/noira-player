#include "pch.h"
#include "DxDeviceResources.h"

#include <windows.ui.xaml.media.dxinterop.h>

namespace winrt::NextGenEmby::Native::implementation
{
    void DxDeviceResources::AttachSurface(winrt::Windows::UI::Xaml::Controls::SwapChainPanel const& panel)
    {
        if (panel == nullptr)
        {
            throw winrt::hresult_invalid_argument(L"SwapChainPanel is required.");
        }

        m_panel = panel;

        auto width = static_cast<uint32_t>(panel.ActualWidth());
        auto height = static_cast<uint32_t>(panel.ActualHeight());
        CreateSwapChain(width == 0 ? 1 : width, height == 0 ? 1 : height, false);

        Microsoft::WRL::ComPtr<ISwapChainPanelNative> panelNative;
        winrt::check_hresult(winrt::get_unknown(m_panel)->QueryInterface(IID_PPV_ARGS(&panelNative)));
        winrt::check_hresult(panelNative->SetSwapChain(m_swapChain.Get()));
    }

    void DxDeviceResources::CreateDevice()
    {
        if (m_device)
        {
            return;
        }

        D3D_FEATURE_LEVEL featureLevels[] =
        {
            D3D_FEATURE_LEVEL_11_1,
            D3D_FEATURE_LEVEL_11_0
        };

        D3D_FEATURE_LEVEL selectedFeatureLevel{};
        winrt::check_hresult(D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_HARDWARE,
            nullptr,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            featureLevels,
            ARRAYSIZE(featureLevels),
            D3D11_SDK_VERSION,
            m_device.ReleaseAndGetAddressOf(),
            &selectedFeatureLevel,
            m_context.ReleaseAndGetAddressOf()));
    }

    void DxDeviceResources::CreateSwapChain(uint32_t width, uint32_t height, bool useTenBit)
    {
        CreateDevice();

        Microsoft::WRL::ComPtr<IDXGIDevice3> dxgiDevice;
        winrt::check_hresult(m_device.As(&dxgiDevice));

        Microsoft::WRL::ComPtr<IDXGIAdapter> adapter;
        winrt::check_hresult(dxgiDevice->GetAdapter(adapter.ReleaseAndGetAddressOf()));

        Microsoft::WRL::ComPtr<IDXGIFactory2> factory;
        winrt::check_hresult(adapter->GetParent(IID_PPV_ARGS(&factory)));

        DXGI_SWAP_CHAIN_DESC1 description{};
        description.Width = width == 0 ? 1 : width;
        description.Height = height == 0 ? 1 : height;
        description.Format = useTenBit ? DXGI_FORMAT_R10G10B10A2_UNORM : DXGI_FORMAT_B8G8R8A8_UNORM;
        description.Stereo = false;
        description.SampleDesc.Count = 1;
        description.SampleDesc.Quality = 0;
        description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        description.BufferCount = 2;
        description.Scaling = DXGI_SCALING_STRETCH;
        description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
        description.AlphaMode = DXGI_ALPHA_MODE_IGNORE;
        description.Flags = 0;

        Microsoft::WRL::ComPtr<IDXGISwapChain1> swapChain;
        winrt::check_hresult(factory->CreateSwapChainForComposition(
            m_device.Get(),
            &description,
            nullptr,
            swapChain.ReleaseAndGetAddressOf()));

        winrt::check_hresult(swapChain.As(&m_swapChain));
        SetSdrColorSpace();
    }

    bool DxDeviceResources::SetHdr10ColorSpace()
    {
        if (!m_swapChain)
        {
            return false;
        }

        return SUCCEEDED(m_swapChain->SetColorSpace1(DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020));
    }

    bool DxDeviceResources::SetSdrColorSpace()
    {
        if (!m_swapChain)
        {
            return false;
        }

        return SUCCEEDED(m_swapChain->SetColorSpace1(DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709));
    }

    bool DxDeviceResources::SetHdr10Metadata(DXGI_HDR_METADATA_HDR10 const& metadata)
    {
        Microsoft::WRL::ComPtr<IDXGISwapChain4> swapChain4;
        if (!m_swapChain || FAILED(m_swapChain.As(&swapChain4)))
        {
            return false;
        }

        return SUCCEEDED(swapChain4->SetHDRMetaData(
            DXGI_HDR_METADATA_TYPE_HDR10,
            sizeof(metadata),
            const_cast<DXGI_HDR_METADATA_HDR10*>(&metadata)));
    }

    bool DxDeviceResources::TryCopyToBackBuffer(ID3D11Texture2D* texture)
    {
        if (!m_swapChain || !m_context || texture == nullptr)
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID3D11Texture2D> backBuffer;
        if (FAILED(m_swapChain->GetBuffer(0, IID_PPV_ARGS(&backBuffer))))
        {
            return false;
        }

        D3D11_TEXTURE2D_DESC sourceDescription{};
        D3D11_TEXTURE2D_DESC targetDescription{};
        texture->GetDesc(&sourceDescription);
        backBuffer->GetDesc(&targetDescription);

        if (sourceDescription.Width != targetDescription.Width ||
            sourceDescription.Height != targetDescription.Height ||
            sourceDescription.Format != targetDescription.Format)
        {
            // NV12/P010 video frames need a video processor or shader path before copy.
            return false;
        }

        m_context->CopyResource(backBuffer.Get(), texture);
        return Present();
    }

    bool DxDeviceResources::TryProcessVideoFrameToBackBuffer(
        ID3D11Texture2D* texture,
        uint32_t arraySlice,
        uint32_t width,
        uint32_t height)
    {
        if (!m_swapChain || !m_device || !m_context || texture == nullptr)
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID3D11Texture2D> backBuffer;
        if (FAILED(m_swapChain->GetBuffer(0, IID_PPV_ARGS(&backBuffer))))
        {
            return false;
        }

        D3D11_TEXTURE2D_DESC sourceDescription{};
        D3D11_TEXTURE2D_DESC targetDescription{};
        texture->GetDesc(&sourceDescription);
        backBuffer->GetDesc(&targetDescription);

        Microsoft::WRL::ComPtr<ID3D11VideoDevice> videoDevice;
        Microsoft::WRL::ComPtr<ID3D11VideoContext> videoContext;
        if (FAILED(m_device.As(&videoDevice)) || FAILED(m_context.As(&videoContext)))
        {
            return false;
        }

        D3D11_VIDEO_PROCESSOR_CONTENT_DESC contentDescription{};
        contentDescription.InputFrameFormat = D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE;
        contentDescription.InputWidth = width == 0 ? sourceDescription.Width : width;
        contentDescription.InputHeight = height == 0 ? sourceDescription.Height : height;
        contentDescription.OutputWidth = targetDescription.Width;
        contentDescription.OutputHeight = targetDescription.Height;
        contentDescription.Usage = D3D11_VIDEO_USAGE_PLAYBACK_NORMAL;

        Microsoft::WRL::ComPtr<ID3D11VideoProcessorEnumerator> enumerator;
        if (FAILED(videoDevice->CreateVideoProcessorEnumerator(
            &contentDescription,
            enumerator.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID3D11VideoProcessor> processor;
        if (FAILED(videoDevice->CreateVideoProcessor(enumerator.Get(), 0, processor.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC inputDescription{};
        inputDescription.ViewDimension = D3D11_VPIV_DIMENSION_TEXTURE2D;
        inputDescription.Texture2D.MipSlice = 0;
        inputDescription.Texture2D.ArraySlice = arraySlice;

        Microsoft::WRL::ComPtr<ID3D11VideoProcessorInputView> inputView;
        if (FAILED(videoDevice->CreateVideoProcessorInputView(
            texture,
            enumerator.Get(),
            &inputDescription,
            inputView.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        D3D11_VIDEO_PROCESSOR_OUTPUT_VIEW_DESC outputDescription{};
        outputDescription.ViewDimension = D3D11_VPOV_DIMENSION_TEXTURE2D;
        outputDescription.Texture2D.MipSlice = 0;

        Microsoft::WRL::ComPtr<ID3D11VideoProcessorOutputView> outputView;
        if (FAILED(videoDevice->CreateVideoProcessorOutputView(
            backBuffer.Get(),
            enumerator.Get(),
            &outputDescription,
            outputView.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        D3D11_VIDEO_PROCESSOR_STREAM stream{};
        stream.Enable = TRUE;
        stream.pInputSurface = inputView.Get();

        if (FAILED(videoContext->VideoProcessorBlt(processor.Get(), outputView.Get(), 0, 1, &stream)))
        {
            return false;
        }

        return Present();
    }

    bool DxDeviceResources::ClearToBlack()
    {
        if (!m_swapChain || !m_context)
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID3D11Texture2D> backBuffer;
        if (FAILED(m_swapChain->GetBuffer(0, IID_PPV_ARGS(&backBuffer))))
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID3D11RenderTargetView> renderTargetView;
        if (FAILED(m_device->CreateRenderTargetView(backBuffer.Get(), nullptr, renderTargetView.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        float clearColor[] = {0.0f, 0.0f, 0.0f, 1.0f};
        m_context->ClearRenderTargetView(renderTargetView.Get(), clearColor);
        return Present();
    }

    bool DxDeviceResources::Present()
    {
        if (!m_swapChain)
        {
            return false;
        }

        return SUCCEEDED(m_swapChain->Present(1, 0));
    }

    ID3D11Device* DxDeviceResources::Device() const noexcept
    {
        return m_device.Get();
    }

    ID3D11DeviceContext* DxDeviceResources::Context() const noexcept
    {
        return m_context.Get();
    }
}
