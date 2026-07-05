#include "pch.h"
#include "HdrToneMappingPass.h"

#include "../NativePlaybackDiagnostics.h"

#include <algorithm>
#include <cstring>
#include <cstdint>
#include <d3dcompiler.h>
#include <string>

namespace winrt::NextGenEmby::Native::implementation
{
    namespace
    {
        char const HdrToneMappingShader[] = R"(
Texture2D SourceTexture : register(t0);
SamplerState LinearSampler : register(s0);

cbuffer ToneMapConstants : register(b0)
{
    float g_toneP1;
    float g_toneP2;
    uint g_mode;
    float g_padding;
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
    output.TexCoord = float2((position.x + 1.0f) * 0.5f, 1.0f - ((position.y + 1.0f) * 0.5f));
    return output;
}

float3 InversePQ(float3 x)
{
    static const float ST2084_m1 = 2610.0f / (4096.0f * 4.0f);
    static const float ST2084_m2 = (2523.0f / 4096.0f) * 128.0f;
    static const float ST2084_c1 = 3424.0f / 4096.0f;
    static const float ST2084_c2 = (2413.0f / 4096.0f) * 32.0f;
    static const float ST2084_c3 = (2392.0f / 4096.0f) * 32.0f;

    x = pow(max(x, 0.0f), 1.0f / ST2084_m2);
    x = max(x - ST2084_c1, 0.0f) / (ST2084_c2 - ST2084_c3 * x);
    return pow(x, 1.0f / ST2084_m1);
}

float3 TransferPQ(float3 x)
{
    static const float ST2084_m1 = 2610.0f / (4096.0f * 4.0f);
    static const float ST2084_m2 = (2523.0f / 4096.0f) * 128.0f;
    static const float ST2084_c1 = 3424.0f / 4096.0f;
    static const float ST2084_c2 = (2413.0f / 4096.0f) * 32.0f;
    static const float ST2084_c3 = (2392.0f / 4096.0f) * 32.0f;

    x = pow(max(x, 0.0f) / 10000.0f, ST2084_m1);
    x = (ST2084_c1 + ST2084_c2 * x) / (1.0f + ST2084_c3 * x);
    return pow(x, ST2084_m2);
}

float InverseHLGChannel(float x)
{
    static const float B67_a = 0.17883277f;
    static const float B67_b = 0.28466892f;
    static const float B67_c = 0.55991073f;
    return (x <= 0.5f) ? x * x / 3.0f : (exp((x - B67_c) / B67_a) + B67_b) / 12.0f;
}

float3 InverseHLG(float3 x)
{
    return float3(InverseHLGChannel(x.r), InverseHLGChannel(x.g), InverseHLGChannel(x.b));
}

float3 HlgToPq(float3 x)
{
    static const float HLG_Lw = 1000.0f;
    static const float HLG_gamma = 1.2f + 0.42f * log10(HLG_Lw / 1000.0f);
    static const float3 bt2020_lum_rgbweights = float3(0.2627f, 0.6780f, 0.0593f);

    x = InverseHLG(x);
    float hlgYs = dot(bt2020_lum_rgbweights, x);
    x *= HLG_Lw * pow(max(hlgYs, 0.0001f), HLG_gamma - 1.0f);
    return TransferPQ(x);
}

float3 Hable(float3 x)
{
    static const float A = 0.15f;
    static const float B = 0.5f;
    static const float C = 0.1f;
    static const float D = 0.2f;
    static const float E = 0.02f;
    static const float F = 0.3f;
    return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

float4 PSMain(VertexOutput input) : SV_TARGET
{
    float4 color = SourceTexture.Sample(LinearSampler, input.TexCoord);
    color.rgb = saturate(color.rgb);
    if (g_mode == 1)
    {
        color.rgb = HlgToPq(color.rgb);
        return float4(saturate(color.rgb), 1.0f);
    }

    color.rgb = InversePQ(color.rgb);
    color.rgb *= g_toneP1;
    color.rgb = Hable(color.rgb * g_toneP2) / max(Hable(float3(g_toneP2, g_toneP2, g_toneP2)), 0.0001f);
    color.rgb = pow(max(color.rgb, 0.0f), 1.0f / 2.2f);
    return float4(saturate(color.rgb), 1.0f);
}
)";

