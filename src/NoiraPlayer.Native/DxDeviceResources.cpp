#include "pch.h"
#include "DxDeviceResources.h"

#include <algorithm>
#include <cmath>
#include <d2d1_1.h>
#include <dwrite.h>
#include <utility>
#include <windows.ui.xaml.media.dxinterop.h>

namespace winrt::NoiraPlayer::Native::implementation
{
    namespace
    {
        constexpr uint32_t DefaultSwapChainWidth = 1280;
        constexpr uint32_t DefaultSwapChainHeight = 720;

        RECT CalculateContainRect(
            uint32_t targetWidth,
            uint32_t targetHeight,
            uint32_t displayWidth,
            uint32_t displayHeight)
        {
            if (targetWidth == 0 || targetHeight == 0 || displayWidth == 0 || displayHeight == 0)
            {
                return RECT{0, 0, static_cast<LONG>(targetWidth), static_cast<LONG>(targetHeight)};
            }

            auto targetAspect = static_cast<double>(targetWidth) / targetHeight;
            auto sourceAspect = static_cast<double>(displayWidth) / displayHeight;
            auto destinationWidth = targetWidth;
            auto destinationHeight = targetHeight;

            if (sourceAspect > targetAspect)
            {
                destinationHeight = static_cast<uint32_t>(
                    (std::max)(1.0, std::round(static_cast<double>(targetWidth) / sourceAspect)));
            }
            else if (sourceAspect < targetAspect)
            {
                destinationWidth = static_cast<uint32_t>(
                    (std::max)(1.0, std::round(static_cast<double>(targetHeight) * sourceAspect)));
            }

            auto left = static_cast<LONG>((targetWidth - destinationWidth) / 2);
            auto top = static_cast<LONG>((targetHeight - destinationHeight) / 2);
            return RECT{
                left,
                top,
                left + static_cast<LONG>(destinationWidth),
                top + static_cast<LONG>(destinationHeight)};
        }

        D2D1_RECT_F ToD2DRect(RECT const& rectangle)
        {
            return D2D1::RectF(
                static_cast<float>(rectangle.left),
                static_cast<float>(rectangle.top),
                static_cast<float>(rectangle.right),
                static_cast<float>(rectangle.bottom));
        }

        float EstimateToneMapLuminance(DXGI_HDR_METADATA_HDR10 const* metadata) noexcept
        {
            constexpr auto defaultLuminance = 400.0f;
            if (metadata == nullptr)
            {
                return defaultLuminance;
            }

            auto masteringLuminance = metadata->MaxMasteringLuminance > 0
                ? static_cast<float>(metadata->MaxMasteringLuminance)
                : defaultLuminance;
            if (metadata->MaxContentLightLevel > 0)
            {
                auto lower = (std::min)(masteringLuminance, static_cast<float>(metadata->MaxContentLightLevel));
                auto upper = (std::max)(masteringLuminance, static_cast<float>(metadata->MaxContentLightLevel));
                auto fall = static_cast<float>(metadata->MaxFrameAverageLightLevel);
                return (lower * 0.5f) + (upper * 0.2f) + (fall * 0.3f);
            }

            return masteringLuminance;
        }

    }

