#pragma once

#include <d3d11_4.h>
#include <d3dcommon.h>
#include <wrl/client.h>

namespace winrt::NextGenEmby::Native::implementation
{
    enum class HdrToneMappingMode
    {
        PqToSdrHable = 0,
        HlgToPq = 1
    };

    class HdrToneMappingPass
    {
    public:
        bool Render(
            ID3D11Device* device,
            ID3D11DeviceContext* context,
            ID3D11Texture2D* sourceTexture,
            ID3D11Texture2D* targetTexture,
            float sourcePeakLuminance,
            HdrToneMappingMode mode = HdrToneMappingMode::PqToSdrHable);

    private:
        bool EnsureResources(ID3D11Device* device);
        bool CompileShader(
            char const* entryPoint,
            char const* profile,
            Microsoft::WRL::ComPtr<ID3DBlob>& blob) const;

        Microsoft::WRL::ComPtr<ID3D11VertexShader> m_vertexShader;
        Microsoft::WRL::ComPtr<ID3D11PixelShader> m_pixelShader;
        Microsoft::WRL::ComPtr<ID3D11Buffer> m_constantBuffer;
        Microsoft::WRL::ComPtr<ID3D11SamplerState> m_sampler;
    };
}