        struct ToneMapConstants
        {
            float ToneP1;
            float ToneP2;
            uint32_t Mode;
            float Padding;
        };

        std::wstring HResultMessage(char const* operation, HRESULT result)
        {
            return std::wstring(operation, operation + std::strlen(operation)) +
                L" failed hr=0x" + std::to_wstring(static_cast<unsigned long>(result));
        }
    }

    bool HdrToneMappingPass::CompileShader(
        char const* entryPoint,
        char const* profile,
        Microsoft::WRL::ComPtr<ID3DBlob>& blob) const
    {
        Microsoft::WRL::ComPtr<ID3DBlob> errors;
        auto result = D3DCompile(
            HdrToneMappingShader,
            std::strlen(HdrToneMappingShader),
            "HdrToneMappingPass",
            nullptr,
            nullptr,
            entryPoint,
            profile,
            D3DCOMPILE_ENABLE_STRICTNESS,
            0,
            blob.ReleaseAndGetAddressOf(),
            errors.ReleaseAndGetAddressOf());
        if (SUCCEEDED(result))
        {
            return true;
        }

        auto message = HResultMessage("D3DCompile", result);
        if (errors)
        {
            std::string errorText(
                static_cast<char const*>(errors->GetBufferPointer()),
                errors->GetBufferSize());
            message += L" ";
            message += winrt::to_hstring(errorText);
        }

        AppendNativePlaybackDiagnostic(message);
        return false;
    }

    bool HdrToneMappingPass::EnsureResources(ID3D11Device* device)
    {
        if (m_vertexShader && m_pixelShader && m_constantBuffer && m_sampler)
        {
            return true;
        }

        if (device == nullptr)
        {
            return false;
        }

        Microsoft::WRL::ComPtr<ID3DBlob> vertexShaderBlob;
        Microsoft::WRL::ComPtr<ID3DBlob> pixelShaderBlob;
        if (!CompileShader("VSMain", "vs_4_0", vertexShaderBlob) ||
            !CompileShader("PSMain", "ps_4_0", pixelShaderBlob))
        {
            return false;
        }

        auto result = device->CreateVertexShader(
            vertexShaderBlob->GetBufferPointer(),
            vertexShaderBlob->GetBufferSize(),
            nullptr,
            m_vertexShader.ReleaseAndGetAddressOf());
        if (FAILED(result))
        {
            AppendNativePlaybackDiagnostic(HResultMessage("CreateVertexShader", result));
            return false;
        }

        result = device->CreatePixelShader(
            pixelShaderBlob->GetBufferPointer(),
            pixelShaderBlob->GetBufferSize(),
            nullptr,
            m_pixelShader.ReleaseAndGetAddressOf());
        if (FAILED(result))
        {
            AppendNativePlaybackDiagnostic(HResultMessage("CreatePixelShader", result));
            return false;
        }

        D3D11_BUFFER_DESC constantBufferDescription{};
        constantBufferDescription.ByteWidth = sizeof(ToneMapConstants);
        constantBufferDescription.Usage = D3D11_USAGE_DEFAULT;
        constantBufferDescription.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
        result = device->CreateBuffer(
            &constantBufferDescription,
            nullptr,
            m_constantBuffer.ReleaseAndGetAddressOf());
        if (FAILED(result))
        {
            AppendNativePlaybackDiagnostic(HResultMessage("CreateBuffer", result));
            return false;
        }

        D3D11_SAMPLER_DESC samplerDescription{};
        samplerDescription.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
        samplerDescription.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDescription.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDescription.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDescription.MaxLOD = D3D11_FLOAT32_MAX;
        result = device->CreateSamplerState(
            &samplerDescription,
            m_sampler.ReleaseAndGetAddressOf());
        if (FAILED(result))
        {
            AppendNativePlaybackDiagnostic(HResultMessage("CreateSamplerState", result));
            return false;
        }

        return true;
    }

