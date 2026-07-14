#include "pch.h"
#include "DxDeviceResources.h"
#include "NativePlaybackDiagnostics.h"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <d2d1_1.h>
#include <d3dcompiler.h>
#include <dwrite.h>
#include <cstring>
#include <utility>
#include <windows.ui.xaml.media.dxinterop.h>

namespace winrt::NoiraPlayer::Native::implementation
{
    namespace
    {
        constexpr uint32_t DefaultSwapChainWidth = 1280;
        constexpr uint32_t DefaultSwapChainHeight = 720;
        constexpr float SubtitleSdrWhiteNits = 203.0f;

        char const SubtitleOverlayShader[] = R"(
Texture2D SubtitleTexture : register(t0);
SamplerState LinearSampler : register(s0);

cbuffer SubtitleConstants : register(b0)
{
    uint g_hdrPqOutput;
    float g_sdrWhiteNits;
    float2 g_padding;
};

struct VertexOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

VertexOutput VSMain(uint vertexId : SV_VertexID)
{
    float2 positions[3] =
    {
        float2(-1.0f, -1.0f),
        float2(-1.0f,  3.0f),
        float2( 3.0f, -1.0f)
    };

    VertexOutput output;
    float2 position = positions[vertexId];
    output.Position = float4(position, 0.0f, 1.0f);
    output.TexCoord = float2(
        (position.x + 1.0f) * 0.5f,
        1.0f - ((position.y + 1.0f) * 0.5f));
    return output;
}

float3 TransferPQ(float3 nits)
{
    static const float m1 = 2610.0f / (4096.0f * 4.0f);
    static const float m2 = (2523.0f / 4096.0f) * 128.0f;
    static const float c1 = 3424.0f / 4096.0f;
    static const float c2 = (2413.0f / 4096.0f) * 32.0f;
    static const float c3 = (2392.0f / 4096.0f) * 32.0f;
    float3 value = pow(max(nits, 0.0f) / 10000.0f, m1);
    return pow((c1 + c2 * value) / (1.0f + c3 * value), m2);
}

float4 PSMain(VertexOutput input) : SV_TARGET
{
    float4 color = SubtitleTexture.Sample(LinearSampler, input.TexCoord);
    if (g_hdrPqOutput != 0 && color.a > 0.0001f)
    {
        float3 straight = saturate(color.rgb / color.a);
        float3 linearNits = pow(straight, 2.2f) * g_sdrWhiteNits;
        color.rgb = TransferPQ(linearNits) * color.a;
    }
    return color;
}
)";

        struct SubtitleOverlayConstants
        {
            uint32_t HdrPqOutput;
            float SdrWhiteNits;
            float Padding[2];
        };

        uint64_t HashSubtitleBitmap(SubtitleBitmapRegion const& region) noexcept
        {
            constexpr uint64_t offset = 1469598103934665603ull;
            constexpr uint64_t prime = 1099511628211ull;
            auto hash = offset;
            auto append = [&](uint8_t value)
            {
                hash ^= value;
                hash *= prime;
            };
            for (auto value : region.BgraPixels)
            {
                append(value);
            }
            for (auto value : {region.Width, region.Height, region.Stride})
            {
                append(static_cast<uint8_t>(value));
                append(static_cast<uint8_t>(value >> 8));
                append(static_cast<uint8_t>(value >> 16));
                append(static_cast<uint8_t>(value >> 24));
            }
            return hash;
        }

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

