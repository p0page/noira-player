#include "pch.h"
#include "VideoRenderer.h"

namespace winrt::NoiraPlayer::Native::implementation
{
    VideoRenderer::VideoRenderer(DxDeviceResources& deviceResources)
        : m_deviceResources(deviceResources)
    {
    }

    VideoRenderPhaseSample VideoRenderer::Render(
        DecodedVideoFrame const& frame,
        bool hdrDisplayActive)
    {
        auto outputHdr10 = hdrDisplayActive &&
            ShouldOutputHdr10ForFrame(frame.HdrKind, m_deviceResources.IsTenBitSwapChain());
        if (outputHdr10)
        {
            if (frame.Hdr10Metadata.has_value())
            {
                m_deviceResources.SetHdr10Metadata(frame.Hdr10Metadata.value());
            }
            else if (frame.HdrKind == VideoHdrKind::Hlg)
            {
                m_deviceResources.SetHdr10Metadata(CreateHlgReferenceHdr10Metadata());
            }

            outputHdr10 = m_deviceResources.SetHdr10ColorSpace();
        }

        if (!outputHdr10 && m_currentHdrKind != VideoHdrKind::None)
        {
            m_deviceResources.SetSdrColorSpace();
        }

        m_currentHdrKind = outputHdr10 ? VideoHdrKind::Hdr10 : VideoHdrKind::None;
        m_deviceResources.ObserveVideoColorMapping(frame.ColorMetadata, outputHdr10);

        VideoRenderPhaseSample sample{};
        if (frame.Texture)
        {
            if (m_deviceResources.TryCopyToBackBuffer(frame.Texture.Get()))
            {
                sample.Path = VideoRenderPath::DirectCopy;
            }
            else
            {
                m_deviceResources.TryProcessVideoFrameToBackBuffer(
                    frame.Texture.Get(),
                    frame.TextureArrayIndex,
                    frame.Width,
                    frame.Height,
                    frame.DisplayWidth,
                    frame.DisplayHeight,
                    frame.ColorMetadata,
                    outputHdr10,
                    frame.Hdr10Metadata.has_value() ? &frame.Hdr10Metadata.value() : nullptr,
                    &sample);
            }
        }

        if (sample.Path == VideoRenderPath::None && !frame.BgraPixels.empty())
        {
            if (m_deviceResources.DrawBgraFrameToBackBuffer(
                frame.BgraPixels.data(),
                frame.Width,
                frame.Height,
                frame.DisplayWidth,
                frame.DisplayHeight,
                frame.BgraStride))
            {
                sample.Path = VideoRenderPath::Bgra;
            }
        }

        return sample;
    }

    void VideoRenderer::ClearToBlack()
    {
        m_currentHdrKind = VideoHdrKind::None;
        m_deviceResources.SetSdrColorSpace();
        m_deviceResources.ClearToBlack();
    }
}
