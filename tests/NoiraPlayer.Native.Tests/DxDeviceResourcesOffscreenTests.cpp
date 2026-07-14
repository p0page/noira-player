#include <cassert>

#include "DxDeviceResources.h"
#include "Media/SubtitleBitmap.h"

using winrt::NoiraPlayer::Native::implementation::DxDeviceResources;
using winrt::NoiraPlayer::Native::implementation::SubtitleBitmapRegion;
using winrt::NoiraPlayer::Native::implementation::VideoColorMetadata;
using winrt::NoiraPlayer::Native::implementation::VideoRenderPath;
using winrt::NoiraPlayer::Native::implementation::VideoRenderPhaseSample;

int main()
{
    DxDeviceResources resources;
    assert(!resources.HasRenderTarget());
    assert(!resources.HasCachedVideoProcessor());
    assert(resources.VideoProcessorCacheHitCount() == 0);
    assert(resources.VideoProcessorCacheMissCount() == 0);

    resources.CreateSwapChain(16, 16, false);

    assert(resources.HasRenderTarget());
    assert(resources.ClearToBlack());
    VideoRenderPhaseSample failedSample{};
    failedSample.Path = VideoRenderPath::DirectCopy;
    assert(!resources.TryProcessVideoFrameToBackBuffer(
        nullptr,
        0,
        16,
        16,
        16,
        16,
        VideoColorMetadata{},
        false,
        nullptr,
        &failedSample));
    assert(failedSample.Path == VideoRenderPath::None);
    assert(!resources.HasCachedVideoProcessor());

    D3D11_TEXTURE2D_DESC videoDescription{};
    videoDescription.Width = 16;
    videoDescription.Height = 16;
    videoDescription.MipLevels = 1;
    videoDescription.ArraySize = 1;
    videoDescription.Format = DXGI_FORMAT_NV12;
    videoDescription.SampleDesc.Count = 1;
    videoDescription.Usage = D3D11_USAGE_DEFAULT;
    videoDescription.BindFlags = D3D11_BIND_DECODER;
    Microsoft::WRL::ComPtr<ID3D11Texture2D> videoTexture;
    assert(SUCCEEDED(resources.Device()->CreateTexture2D(
        &videoDescription,
        nullptr,
        videoTexture.ReleaseAndGetAddressOf())));

    VideoRenderPhaseSample firstVideoSample{};
    assert(resources.TryProcessVideoFrameToBackBuffer(
        videoTexture.Get(),
        0,
        16,
        16,
        16,
        16,
        VideoColorMetadata{},
        false,
        nullptr,
        &firstVideoSample));
    assert(firstVideoSample.Path == VideoRenderPath::VideoProcessor);
    assert(resources.HasCachedVideoProcessor());
    assert(resources.VideoProcessorCacheHitCount() == 0);
    assert(resources.VideoProcessorCacheMissCount() == 1);

    VideoRenderPhaseSample secondVideoSample{};
    assert(resources.TryProcessVideoFrameToBackBuffer(
        videoTexture.Get(),
        0,
        16,
        16,
        16,
        16,
        VideoColorMetadata{},
        false,
        nullptr,
        &secondVideoSample));
    assert(secondVideoSample.Path == VideoRenderPath::VideoProcessor);
    assert(resources.VideoProcessorCacheHitCount() == 1);
    assert(resources.VideoProcessorCacheMissCount() == 1);

    resources.CreateSwapChain(32, 16, false);
    assert(!resources.HasCachedVideoProcessor());

    VideoRenderPhaseSample recreatedVideoSample{};
    assert(resources.TryProcessVideoFrameToBackBuffer(
        videoTexture.Get(),
        0,
        16,
        16,
        16,
        16,
        VideoColorMetadata{},
        false,
        nullptr,
        &recreatedVideoSample));
    assert(recreatedVideoSample.Path == VideoRenderPath::VideoProcessor);
    assert(resources.HasCachedVideoProcessor());
    assert(resources.VideoProcessorCacheHitCount() == 1);
    assert(resources.VideoProcessorCacheMissCount() == 2);

    SubtitleBitmapRegion subtitle;
    subtitle.X = 4;
    subtitle.Y = 4;
    subtitle.Width = 2;
    subtitle.Height = 2;
    subtitle.CanvasWidth = 16;
    subtitle.CanvasHeight = 16;
    subtitle.Stride = 8;
    subtitle.BgraPixels =
    {
        0, 0, 255, 255, 0, 255, 0, 255,
        255, 0, 0, 255, 255, 255, 255, 255
    };
    assert(resources.DrawSubtitleBitmapOverlay(subtitle));
    assert(resources.Present());
    return 0;
}
