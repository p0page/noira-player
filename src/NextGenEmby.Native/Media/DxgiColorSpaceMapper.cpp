#include "pch.h"
#include "DxgiColorSpaceMapper.h"

#pragma warning(push)
#pragma warning(disable : 4244 4819)
extern "C"
{
#include <libavutil/pixfmt.h>
}
#pragma warning(pop)

namespace winrt::NextGenEmby::Native::implementation
{
    namespace
    {
        bool IsFullRange(VideoColorMetadata const& metadata) noexcept
        {
            return metadata.ColorRange == AVCOL_RANGE_JPEG;
        }

        bool IsBt2020(VideoColorMetadata const& metadata) noexcept
        {
            return metadata.ColorPrimaries == AVCOL_PRI_BT2020 ||
                metadata.ColorSpace == AVCOL_SPC_BT2020_NCL ||
                metadata.ColorSpace == AVCOL_SPC_BT2020_CL;
        }

        bool IsBt601(VideoColorMetadata const& metadata) noexcept
        {
            return metadata.ColorPrimaries == AVCOL_PRI_BT470BG ||
                metadata.ColorPrimaries == AVCOL_PRI_SMPTE170M ||
                metadata.ColorPrimaries == AVCOL_PRI_SMPTE240M ||
                metadata.ColorSpace == AVCOL_SPC_BT470BG ||
                metadata.ColorSpace == AVCOL_SPC_SMPTE170M ||
                metadata.ColorSpace == AVCOL_SPC_SMPTE240M;
        }

        bool IsRgb(VideoColorMetadata const& metadata) noexcept
        {
            return metadata.ColorSpace == AVCOL_SPC_RGB;
        }

        int DefaultChromaLocation(VideoColorMetadata const& metadata) noexcept
        {
            if (IsRgb(metadata))
            {
                return AVCHROMA_LOC_UNSPECIFIED;
            }

            if (IsBt2020(metadata) &&
                (metadata.ColorTransfer == AVCOL_TRC_SMPTE2084 ||
                    metadata.ColorTransfer == AVCOL_TRC_ARIB_STD_B67))
            {
                return AVCHROMA_LOC_TOPLEFT;
            }

            return AVCHROMA_LOC_LEFT;
        }

        int EffectiveChromaLocation(VideoColorMetadata const& metadata) noexcept
        {
            return metadata.ChromaLocation == AVCHROMA_LOC_LEFT ||
                metadata.ChromaLocation == AVCHROMA_LOC_TOPLEFT
                ? metadata.ChromaLocation
                : DefaultChromaLocation(metadata);
        }

        int AlternativeChromaLocation(int chromaLocation) noexcept
        {
            switch (chromaLocation)
            {
            case AVCHROMA_LOC_TOPLEFT:
                return AVCHROMA_LOC_LEFT;
            case AVCHROMA_LOC_LEFT:
                return AVCHROMA_LOC_TOPLEFT;
            default:
                return AVCHROMA_LOC_UNSPECIFIED;
            }
        }

        bool IsJpegTransfer(VideoColorMetadata const& metadata) noexcept
        {
            return metadata.ColorTransfer == AVCOL_TRC_SMPTE170M &&
                !IsBt601(metadata) &&
                !IsBt2020(metadata);
        }

        DXGI_COLOR_SPACE_TYPE MapRgbColorSpace(VideoColorMetadata const& metadata) noexcept
        {
            if (!IsFullRange(metadata))
            {
                if (metadata.ColorPrimaries == AVCOL_PRI_BT2020)
                {
                    return metadata.ColorTransfer == AVCOL_TRC_SMPTE2084
                        ? DXGI_COLOR_SPACE_RGB_STUDIO_G2084_NONE_P2020
                        : DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P2020;
                }

                return DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P709;
            }

            if (metadata.ColorPrimaries == AVCOL_PRI_BT2020)
            {
                return metadata.ColorTransfer == AVCOL_TRC_SMPTE2084
                    ? DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020
                    : DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P2020;
            }

            return DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709;
        }

