#include "pch.h"
#include "VideoRenderer.h"

namespace winrt::NextGenEmby::Native::implementation
{
    VideoRenderer::VideoRenderer(DxDeviceResources& deviceResources)
        : m_deviceResources(deviceResources)
    {
    }

    void VideoRenderer::Render(DecodedVideoFrame const& frame)
    {
        if (frame.HdrKind == VideoHdrKind::Hdr10)
        {
            if (frame.Hdr10Metadata.has_value())
            {
                m_deviceResources.SetHdr10Metadata(frame.Hdr10Metadata.value());
            }

            m_deviceResources.SetHdr10ColorSpace();
        }
        else if (m_currentHdrKind != VideoHdrKind::None)
        {
            m_deviceResources.SetSdrColorSpace();
        }

        m_currentHdrKind = frame.HdrKind;

        if (frame.Texture)
        {
            m_deviceResources.TryCopyToBackBuffer(frame.Texture.Get());
        }
    }

    void VideoRenderer::ClearToBlack()
    {
        m_currentHdrKind = VideoHdrKind::None;
        m_deviceResources.SetSdrColorSpace();
        m_deviceResources.ClearToBlack();
    }
}
