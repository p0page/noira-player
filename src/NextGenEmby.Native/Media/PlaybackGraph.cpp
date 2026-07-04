#include "pch.h"
#include "PlaybackGraph.h"

namespace winrt::NextGenEmby::Native::implementation
{
    PlaybackGraph::PlaybackGraph(DxDeviceResources& deviceResources)
        : m_videoRenderer(deviceResources)
    {
    }

    void PlaybackGraph::Open(NextGenEmby::Native::NativePlaybackOpenRequest const& request)
    {
        if (request == nullptr)
        {
            throw winrt::hresult_invalid_argument(L"Playback request is required.");
        }

        try
        {
            m_input.Open(request.DirectStreamUrl());
            m_videoDecoder.Open(request.DirectStreamUrl(), 0);
            m_audioRenderer.Open(request.AudioStreamIndex(), request.HasAudioStreamIndex());
            m_subtitleRenderer.Open(request.HasSubtitleStreamIndex()
                ? std::optional<int32_t>{request.SubtitleStreamIndex()}
                : std::nullopt);
            m_videoRenderer.ClearToBlack();
            m_url = request.DirectStreamUrl();
            m_positionTicks = request.StartPositionTicks();
            m_open = true;
            m_paused = false;
            RenderNextFrame();
            m_audioRenderer.Start();
            m_subtitleRenderer.RenderAt(m_positionTicks);
        }
        catch (...)
        {
            Stop();
            throw;
        }
    }

    void PlaybackGraph::Pause()
    {
        if (m_open)
        {
            m_paused = true;
            m_audioRenderer.Pause();
        }
    }

    void PlaybackGraph::Resume()
    {
        if (m_open)
        {
            m_paused = false;
            m_audioRenderer.Resume();
        }
    }

    void PlaybackGraph::Seek(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Seek position cannot be negative.");
        }

        m_positionTicks = positionTicks;
        m_videoDecoder.Seek(positionTicks);
        RenderNextFrame();
        m_subtitleRenderer.RenderAt(m_positionTicks);
    }

    void PlaybackGraph::Stop() noexcept
    {
        m_audioRenderer.Stop();
        m_subtitleRenderer.Disable();
        m_videoDecoder.Close();
        m_videoRenderer.ClearToBlack();
        m_input.Close();
        m_url.clear();
        m_positionTicks = 0;
        m_open = false;
        m_paused = false;
    }

    int64_t PlaybackGraph::CurrentPositionTicks() const noexcept
    {
        return m_positionTicks;
    }

    void PlaybackGraph::RenderNextFrame()
    {
        if (auto frame = m_videoDecoder.TryReadFrame())
        {
            m_videoRenderer.Render(*frame);
            m_positionTicks = frame->PositionTicks;
            m_subtitleRenderer.RenderAt(m_positionTicks);
        }
    }
}
