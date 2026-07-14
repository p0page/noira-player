#pragma once

#include "DxDeviceResources.h"
#include "VideoDecoder.h"

namespace winrt::NoiraPlayer::Native::implementation
{
    inline bool ShouldOutputHdr10ForFrame(VideoHdrKind hdrKind, bool isTenBitSwapChain) noexcept
    {
        return isTenBitSwapChain &&
            (hdrKind == VideoHdrKind::Hdr10 || hdrKind == VideoHdrKind::Hlg);
    }

    inline DXGI_HDR_METADATA_HDR10 CreateHlgReferenceHdr10Metadata() noexcept
    {
        DXGI_HDR_METADATA_HDR10 metadata{};
        metadata.RedPrimary[0] = 34000;
        metadata.RedPrimary[1] = 16000;
        metadata.GreenPrimary[0] = 13250;
        metadata.GreenPrimary[1] = 34500;
        metadata.BluePrimary[0] = 7500;
        metadata.BluePrimary[1] = 3000;
        metadata.WhitePoint[0] = 15635;
        metadata.WhitePoint[1] = 16450;
        metadata.MaxMasteringLuminance = 1000 * 10000;
        metadata.MinMasteringLuminance = 50;
        return metadata;
    }

    class VideoRenderer
    {
    public:
        explicit VideoRenderer(DxDeviceResources& deviceResources);

        VideoRenderPhaseSample Render(DecodedVideoFrame const& frame, bool hdrDisplayActive);
        void ClearToBlack();

    private:
        DxDeviceResources& m_deviceResources;
        VideoHdrKind m_currentHdrKind{VideoHdrKind::None};
    };
}