        DXGI_COLOR_SPACE_TYPE MapBt2020G22ColorSpace(
            bool fullRange,
            int chromaLocation) noexcept
        {
            switch (chromaLocation)
            {
            case AVCHROMA_LOC_LEFT:
                return fullRange
                    ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P2020
                    : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P2020;
            case AVCHROMA_LOC_TOPLEFT:
            case AVCHROMA_LOC_UNSPECIFIED:
                return fullRange
                    ? DXGI_COLOR_SPACE_CUSTOM
                    : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_TOPLEFT_P2020;
            default:
                return DXGI_COLOR_SPACE_CUSTOM;
            }
        }

        DXGI_COLOR_SPACE_TYPE MapBt2020PqColorSpace(
            bool fullRange,
            int chromaLocation) noexcept
        {
            if (fullRange)
            {
                return DXGI_COLOR_SPACE_CUSTOM;
            }

            switch (chromaLocation)
            {
            case AVCHROMA_LOC_LEFT:
                return DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_LEFT_P2020;
            case AVCHROMA_LOC_TOPLEFT:
            case AVCHROMA_LOC_UNSPECIFIED:
                return DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_TOPLEFT_P2020;
            default:
                return DXGI_COLOR_SPACE_CUSTOM;
            }
        }

        DXGI_COLOR_SPACE_TYPE MapInputColorSpace(
            VideoColorMetadata const& metadata,
            int effectiveTransfer,
            int chromaLocation) noexcept
        {
            if (IsRgb(metadata))
            {
                auto rgbMetadata = metadata;
                rgbMetadata.ColorTransfer = effectiveTransfer;
                return MapRgbColorSpace(rgbMetadata);
            }

            if (IsBt2020(metadata))
            {
                if (effectiveTransfer == AVCOL_TRC_SMPTE2084)
                {
                    return MapBt2020PqColorSpace(IsFullRange(metadata), chromaLocation);
                }

                if (effectiveTransfer == AVCOL_TRC_ARIB_STD_B67)
                {
                    return IsFullRange(metadata)
                        ? DXGI_COLOR_SPACE_YCBCR_FULL_GHLG_TOPLEFT_P2020
                        : DXGI_COLOR_SPACE_YCBCR_STUDIO_GHLG_TOPLEFT_P2020;
                }

                return MapBt2020G22ColorSpace(IsFullRange(metadata), chromaLocation);
            }

            if (IsBt601(metadata))
            {
                return IsFullRange(metadata)
                    ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P601
                    : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P601;
            }

            if (IsJpegTransfer(metadata))
            {
                return IsFullRange(metadata)
                    ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_NONE_P709_X601
                    : DXGI_COLOR_SPACE_CUSTOM;
            }

            return IsFullRange(metadata)
                ? DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P709
                : DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P709;
        }

        bool UsesBt709MatrixLegacy(VideoColorMetadata const& metadata) noexcept
        {
            switch (metadata.ColorSpace)
            {
            case AVCOL_SPC_BT709:
            case AVCOL_SPC_BT2020_NCL:
            case AVCOL_SPC_BT2020_CL:
                return true;
            case AVCOL_SPC_BT470BG:
            case AVCOL_SPC_SMPTE170M:
            case AVCOL_SPC_SMPTE240M:
                return false;
            default:
                return true;
            }
        }

        D3D11_VIDEO_PROCESSOR_COLOR_SPACE CreateLegacyInputColorSpace(
            VideoColorMetadata const& metadata) noexcept
        {
            D3D11_VIDEO_PROCESSOR_COLOR_SPACE colorSpace{};
            colorSpace.Usage = 0;
            colorSpace.RGB_Range = 0;
            colorSpace.YCbCr_Matrix = UsesBt709MatrixLegacy(metadata) ? 1 : 0;
            colorSpace.YCbCr_xvYCC = 0;
            colorSpace.Nominal_Range = IsFullRange(metadata)
                ? D3D11_VIDEO_PROCESSOR_NOMINAL_RANGE_0_255
                : D3D11_VIDEO_PROCESSOR_NOMINAL_RANGE_16_235;
            return colorSpace;
        }

        D3D11_VIDEO_PROCESSOR_COLOR_SPACE CreateLegacyOutputColorSpace() noexcept
        {
            D3D11_VIDEO_PROCESSOR_COLOR_SPACE colorSpace{};
            colorSpace.Usage = 0;
            colorSpace.RGB_Range = 0;
            colorSpace.YCbCr_Matrix = 1;
            colorSpace.YCbCr_xvYCC = 0;
            colorSpace.Nominal_Range = D3D11_VIDEO_PROCESSOR_NOMINAL_RANGE_0_255;
            return colorSpace;
        }
    }