    bool HdrToneMappingPass::Render(
        ID3D11Device* device,
        ID3D11DeviceContext* context,
        ID3D11Texture2D* sourceTexture,
        ID3D11Texture2D* targetTexture,
        float sourcePeakLuminance,
        HdrToneMappingMode mode)
    {
        if (device == nullptr || context == nullptr || sourceTexture == nullptr || targetTexture == nullptr)
        {
            return false;
        }

        if (!EnsureResources(device))
        {
            return false;
        }

        D3D11_TEXTURE2D_DESC sourceDescription{};
        D3D11_TEXTURE2D_DESC targetDescription{};
        sourceTexture->GetDesc(&sourceDescription);
        targetTexture->GetDesc(&targetDescription);

        Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> sourceView;
        auto result = device->CreateShaderResourceView(
            sourceTexture,
            nullptr,
            sourceView.ReleaseAndGetAddressOf());
        if (FAILED(result))
        {
            AppendNativePlaybackDiagnostic(HResultMessage("CreateShaderResourceView", result));
            return false;
        }

        Microsoft::WRL::ComPtr<ID3D11RenderTargetView> targetView;
        result = device->CreateRenderTargetView(
            targetTexture,
            nullptr,
            targetView.ReleaseAndGetAddressOf());
        if (FAILED(result))
        {
            AppendNativePlaybackDiagnostic(HResultMessage("CreateRenderTargetView", result));
            return false;
        }

        auto luminance = std::clamp(sourcePeakLuminance, 100.0f, 10000.0f);
        ToneMapConstants constants{};
        constants.ToneP1 = (10000.0f / luminance) * 2.0f;
        constants.ToneP2 = luminance / 100.0f;
        constants.Mode = static_cast<uint32_t>(mode);
        context->UpdateSubresource(m_constantBuffer.Get(), 0, nullptr, &constants, 0, 0);

        Microsoft::WRL::ComPtr<ID3D11RenderTargetView> previousTargetView;
        Microsoft::WRL::ComPtr<ID3D11DepthStencilView> previousDepthStencilView;
        context->OMGetRenderTargets(
            1,
            previousTargetView.ReleaseAndGetAddressOf(),
            previousDepthStencilView.ReleaseAndGetAddressOf());

        D3D11_VIEWPORT previousViewports[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{};
        UINT previousViewportCount = ARRAYSIZE(previousViewports);
        context->RSGetViewports(&previousViewportCount, previousViewports);

        D3D11_VIEWPORT viewport{};
        viewport.TopLeftX = 0.0f;
        viewport.TopLeftY = 0.0f;
        viewport.Width = static_cast<float>(targetDescription.Width);
        viewport.Height = static_cast<float>(targetDescription.Height);
        viewport.MinDepth = 0.0f;
        viewport.MaxDepth = 1.0f;

        ID3D11RenderTargetView* renderTargets[] = {targetView.Get()};
        ID3D11ShaderResourceView* resources[] = {sourceView.Get()};
        ID3D11SamplerState* samplers[] = {m_sampler.Get()};
        ID3D11Buffer* constantBuffers[] = {m_constantBuffer.Get()};

        context->OMSetRenderTargets(1, renderTargets, nullptr);
        context->RSSetViewports(1, &viewport);
        context->IASetInputLayout(nullptr);
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->VSSetShader(m_vertexShader.Get(), nullptr, 0);
        context->PSSetShader(m_pixelShader.Get(), nullptr, 0);
        context->PSSetShaderResources(0, 1, resources);
        context->PSSetSamplers(0, 1, samplers);
        context->PSSetConstantBuffers(0, 1, constantBuffers);
        context->Draw(3, 0);

        ID3D11ShaderResourceView* nullResources[] = {nullptr};
        context->PSSetShaderResources(0, 1, nullResources);
        context->VSSetShader(nullptr, nullptr, 0);
        context->PSSetShader(nullptr, nullptr, 0);
        context->OMSetRenderTargets(1, previousTargetView.GetAddressOf(), previousDepthStencilView.Get());
        if (previousViewportCount > 0)
        {
            context->RSSetViewports(previousViewportCount, previousViewports);
        }

        return true;
    }
}
