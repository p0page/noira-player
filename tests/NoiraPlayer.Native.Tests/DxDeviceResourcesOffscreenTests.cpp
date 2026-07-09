#include <cassert>

#include "DxDeviceResources.h"

using winrt::NoiraPlayer::Native::implementation::DxDeviceResources;

int main()
{
    DxDeviceResources resources;
    assert(!resources.HasRenderTarget());

    resources.CreateSwapChain(16, 16, false);

    assert(resources.HasRenderTarget());
    assert(resources.ClearToBlack());
    assert(resources.Present());
    return 0;
}