    DxgiVideoColorSpaceMapping MapVideoColorSpace(
        VideoColorMetadata const& metadata,
        bool outputHdr10) noexcept
    {
        DxgiVideoColorSpaceMapping mapping{};
        mapping.IsHdr10 = IsHdr10Color(metadata);
        mapping.IsHlg = IsHlgColor(metadata);
        mapping.NeedsTenBitOutput = metadata.BitsPerChannel > 8 || mapping.IsHdr10 || mapping.IsHlg;
        mapping.OutputColorSpace = outputHdr10
            ? DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020
            : DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709;

        auto effectiveTransfer = metadata.ColorTransfer;
        if (mapping.IsHdr10 && !outputHdr10)
        {
            effectiveTransfer = AVCOL_TRC_BT709;
            mapping.RequiresToneMapping = true;
            mapping.PostProcessKind = DxgiPostProcessKind::Hdr10PqToSdrHable;
            mapping.Reason = L"HDR10 to SDR requires explicit tone mapping.";
        }
        else if (mapping.IsHlg)
        {
            effectiveTransfer = outputHdr10 ? AVCOL_TRC_SMPTE2084 : AVCOL_TRC_BT709;
            if (outputHdr10)
            {
                mapping.PostProcessKind = DxgiPostProcessKind::HlgToPq;
                mapping.Reason = L"HLG to HDR10/PQ requires explicit transfer conversion.";
            }
        }

        auto primaryChromaLocation = EffectiveChromaLocation(metadata);
        mapping.InputColorSpace = MapInputColorSpace(metadata, effectiveTransfer, primaryChromaLocation);

        if (IsBt2020(metadata))
        {
            auto alternative = AlternativeChromaLocation(primaryChromaLocation);
            if (alternative != AVCHROMA_LOC_UNSPECIFIED)
            {
                auto alternativeColorSpace = MapInputColorSpace(metadata, effectiveTransfer, alternative);
                if (alternativeColorSpace != DXGI_COLOR_SPACE_CUSTOM &&
                    alternativeColorSpace != mapping.InputColorSpace)
                {
                    mapping.AlternativeInputColorSpace = alternativeColorSpace;
                    mapping.HasAlternativeInputColorSpace = true;
                }
            }
        }

        mapping.IsSupported = mapping.InputColorSpace != DXGI_COLOR_SPACE_CUSTOM &&
            mapping.OutputColorSpace != DXGI_COLOR_SPACE_CUSTOM;
        if (!mapping.IsSupported && mapping.Reason.empty())
        {
            mapping.Reason = mapping.IsHdr10 && IsFullRange(metadata)
                ? L"No DXGI full-range PQ YCbCr color space."
                : L"No DXGI color-space mapping for source metadata.";
        }

        mapping.LegacyInputColorSpace = CreateLegacyInputColorSpace(metadata);
        mapping.LegacyOutputColorSpace = CreateLegacyOutputColorSpace();
        return mapping;
    }

    bool IsHdr10Color(VideoColorMetadata const& metadata) noexcept
    {
        return metadata.ColorTransfer == AVCOL_TRC_SMPTE2084 && IsBt2020(metadata);
    }

    bool IsHlgColor(VideoColorMetadata const& metadata) noexcept
    {
        return metadata.ColorTransfer == AVCOL_TRC_ARIB_STD_B67 && IsBt2020(metadata);
    }

    bool UsesBt709Matrix(VideoColorMetadata const& metadata, uint32_t width, uint32_t height) noexcept
    {
        switch (metadata.ColorSpace)
        {
        case AVCOL_SPC_BT709:
        case AVCOL_SPC_BT2020_NCL:
        case AVCOL_SPC_BT2020_CL:
            return true;
        case AVCOL_SPC_BT470BG:
        case AVCOL_SPC_SMPTE170M:
        case AVCOL_SPC_SMPTE240M:
            return false;
        default:
            return width >= 1280 || height > 576;
        }
    }
}
