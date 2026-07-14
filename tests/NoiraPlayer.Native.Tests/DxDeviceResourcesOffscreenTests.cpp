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
