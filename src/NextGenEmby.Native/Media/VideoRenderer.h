#pragma once

#include "DxDeviceResources.h"
#include "VideoDecoder.h"

namespace winrt::NextGenEmby::Native::implementation
{
    class VideoRenderer
    {
    public:
        explicit VideoRenderer(DxDeviceResources& deviceResources);

        void Render(DecodedVideoFrame const& frame);
        void ClearToBlack();

    private:
        DxDeviceResources& m_deviceResources;
        VideoHdrKind m_currentHdrKind{VideoHdrKind::None};
    };
}