        Microsoft::WRL::ComPtr<ID3D11Multithread> multithread;
        winrt::check_hresult(m_context.As(&multithread));
        multithread->SetMultithreadProtected(TRUE);
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
            DXGI_HDR_METADATA_HDR10 const* hdr10Metadata,
            VideoRenderPhaseSample* phaseSample)
    {
        if (phaseSample != nullptr)
        {
            *phaseSample = {};
        }

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

        VideoRenderPhaseSample completedSample{};
        completedSample.Path = VideoRenderPath::VideoProcessor;
        auto const setupStartedAt = std::chrono::steady_clock::now();

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

        completedSample.ProcessorSetupCpuMs = std::chrono::duration<double, std::milli>(
            std::chrono::steady_clock::now() - setupStartedAt).count();
        auto const viewTargetStartedAt = std::chrono::steady_clock::now();

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

        completedSample.ViewTargetCpuMs = std::chrono::duration<double, std::milli>(
            std::chrono::steady_clock::now() - viewTargetStartedAt).count();

        D3D11_VIDEO_PROCESSOR_STREAM stream{};
        stream.Enable = TRUE;
        stream.pInputSurface = inputView.Get();

        auto const clearStartedAt = std::chrono::steady_clock::now();
        auto const cleared = ClearTextureToBlack(videoProcessorTarget.Get());
        completedSample.ClearCpuMs = std::chrono::duration<double, std::milli>(
            std::chrono::steady_clock::now() - clearStartedAt).count();
        if (!cleared)
        {
            return false;
        }

        auto const bltStartedAt = std::chrono::steady_clock::now();
        auto const bltResult = videoContext->VideoProcessorBlt(
            processor.Get(),
            outputView.Get(),
            0,
            1,
            &stream);
        completedSample.BltCpuMs = std::chrono::duration<double, std::milli>(
            std::chrono::steady_clock::now() - bltStartedAt).count();
        if (FAILED(bltResult))
        {
            return false;
        }

        if (!requiresPostProcess)
        {
            SetVideoProcessorConversionStatus(
                selectedInputColorSpace,
                mapping.OutputColorSpace,
                L"validated");
        }

        if (requiresPostProcess)
        {
            auto luminance = EstimateToneMapLuminance(hdr10Metadata);
            auto mode = postProcessKind == DxgiPostProcessKind::HlgToPq
                ? HdrToneMappingMode::HlgToPq
                : HdrToneMappingMode::PqToSdrHable;
            auto const postProcessStartedAt = std::chrono::steady_clock::now();
            auto const postProcessed = m_hdrToneMappingPass.Render(
                    m_device.Get(),
                    m_context.Get(),
                    videoProcessorTarget.Get(),
                    backBuffer.Get(),
                    luminance,
                    mode);
            completedSample.PostProcessCpuMs = std::chrono::duration<double, std::milli>(
                std::chrono::steady_clock::now() - postProcessStartedAt).count();
            if (!postProcessed)
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
            completedSample.PostProcessed = true;
        }

        if (phaseSample != nullptr)
        {
            *phaseSample = completedSample;
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

    bool DxDeviceResources::EnsureSubtitleOverlayResources()
    {
        if (m_subtitleVertexShader &&
            m_subtitlePixelShader &&
            m_subtitleSampler &&
            m_subtitleBlendState &&
            m_subtitleConstants)
        {
            return true;
        }

        if (!m_device)
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID3DBlob> vertexBlob;
        Microsoft::WRL::ComPtr<ID3DBlob> pixelBlob;
        Microsoft::WRL::ComPtr<ID3DBlob> errors;
        auto compile = [&](char const* entry, char const* profile, ID3DBlob** output)
        {
            errors.Reset();
            auto result = D3DCompile(
                SubtitleOverlayShader,
                std::strlen(SubtitleOverlayShader),
                "SubtitleOverlayShader",
                nullptr,
                nullptr,
                entry,
                profile,
                D3DCOMPILE_ENABLE_STRICTNESS,
                0,
                output,
                errors.ReleaseAndGetAddressOf());
            if (FAILED(result))
            {
                AppendNativePlaybackDiagnostic(
                    L"DxDeviceResources subtitle shader compile failed hr=" +
                    std::to_wstring(static_cast<unsigned long>(result)));
            }
            return SUCCEEDED(result);
        };

        if (!compile("VSMain", "vs_4_0", vertexBlob.ReleaseAndGetAddressOf()) ||
            !compile("PSMain", "ps_4_0", pixelBlob.ReleaseAndGetAddressOf()))
        {
            return false;
        }

        auto result = m_device->CreateVertexShader(
            vertexBlob->GetBufferPointer(),
            vertexBlob->GetBufferSize(),
            nullptr,
            m_subtitleVertexShader.ReleaseAndGetAddressOf());
        if (FAILED(result))
        {
            return false;
        }

        result = m_device->CreatePixelShader(
            pixelBlob->GetBufferPointer(),
            pixelBlob->GetBufferSize(),
            nullptr,
            m_subtitlePixelShader.ReleaseAndGetAddressOf());
        if (FAILED(result))
        {
            return false;
        }

        D3D11_SAMPLER_DESC samplerDescription{};
        samplerDescription.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
        samplerDescription.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDescription.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDescription.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDescription.MaxLOD = D3D11_FLOAT32_MAX;
        result = m_device->CreateSamplerState(
            &samplerDescription,
            m_subtitleSampler.ReleaseAndGetAddressOf());
        if (FAILED(result))
        {
            return false;
        }

        D3D11_BLEND_DESC blendDescription{};
        blendDescription.RenderTarget[0].BlendEnable = true;
        blendDescription.RenderTarget[0].SrcBlend = D3D11_BLEND_ONE;
        blendDescription.RenderTarget[0].DestBlend = D3D11_BLEND_INV_SRC_ALPHA;
        blendDescription.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
        blendDescription.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
        blendDescription.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA;
        blendDescription.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
        blendDescription.RenderTarget[0].RenderTargetWriteMask =
            D3D11_COLOR_WRITE_ENABLE_ALL;
        result = m_device->CreateBlendState(
            &blendDescription,
            m_subtitleBlendState.ReleaseAndGetAddressOf());
        if (FAILED(result))
        {
            return false;
        }

        D3D11_BUFFER_DESC constantsDescription{};
        constantsDescription.ByteWidth = sizeof(SubtitleOverlayConstants);
        constantsDescription.Usage = D3D11_USAGE_DEFAULT;
        constantsDescription.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
        return SUCCEEDED(m_device->CreateBuffer(
            &constantsDescription,
            nullptr,
            m_subtitleConstants.ReleaseAndGetAddressOf()));
    }

    bool DxDeviceResources::DrawSubtitleBitmapOverlayD3d11(
        SubtitleBitmapRegion const& region)
    {
        if (!EnsureSubtitleOverlayResources() || !m_context || !m_swapChain)
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID3D11Texture2D> backBuffer;
        if (FAILED(m_swapChain->GetBuffer(0, IID_PPV_ARGS(&backBuffer))))
        {
            return false;
        }

        auto subtitleHash = HashSubtitleBitmap(region);
        if (!m_cachedSubtitleView ||
            subtitleHash != m_cachedSubtitleHash ||
            region.Width != m_cachedSubtitleWidth ||
            region.Height != m_cachedSubtitleHeight ||
            region.Stride != m_cachedSubtitleStride)
        {
            D3D11_TEXTURE2D_DESC textureDescription{};
            textureDescription.Width = region.Width;
            textureDescription.Height = region.Height;
            textureDescription.MipLevels = 1;
            textureDescription.ArraySize = 1;
            textureDescription.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            textureDescription.SampleDesc.Count = 1;
            textureDescription.Usage = D3D11_USAGE_IMMUTABLE;
            textureDescription.BindFlags = D3D11_BIND_SHADER_RESOURCE;
            D3D11_SUBRESOURCE_DATA textureData{};
            textureData.pSysMem = region.BgraPixels.data();
            textureData.SysMemPitch = region.Stride;

            Microsoft::WRL::ComPtr<ID3D11Texture2D> subtitleTexture;
            if (FAILED(m_device->CreateTexture2D(
                &textureDescription,
                &textureData,
                subtitleTexture.ReleaseAndGetAddressOf())))
            {
                return false;
            }

            m_cachedSubtitleView.Reset();
            if (FAILED(m_device->CreateShaderResourceView(
                subtitleTexture.Get(),
                nullptr,
                m_cachedSubtitleView.ReleaseAndGetAddressOf())))
            {
                return false;
            }
            m_cachedSubtitleHash = subtitleHash;
            m_cachedSubtitleWidth = region.Width;
            m_cachedSubtitleHeight = region.Height;
            m_cachedSubtitleStride = region.Stride;
        }

        Microsoft::WRL::ComPtr<ID3D11RenderTargetView> targetView;
        if (FAILED(m_device->CreateRenderTargetView(
                backBuffer.Get(),
                nullptr,
                targetView.ReleaseAndGetAddressOf())))
        {
            return false;
        }

        DXGI_SWAP_CHAIN_DESC1 swapChainDescription{};
        if (FAILED(m_swapChain->GetDesc1(&swapChainDescription)))
        {
            return false;
        }

        auto destination = MapSubtitleRegionToContainedVideo(
            region,
            swapChainDescription.Width,
            swapChainDescription.Height);
        D3D11_VIEWPORT viewport{};
        viewport.TopLeftX = destination.Left;
        viewport.TopLeftY = destination.Top;
        viewport.Width = (std::max)(1.0f, destination.Right - destination.Left);
        viewport.Height = (std::max)(1.0f, destination.Bottom - destination.Top);
        viewport.MinDepth = 0.0f;
        viewport.MaxDepth = 1.0f;

        auto hdrPqOutput =
            m_swapChainColorSpace == DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020;
        SubtitleOverlayConstants constants{};
        constants.HdrPqOutput = hdrPqOutput ? 1u : 0u;
        constants.SdrWhiteNits = SubtitleSdrWhiteNits;
        m_context->UpdateSubresource(
            m_subtitleConstants.Get(),
            0,
            nullptr,
            &constants,
            0,
            0);

        Microsoft::WRL::ComPtr<ID3D11RenderTargetView> previousTarget;
        Microsoft::WRL::ComPtr<ID3D11DepthStencilView> previousDepth;
        m_context->OMGetRenderTargets(
            1,
            previousTarget.ReleaseAndGetAddressOf(),
            previousDepth.ReleaseAndGetAddressOf());
        Microsoft::WRL::ComPtr<ID3D11BlendState> previousBlend;
        float previousBlendFactor[4]{};
        UINT previousSampleMask = 0;
        m_context->OMGetBlendState(
            previousBlend.ReleaseAndGetAddressOf(),
            previousBlendFactor,
            &previousSampleMask);
        D3D11_VIEWPORT previousViewports[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{};
        UINT previousViewportCount = ARRAYSIZE(previousViewports);
        m_context->RSGetViewports(&previousViewportCount, previousViewports);
        Microsoft::WRL::ComPtr<ID3D11VertexShader> previousVertexShader;
        Microsoft::WRL::ComPtr<ID3D11PixelShader> previousPixelShader;
        Microsoft::WRL::ComPtr<ID3D11InputLayout> previousInputLayout;
        Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> previousSourceView;
        Microsoft::WRL::ComPtr<ID3D11SamplerState> previousSampler;
        Microsoft::WRL::ComPtr<ID3D11Buffer> previousConstantBuffer;
        m_context->VSGetShader(previousVertexShader.ReleaseAndGetAddressOf(), nullptr, nullptr);
        m_context->PSGetShader(previousPixelShader.ReleaseAndGetAddressOf(), nullptr, nullptr);
        m_context->IAGetInputLayout(previousInputLayout.ReleaseAndGetAddressOf());
        m_context->PSGetShaderResources(0, 1, previousSourceView.ReleaseAndGetAddressOf());
        m_context->PSGetSamplers(0, 1, previousSampler.ReleaseAndGetAddressOf());
        m_context->PSGetConstantBuffers(0, 1, previousConstantBuffer.ReleaseAndGetAddressOf());
        D3D11_PRIMITIVE_TOPOLOGY previousTopology{};
        m_context->IAGetPrimitiveTopology(&previousTopology);

        ID3D11RenderTargetView* target = targetView.Get();
        ID3D11ShaderResourceView* source = m_cachedSubtitleView.Get();
        ID3D11SamplerState* sampler = m_subtitleSampler.Get();
        ID3D11Buffer* constantBuffer = m_subtitleConstants.Get();
        float blendFactor[4]{};
        m_context->OMSetRenderTargets(1, &target, nullptr);
        m_context->OMSetBlendState(
            m_subtitleBlendState.Get(),
            blendFactor,
            0xffffffff);
        m_context->RSSetViewports(1, &viewport);
        m_context->IASetInputLayout(nullptr);
        m_context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        m_context->VSSetShader(m_subtitleVertexShader.Get(), nullptr, 0);
        m_context->PSSetShader(m_subtitlePixelShader.Get(), nullptr, 0);
        m_context->PSSetShaderResources(0, 1, &source);
        m_context->PSSetSamplers(0, 1, &sampler);
        m_context->PSSetConstantBuffers(0, 1, &constantBuffer);
        m_context->Draw(3, 0);

        ID3D11ShaderResourceView* nullView = nullptr;
        m_context->PSSetShaderResources(0, 1, &nullView);
        ID3D11RenderTargetView* oldTarget = previousTarget.Get();
        m_context->OMSetRenderTargets(1, &oldTarget, previousDepth.Get());
        m_context->OMSetBlendState(
            previousBlend.Get(),
            previousBlendFactor,
            previousSampleMask);
        if (previousViewportCount > 0)
        {
            m_context->RSSetViewports(previousViewportCount, previousViewports);
        }
        m_context->VSSetShader(previousVertexShader.Get(), nullptr, 0);
        m_context->PSSetShader(previousPixelShader.Get(), nullptr, 0);
        ID3D11ShaderResourceView* oldSourceView = previousSourceView.Get();
        ID3D11SamplerState* oldSampler = previousSampler.Get();
        ID3D11Buffer* oldConstantBuffer = previousConstantBuffer.Get();
        m_context->PSSetShaderResources(0, 1, &oldSourceView);
        m_context->PSSetSamplers(0, 1, &oldSampler);
        m_context->PSSetConstantBuffers(0, 1, &oldConstantBuffer);
        m_context->IASetInputLayout(previousInputLayout.Get());
        m_context->IASetPrimitiveTopology(previousTopology);
        return true;
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

        if (m_isTenBitSwapChain)
        {
            return DrawSubtitleBitmapOverlayD3d11(region);
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
        auto targetResult = d2dContext->CreateBitmapFromDxgiSurface(
            surface.Get(),
            &targetProperties,
            targetBitmap.ReleaseAndGetAddressOf());
        if (FAILED(targetResult))
        {
            AppendNativePlaybackDiagnostic(
                L"DxDeviceResources.DrawSubtitleBitmapOverlay target creation failed format=" +
                std::to_wstring(static_cast<int>(swapChainDescription.Format)) +
                L" hr=" +
                std::to_wstring(static_cast<unsigned long>(targetResult)));
            return false;
        }

        D2D1_BITMAP_PROPERTIES1 sourceProperties{};
        sourceProperties.pixelFormat = D2D1::PixelFormat(
            DXGI_FORMAT_B8G8R8A8_UNORM,
            D2D1_ALPHA_MODE_PREMULTIPLIED);
        sourceProperties.dpiX = 96.0f;
        sourceProperties.dpiY = 96.0f;

        Microsoft::WRL::ComPtr<ID2D1Bitmap1> sourceBitmap;
        auto sourceResult = d2dContext->CreateBitmap(
            D2D1::SizeU(region.Width, region.Height),
            region.BgraPixels.data(),
            region.Stride,
            &sourceProperties,
            sourceBitmap.ReleaseAndGetAddressOf());
        if (FAILED(sourceResult))
        {
            AppendNativePlaybackDiagnostic(
                L"DxDeviceResources.DrawSubtitleBitmapOverlay source creation failed hr=" +
                std::to_wstring(static_cast<unsigned long>(sourceResult)));
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
        auto drawResult = d2dContext->EndDraw();
        if (FAILED(drawResult))
        {
            AppendNativePlaybackDiagnostic(
                L"DxDeviceResources.DrawSubtitleBitmapOverlay draw failed hr=" +
                std::to_wstring(static_cast<unsigned long>(drawResult)));
        }
        return SUCCEEDED(drawResult);
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
        m_lastVideoProcessorConversionValidated =
            status.rfind(L"validated", 0) == 0 &&
            status.find(L"requires-") == std::wstring::npos &&
            status.find(L"failed") == std::wstring::npos;
        m_lastVideoProcessorInputColorSpace = inputColorSpace;
        m_lastVideoProcessorOutputColorSpace = outputColorSpace;
        m_lastVideoProcessorConversionStatus = std::move(status);
    }
}
