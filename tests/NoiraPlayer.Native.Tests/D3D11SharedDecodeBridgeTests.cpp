#include <cassert>

#include <d3d11.h>
#include <wrl/client.h>

#include "Media/D3D11SharedDecodeBridge.h"

using Microsoft::WRL::ComPtr;
using winrt::NoiraPlayer::Native::implementation::D3D11SharedDecodeBridge;

int main()
{
    ComPtr<ID3D11Device> renderDevice;
    ComPtr<ID3D11DeviceContext> renderContext;
    auto result = D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        D3D11_CREATE_DEVICE_BGRA_SUPPORT | D3D11_CREATE_DEVICE_VIDEO_SUPPORT,
        nullptr,
        0,
        D3D11_SDK_VERSION,
        &renderDevice,
        nullptr,
        &renderContext);
    assert(SUCCEEDED(result));

    D3D11SharedDecodeBridge bridge;
    assert(bridge.Initialize(renderDevice.Get(), renderContext.Get()));
    assert(bridge.DecoderDevice() != nullptr);
    assert(bridge.DecoderContext() != nullptr);
    assert(bridge.DecoderTextureMiscFlags() == D3D11_RESOURCE_MISC_SHARED);

    uint64_t previousFenceValue = 0;
    for (auto format : { DXGI_FORMAT_NV12, DXGI_FORMAT_P010 })
    {
        D3D11_TEXTURE2D_DESC decoderTextureDescription{};
        decoderTextureDescription.Width = 1920;
        decoderTextureDescription.Height = 1080;
        decoderTextureDescription.MipLevels = 1;
        decoderTextureDescription.ArraySize = 4;
        decoderTextureDescription.Format = format;
        decoderTextureDescription.SampleDesc.Count = 1;
        decoderTextureDescription.Usage = D3D11_USAGE_DEFAULT;
        decoderTextureDescription.BindFlags = D3D11_BIND_DECODER;
        decoderTextureDescription.MiscFlags = bridge.DecoderTextureMiscFlags();

        ComPtr<ID3D11Texture2D> decoderTexture;
        result = bridge.DecoderDevice()->CreateTexture2D(
            &decoderTextureDescription,
            nullptr,
            &decoderTexture);
        assert(SUCCEEDED(result));

        ComPtr<ID3D11Texture2D> renderTexture;
        uint64_t fenceValue = 0;
        assert(bridge.ExportFrame(decoderTexture.Get(), renderTexture, fenceValue));
        assert(renderTexture != nullptr);
        assert(fenceValue > previousFenceValue);
        assert(bridge.WaitForFrame(fenceValue));
        previousFenceValue = fenceValue;

        D3D11_TEXTURE2D_DESC renderTextureDescription{};
        renderTexture->GetDesc(&renderTextureDescription);
        assert(renderTextureDescription.Width == decoderTextureDescription.Width);
        assert(renderTextureDescription.Height == decoderTextureDescription.Height);
        assert(renderTextureDescription.ArraySize == decoderTextureDescription.ArraySize);
        assert(renderTextureDescription.Format == decoderTextureDescription.Format);

        ComPtr<ID3D11Device> openedTextureDevice;
        renderTexture->GetDevice(&openedTextureDevice);
        assert(openedTextureDevice.Get() == renderDevice.Get());
    }

    bridge.Close();
    assert(bridge.DecoderDevice() == nullptr);
    return 0;
}