    void DxDeviceResources::AttachSurface(winrt::Windows::UI::Xaml::Controls::SwapChainPanel const& panel)
    {
        if (panel == nullptr)
        {
            throw winrt::hresult_invalid_argument(L"SwapChainPanel is required.");
        }

        m_panel = panel;

        auto scaleX = panel.CompositionScaleX() > 0.0f ? panel.CompositionScaleX() : 1.0f;
        auto scaleY = panel.CompositionScaleY() > 0.0f ? panel.CompositionScaleY() : 1.0f;
        auto width = panel.ActualWidth() > 0.0
            ? static_cast<uint32_t>((std::max)(1.0, std::ceil(panel.ActualWidth() * scaleX)))
            : DefaultSwapChainWidth;
        auto height = panel.ActualHeight() > 0.0
            ? static_cast<uint32_t>((std::max)(1.0, std::ceil(panel.ActualHeight() * scaleY)))
            : DefaultSwapChainHeight;
        CreateSwapChain(width, height, true);
        DXGI_MATRIX_3X2_F inverseScale{};
        inverseScale._11 = 1.0f / scaleX;
        inverseScale._22 = 1.0f / scaleY;
        winrt::check_hresult(m_swapChain->SetMatrixTransform(&inverseScale));

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

        auto createSwapChainWithFormat = [&](DXGI_FORMAT format, Microsoft::WRL::ComPtr<IDXGISwapChain3>& result)
        {
            DXGI_SWAP_CHAIN_DESC1 description{};
            description.Width = width == 0 ? 1 : width;
            description.Height = height == 0 ? 1 : height;
            description.Format = format;
            description.Stereo = false;
            description.SampleDesc.Count = 1;
            description.SampleDesc.Quality = 0;
            description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
            description.BufferCount = 3;
            description.Scaling = DXGI_SCALING_STRETCH;
            description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
            description.AlphaMode = DXGI_ALPHA_MODE_IGNORE;
            description.Flags = 0;

            Microsoft::WRL::ComPtr<IDXGISwapChain1> swapChain;
            auto hr = factory->CreateSwapChainForComposition(
                m_device.Get(),
                &description,
                nullptr,
                swapChain.ReleaseAndGetAddressOf());
            if (FAILED(hr))
            {
                return hr;
            }

            return swapChain.As(&result);
        };

        auto requestedFormat = useTenBit ? DXGI_FORMAT_R10G10B10A2_UNORM : DXGI_FORMAT_B8G8R8A8_UNORM;
        Microsoft::WRL::ComPtr<IDXGISwapChain3> swapChain;
        auto hr = createSwapChainWithFormat(requestedFormat, swapChain);
        auto actualFormat = requestedFormat;
        if (FAILED(hr) && useTenBit)
        {
            actualFormat = DXGI_FORMAT_B8G8R8A8_UNORM;
            hr = createSwapChainWithFormat(actualFormat, swapChain);
        }

        winrt::check_hresult(hr);
        m_swapChain = swapChain;
        m_swapChainFormat = actualFormat;
        m_isTenBitSwapChain = actualFormat == DXGI_FORMAT_R10G10B10A2_UNORM;
        SetSdrColorSpace();
    }

    bool DxDeviceResources::SetHdr10ColorSpace()
    {
        if (!m_swapChain || !m_isTenBitSwapChain)
        {
            return false;
        }

        auto colorSpace = DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020;
        auto hr = m_swapChain->SetColorSpace1(colorSpace);
        if (SUCCEEDED(hr))
        {
            m_swapChainColorSpace = colorSpace;
        }

        return SUCCEEDED(hr);
    }

    bool DxDeviceResources::SetSdrColorSpace()
    {
        if (!m_swapChain)
        {
            return false;
        }

        auto colorSpace = DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709;
        auto hr = m_swapChain->SetColorSpace1(colorSpace);
        if (SUCCEEDED(hr))
        {
            m_swapChainColorSpace = colorSpace;
        }

        return SUCCEEDED(hr);
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
        return true;
    }

