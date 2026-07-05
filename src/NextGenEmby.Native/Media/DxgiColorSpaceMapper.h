#pragma once

#include <cstdint>
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <string>

namespace winrt::NextGenEmby::Native::implementation
{
    enum class DxgiPostProcessKind
    {
        None = 0,
        Hdr10PqToSdrHable = 1,
        HlgToPq = 2
    };

    struct VideoColorMetadata
    {
        int ColorPrimaries{2};
        int ColorTransfer{2};
        int ColorSpace{2};
        int ColorRange{0};
        int ChromaLocation{0};
        uint32_t BitsPerChannel{8};
        bool HasDolbyVisionMetadata{false};
        uint32_t DolbyVisionProfile{0};
        uint32_t DolbyVisionLevel{0};
        bool DolbyVisionRpuPresent{false};
        bool DolbyVisionEnhancementLayerPresent{false};
        bool DolbyVisionBaseLayerPresent{false};
        uint32_t DolbyVisionBaseLayerSignalCompatibilityId{0};
    };

    struct DxgiVideoColorSpaceMapping
    {
        DXGI_COLOR_SPACE_TYPE InputColorSpace{DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P709};
        DXGI_COLOR_SPACE_TYPE OutputColorSpace{DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709};
        DXGI_COLOR_SPACE_TYPE AlternativeInputColorSpace{DXGI_COLOR_SPACE_CUSTOM};
        D3D11_VIDEO_PROCESSOR_COLOR_SPACE LegacyInputColorSpace{};
        D3D11_VIDEO_PROCESSOR_COLOR_SPACE LegacyOutputColorSpace{};
        bool IsSupported{true};
        bool HasAlternativeInputColorSpace{false};
        bool IsHdr10{false};
        bool IsHlg{false};
        bool NeedsTenBitOutput{false};
        bool RequiresToneMapping{false};
        DxgiPostProcessKind PostProcessKind{DxgiPostProcessKind::None};
        std::wstring Reason;
    };

    DxgiVideoColorSpaceMapping MapVideoColorSpace(
        VideoColorMetadata const& metadata,
        bool outputHdr10) noexcept;
    bool IsHdr10Color(VideoColorMetadata const& metadata) noexcept;
    bool IsHlgColor(VideoColorMetadata const& metadata) noexcept;
    bool UsesBt709Matrix(VideoColorMetadata const& metadata, uint32_t width, uint32_t height) noexcept;
}
