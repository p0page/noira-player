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

        auto rendered = false;
        if (frame.Texture)
        {
            rendered = m_deviceResources.TryCopyToBackBuffer(frame.Texture.Get());
            if (!rendered)
            {
                rendered = m_deviceResources.TryProcessVideoFrameToBackBuffer(
                    frame.Texture.Get(),
                    frame.TextureArrayIndex,
                    frame.Width,
                    frame.Height);
            }
        }

        if (!rendered && !frame.BgraPixels.empty())
        {
            m_deviceResources.DrawBgraFrameToBackBuffer(
                frame.BgraPixels.data(),
                frame.Width,
                frame.Height,
                frame.BgraStride);
        }
    }

    void VideoRenderer::ClearToBlack()
    {
        m_currentHdrKind = VideoHdrKind::None;
        m_deviceResources.SetSdrColorSpace();
        m_deviceResources.ClearToBlack();
    }
}