    bool DxDeviceResources::TryProcessVideoFrameToBackBuffer(
        ID3D11Texture2D* texture,
        uint32_t arraySlice,
            uint32_t width,
            uint32_t height,
            uint32_t displayWidth,
            uint32_t displayHeight,
            VideoColorMetadata const& colorMetadata,
            bool outputHdr10,
            DXGI_HDR_METADATA_HDR10 const* hdr10Metadata)
    {
        if (!m_swapChain || !m_device || !m_context || texture == nullptr)
        {
            return false;
        }

        m_lastVideoProcessorConversionValidated = false;
        SetVideoProcessorConversionStatus(
            DXGI_COLOR_SPACE_CUSTOM,
            DXGI_COLOR_SPACE_CUSTOM,
            L"pending");

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

        auto mapping = MapVideoColorSpace(colorMetadata, outputHdr10 && m_isTenBitSwapChain);
        if (!mapping.IsSupported)
        {
            SetVideoProcessorConversionStatus(
                mapping.InputColorSpace,
                mapping.OutputColorSpace,
                L"unsupported: " + mapping.Reason);
            return false;
        }

        auto selectedInputColorSpace = mapping.InputColorSpace;
        auto postProcessKind = mapping.PostProcessKind;
        auto requiresPostProcess = postProcessKind != DxgiPostProcessKind::None;
        Microsoft::WRL::ComPtr<ID3D11VideoContext1> videoContext1;
        Microsoft::WRL::ComPtr<ID3D11VideoProcessorEnumerator1> enumerator1;
        auto useDxgiColorSpace = false;
        if (SUCCEEDED(m_context.As(&videoContext1)) && SUCCEEDED(enumerator.As(&enumerator1)))
        {
            BOOL isSupported = FALSE;
            auto conversionResult = enumerator1->CheckVideoProcessorFormatConversion(
                sourceDescription.Format,
                mapping.InputColorSpace,
                targetDescription.Format,
                mapping.OutputColorSpace,
                &isSupported);
            if (FAILED(conversionResult) || !isSupported)
            {
                if (mapping.HasAlternativeInputColorSpace)
                {
                    isSupported = FALSE;
                    conversionResult = enumerator1->CheckVideoProcessorFormatConversion(
                        sourceDescription.Format,
                        mapping.AlternativeInputColorSpace,
                        targetDescription.Format,
                        mapping.OutputColorSpace,
                        &isSupported);
                    if (SUCCEEDED(conversionResult) && isSupported)
                    {
                        selectedInputColorSpace = mapping.AlternativeInputColorSpace;
                    }
                }

                if (FAILED(conversionResult) || !isSupported)
                {
                    SetVideoProcessorConversionStatus(
                        mapping.InputColorSpace,
                        mapping.OutputColorSpace,
                        L"unsupported-conversion");
                    return false;
                }
            }

            useDxgiColorSpace = true;
            m_lastVideoProcessorConversionValidated = true;
            auto validatedStatus = L"validated";
            if (mapping.RequiresToneMapping)
            {
                validatedStatus = L"validated;requires-tone-mapping";
            }
            else if (postProcessKind == DxgiPostProcessKind::HlgToPq)
            {
                validatedStatus = L"validated;requires-hlg-to-pq";
            }

            SetVideoProcessorConversionStatus(
                selectedInputColorSpace,
                mapping.OutputColorSpace,
                validatedStatus);
        }
        else
        {
            if (mapping.NeedsTenBitOutput || outputHdr10 || mapping.IsHdr10 || mapping.IsHlg)
            {
                SetVideoProcessorConversionStatus(
                    mapping.InputColorSpace,
                    mapping.OutputColorSpace,
                    L"unvalidated-hdr-rejected");
                return false;
            }

            SetVideoProcessorConversionStatus(
                mapping.InputColorSpace,
                mapping.OutputColorSpace,
                L"legacy-unvalidated");
        }

        auto sourceRect = RECT{
            0,
            0,
            static_cast<LONG>(contentDescription.InputWidth),
            static_cast<LONG>(contentDescription.InputHeight)};
        auto outputRect = RECT{
            0,
            0,
            static_cast<LONG>(targetDescription.Width),
            static_cast<LONG>(targetDescription.Height)};
        auto destinationRect = CalculateContainRect(
            targetDescription.Width,
            targetDescription.Height,
            displayWidth == 0 ? contentDescription.InputWidth : displayWidth,
            displayHeight == 0 ? contentDescription.InputHeight : displayHeight);
        videoContext->VideoProcessorSetStreamSourceRect(processor.Get(), 0, TRUE, &sourceRect);
        videoContext->VideoProcessorSetStreamDestRect(processor.Get(), 0, TRUE, &destinationRect);
        videoContext->VideoProcessorSetOutputTargetRect(processor.Get(), TRUE, &outputRect);
        if (useDxgiColorSpace)
        {
            videoContext1->VideoProcessorSetStreamColorSpace1(processor.Get(), 0, selectedInputColorSpace);
            videoContext1->VideoProcessorSetOutputColorSpace1(processor.Get(), mapping.OutputColorSpace);
        }
        else
        {
            videoContext->VideoProcessorSetStreamColorSpace(processor.Get(), 0, &mapping.LegacyInputColorSpace);
            videoContext->VideoProcessorSetOutputColorSpace(processor.Get(), &mapping.LegacyOutputColorSpace);
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

        Microsoft::WRL::ComPtr<ID3D11Texture2D> videoProcessorTarget = backBuffer;
        if (requiresPostProcess)
        {
            D3D11_TEXTURE2D_DESC intermediateDescription = targetDescription;
            intermediateDescription.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
            intermediateDescription.CPUAccessFlags = 0;
            intermediateDescription.MiscFlags = 0;
            intermediateDescription.Usage = D3D11_USAGE_DEFAULT;
            intermediateDescription.MipLevels = 1;
            intermediateDescription.ArraySize = 1;
            if (FAILED(m_device->CreateTexture2D(
                    &intermediateDescription,
                    nullptr,
                    videoProcessorTarget.ReleaseAndGetAddressOf())))
            {
                SetVideoProcessorConversionStatus(
                    selectedInputColorSpace,
                    mapping.OutputColorSpace,
                    postProcessKind == DxgiPostProcessKind::HlgToPq
                        ? L"validated;requires-hlg-to-pq;shader-target-failed"
                        : L"validated;requires-tone-mapping;shader-target-failed");
                return false;
            }
        }

        Microsoft::WRL::ComPtr<ID3D11VideoProcessorOutputView> outputView;
        if (FAILED(videoDevice->CreateVideoProcessorOutputView(
            videoProcessorTarget.Get(),
            enumerator.Get(),
            &outputDescription,
            outputView.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        D3D11_VIDEO_PROCESSOR_STREAM stream{};
        stream.Enable = TRUE;
        stream.pInputSurface = inputView.Get();

        if (!ClearTextureToBlack(videoProcessorTarget.Get()))
        {
            return false;
        }

        if (FAILED(videoContext->VideoProcessorBlt(processor.Get(), outputView.Get(), 0, 1, &stream)))
        {
            return false;
        }

        if (requiresPostProcess)
        {
            auto luminance = EstimateToneMapLuminance(hdr10Metadata);
            auto mode = postProcessKind == DxgiPostProcessKind::HlgToPq
                ? HdrToneMappingMode::HlgToPq
                : HdrToneMappingMode::PqToSdrHable;
            if (!m_hdrToneMappingPass.Render(
                    m_device.Get(),
                    m_context.Get(),
                    videoProcessorTarget.Get(),
                    backBuffer.Get(),
                    luminance,
                    mode))
            {
                SetVideoProcessorConversionStatus(
                    selectedInputColorSpace,
                    mapping.OutputColorSpace,
                    postProcessKind == DxgiPostProcessKind::HlgToPq
                        ? L"validated;requires-hlg-to-pq;shader-failed"
                        : L"validated;requires-tone-mapping;shader-failed");
                return false;
            }

            SetVideoProcessorConversionStatus(
                selectedInputColorSpace,
                mapping.OutputColorSpace,
                postProcessKind == DxgiPostProcessKind::HlgToPq
                    ? L"validated;hlg-to-pq"
                    : L"validated;tone-mapped-hable");
        }

        return true;
    }

    bool DxDeviceResources::DrawBgraFrameToBackBuffer(
        uint8_t const* pixels,
        uint32_t width,
        uint32_t height,
        uint32_t displayWidth,
        uint32_t displayHeight,
        uint32_t stride)
    {
        if (pixels == nullptr || width == 0 || height == 0 || stride == 0 || !m_swapChain)
        {
            return false;
        }

        Microsoft::WRL::ComPtr<IDXGISurface> surface;
        if (FAILED(m_swapChain->GetBuffer(0, IID_PPV_ARGS(&surface))))
        {
            return false;
        }

        D2D1_FACTORY_OPTIONS factoryOptions{};
        Microsoft::WRL::ComPtr<ID2D1Factory> d2dFactory;
        if (FAILED(D2D1CreateFactory(
            D2D1_FACTORY_TYPE_SINGLE_THREADED,
            __uuidof(ID2D1Factory),
            &factoryOptions,
            reinterpret_cast<void**>(d2dFactory.ReleaseAndGetAddressOf()))))
        {
            return false;
        }

        D2D1_RENDER_TARGET_PROPERTIES renderTargetProperties = D2D1::RenderTargetProperties(
            D2D1_RENDER_TARGET_TYPE_DEFAULT,
            D2D1::PixelFormat(DXGI_FORMAT_UNKNOWN, D2D1_ALPHA_MODE_IGNORE));

        Microsoft::WRL::ComPtr<ID2D1RenderTarget> renderTarget;
        if (FAILED(d2dFactory->CreateDxgiSurfaceRenderTarget(
            surface.Get(),
            &renderTargetProperties,
            renderTarget.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        D2D1_BITMAP_PROPERTIES bitmapProperties = D2D1::BitmapProperties(
            D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_IGNORE));

        Microsoft::WRL::ComPtr<ID2D1Bitmap> bitmap;
        if (FAILED(renderTarget->CreateBitmap(
            D2D1::SizeU(width, height),
            pixels,
            stride,
            &bitmapProperties,
            bitmap.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        auto targetSize = renderTarget->GetSize();
        auto targetWidth = static_cast<uint32_t>((std::max)(1.0f, std::round(targetSize.width)));
        auto targetHeight = static_cast<uint32_t>((std::max)(1.0f, std::round(targetSize.height)));
        auto destinationRect = CalculateContainRect(
            targetWidth,
            targetHeight,
            displayWidth == 0 ? width : displayWidth,
            displayHeight == 0 ? height : displayHeight);

        renderTarget->BeginDraw();
        renderTarget->Clear(D2D1::ColorF(D2D1::ColorF::Black));
        renderTarget->DrawBitmap(
            bitmap.Get(),
            ToD2DRect(destinationRect),
            1.0f,
            D2D1_BITMAP_INTERPOLATION_MODE_LINEAR);

        return SUCCEEDED(renderTarget->EndDraw());
    }

    bool DxDeviceResources::DrawTextOverlay(std::wstring const& text)
    {
        if (text.empty() || !m_swapChain || !m_device)
        {
            return false;
        }

        DXGI_SWAP_CHAIN_DESC1 swapChainDescription{};
        if (FAILED(m_swapChain->GetDesc1(&swapChainDescription)))
        {
            return false;
        }

        Microsoft::WRL::ComPtr<IDXGISurface> surface;
        if (FAILED(m_swapChain->GetBuffer(0, IID_PPV_ARGS(&surface))))
        {
            return false;
        }

        D2D1_FACTORY_OPTIONS factoryOptions{};
        Microsoft::WRL::ComPtr<ID2D1Factory1> d2dFactory;
        if (FAILED(D2D1CreateFactory(
            D2D1_FACTORY_TYPE_SINGLE_THREADED,
            factoryOptions,
            d2dFactory.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        Microsoft::WRL::ComPtr<IDXGIDevice> dxgiDevice;
        if (FAILED(m_device.As(&dxgiDevice)))
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID2D1Device> d2dDevice;
        if (FAILED(d2dFactory->CreateDevice(dxgiDevice.Get(), d2dDevice.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID2D1DeviceContext> d2dContext;
        if (FAILED(d2dDevice->CreateDeviceContext(
            D2D1_DEVICE_CONTEXT_OPTIONS_NONE,
            d2dContext.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        D2D1_BITMAP_PROPERTIES1 bitmapProperties{};
        bitmapProperties.pixelFormat = D2D1::PixelFormat(
            swapChainDescription.Format,
            D2D1_ALPHA_MODE_IGNORE);
        bitmapProperties.dpiX = 96.0f;
        bitmapProperties.dpiY = 96.0f;
        bitmapProperties.bitmapOptions =
            D2D1_BITMAP_OPTIONS_TARGET |
            D2D1_BITMAP_OPTIONS_CANNOT_DRAW;

        Microsoft::WRL::ComPtr<ID2D1Bitmap1> targetBitmap;
        if (FAILED(d2dContext->CreateBitmapFromDxgiSurface(
            surface.Get(),
            &bitmapProperties,
            targetBitmap.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        Microsoft::WRL::ComPtr<IDWriteFactory> writeFactory;
        if (FAILED(DWriteCreateFactory(
            DWRITE_FACTORY_TYPE_SHARED,
            __uuidof(IDWriteFactory),
            reinterpret_cast<IUnknown**>(writeFactory.ReleaseAndGetAddressOf()))))
        {
            return false;
        }

        auto width = static_cast<float>(swapChainDescription.Width);
        auto height = static_cast<float>(swapChainDescription.Height);
        auto fontSize = std::clamp(height / 18.0f, 28.0f, 56.0f);

        Microsoft::WRL::ComPtr<IDWriteTextFormat> textFormat;
        if (FAILED(writeFactory->CreateTextFormat(
            L"Segoe UI",
            nullptr,
            DWRITE_FONT_WEIGHT_SEMI_BOLD,
            DWRITE_FONT_STYLE_NORMAL,
            DWRITE_FONT_STRETCH_NORMAL,
            fontSize,
            L"zh-CN",
            textFormat.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        (void)textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
        (void)textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_FAR);

        Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> shadowBrush;
        Microsoft::WRL::ComPtr<ID2D1SolidColorBrush> textBrush;
        if (FAILED(d2dContext->CreateSolidColorBrush(
                D2D1::ColorF(D2D1::ColorF::Black, 0.85f),
                shadowBrush.ReleaseAndGetAddressOf())) ||
            FAILED(d2dContext->CreateSolidColorBrush(
                D2D1::ColorF(D2D1::ColorF::White, 1.0f),
                textBrush.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        auto horizontalPadding = width * 0.08f;
        auto bottomPadding = height * 0.08f;
        auto layout = D2D1::RectF(
            horizontalPadding,
            height * 0.62f,
            width - horizontalPadding,
            height - bottomPadding);
        auto shadowLayout = layout;
        shadowLayout.left += 2.0f;
        shadowLayout.right += 2.0f;
        shadowLayout.top += 2.0f;
        shadowLayout.bottom += 2.0f;

        d2dContext->SetTarget(targetBitmap.Get());
        d2dContext->BeginDraw();
        d2dContext->DrawText(
            text.c_str(),
            static_cast<UINT32>(text.size()),
            textFormat.Get(),
            shadowLayout,
            shadowBrush.Get());
        d2dContext->DrawText(
            text.c_str(),
            static_cast<UINT32>(text.size()),
            textFormat.Get(),
            layout,
            textBrush.Get());

        return SUCCEEDED(d2dContext->EndDraw());
    }

    bool DxDeviceResources::DrawSubtitleBitmapOverlay(SubtitleBitmapRegion const& region)
    {
        if (region.BgraPixels.empty() ||
            region.Width == 0 ||
            region.Height == 0 ||
            region.Stride < region.Width * 4 ||
            !m_swapChain ||
            !m_device)
        {
            return false;
        }

        DXGI_SWAP_CHAIN_DESC1 swapChainDescription{};
        if (FAILED(m_swapChain->GetDesc1(&swapChainDescription)))
        {
            return false;
        }

        Microsoft::WRL::ComPtr<IDXGISurface> surface;
        if (FAILED(m_swapChain->GetBuffer(0, IID_PPV_ARGS(&surface))))
        {
            return false;
        }

        D2D1_FACTORY_OPTIONS factoryOptions{};
        Microsoft::WRL::ComPtr<ID2D1Factory1> d2dFactory;
        if (FAILED(D2D1CreateFactory(
            D2D1_FACTORY_TYPE_SINGLE_THREADED,
            factoryOptions,
            d2dFactory.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        Microsoft::WRL::ComPtr<IDXGIDevice> dxgiDevice;
        if (FAILED(m_device.As(&dxgiDevice)))
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID2D1Device> d2dDevice;
        if (FAILED(d2dFactory->CreateDevice(dxgiDevice.Get(), d2dDevice.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID2D1DeviceContext> d2dContext;
        if (FAILED(d2dDevice->CreateDeviceContext(
            D2D1_DEVICE_CONTEXT_OPTIONS_NONE,
            d2dContext.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        D2D1_BITMAP_PROPERTIES1 targetProperties{};
        targetProperties.pixelFormat = D2D1::PixelFormat(
            swapChainDescription.Format,
            D2D1_ALPHA_MODE_IGNORE);
        targetProperties.dpiX = 96.0f;
        targetProperties.dpiY = 96.0f;
        targetProperties.bitmapOptions =
            D2D1_BITMAP_OPTIONS_TARGET |
            D2D1_BITMAP_OPTIONS_CANNOT_DRAW;

        Microsoft::WRL::ComPtr<ID2D1Bitmap1> targetBitmap;
        if (FAILED(d2dContext->CreateBitmapFromDxgiSurface(
            surface.Get(),
            &targetProperties,
            targetBitmap.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        D2D1_BITMAP_PROPERTIES1 sourceProperties{};
        sourceProperties.pixelFormat = D2D1::PixelFormat(
            DXGI_FORMAT_B8G8R8A8_UNORM,
            D2D1_ALPHA_MODE_PREMULTIPLIED);
        sourceProperties.dpiX = 96.0f;
        sourceProperties.dpiY = 96.0f;

        Microsoft::WRL::ComPtr<ID2D1Bitmap1> sourceBitmap;
        if (FAILED(d2dContext->CreateBitmap(
            D2D1::SizeU(region.Width, region.Height),
            region.BgraPixels.data(),
            region.Stride,
            &sourceProperties,
            sourceBitmap.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        auto destination = MapSubtitleRegionToContainedVideo(
            region,
            swapChainDescription.Width,
            swapChainDescription.Height);
        d2dContext->SetTarget(targetBitmap.Get());
        d2dContext->BeginDraw();
        d2dContext->DrawBitmap(
            sourceBitmap.Get(),
            D2D1::RectF(destination.Left, destination.Top, destination.Right, destination.Bottom),
            1.0f,
            D2D1_BITMAP_INTERPOLATION_MODE_LINEAR);
        return SUCCEEDED(d2dContext->EndDraw());
    }

    bool DxDeviceResources::ClearToBlack()
    {
        return ClearBackBufferToBlack(true);
    }

    bool DxDeviceResources::ClearBackBufferToBlack(bool present)
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
        return present ? Present() : true;
    }

    bool DxDeviceResources::ClearTextureToBlack(ID3D11Texture2D* texture)
    {
        if (!m_device || !m_context || texture == nullptr)
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID3D11RenderTargetView> renderTargetView;
        if (FAILED(m_device->CreateRenderTargetView(texture, nullptr, renderTargetView.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        float clearColor[] = {0.0f, 0.0f, 0.0f, 1.0f};
        m_context->ClearRenderTargetView(renderTargetView.Get(), clearColor);
        return true;
    }

    bool DxDeviceResources::Present()
    {
        if (!m_swapChain)
        {
            return false;
        }

        return SUCCEEDED(m_swapChain->Present(1, 0));
    }

    void DxDeviceResources::ObserveVideoColorMapping(
        VideoColorMetadata const& colorMetadata,
        bool outputHdr10)
    {
        auto mapping = MapVideoColorSpace(colorMetadata, outputHdr10);
        SetVideoProcessorConversionStatus(
            mapping.InputColorSpace,
            mapping.OutputColorSpace,
            mapping.IsSupported
                ? L"mapped-without-video-processor"
                : L"unsupported-mapping");
    }

    bool DxDeviceResources::HasRenderTarget() const noexcept
    {
        return m_swapChain != nullptr;
    }

    ID3D11Device* DxDeviceResources::Device() const noexcept
    {
        return m_device.Get();
    }

    ID3D11DeviceContext* DxDeviceResources::Context() const noexcept
    {
        return m_context.Get();
    }

    DXGI_FORMAT DxDeviceResources::SwapChainFormat() const noexcept
    {
        return m_swapChainFormat;
    }

    DXGI_COLOR_SPACE_TYPE DxDeviceResources::SwapChainColorSpace() const noexcept
    {
        return m_swapChainColorSpace;
    }

    bool DxDeviceResources::IsTenBitSwapChain() const noexcept
    {
        return m_isTenBitSwapChain;
    }

    bool DxDeviceResources::LastVideoProcessorConversionWasValidated() const noexcept
    {
        return m_lastVideoProcessorConversionValidated;
    }

    DXGI_COLOR_SPACE_TYPE DxDeviceResources::LastVideoProcessorInputColorSpace() const noexcept
    {
        return m_lastVideoProcessorInputColorSpace;
    }

    DXGI_COLOR_SPACE_TYPE DxDeviceResources::LastVideoProcessorOutputColorSpace() const noexcept
    {
        return m_lastVideoProcessorOutputColorSpace;
    }

    std::wstring DxDeviceResources::LastVideoProcessorConversionStatus() const
    {
        return m_lastVideoProcessorConversionStatus;
    }

    void DxDeviceResources::SetVideoProcessorConversionStatus(
        DXGI_COLOR_SPACE_TYPE inputColorSpace,
        DXGI_COLOR_SPACE_TYPE outputColorSpace,
        std::wstring status)
    {
        m_lastVideoProcessorInputColorSpace = inputColorSpace;
        m_lastVideoProcessorOutputColorSpace = outputColorSpace;
        m_lastVideoProcessorConversionStatus = std::move(status);
    }
}
