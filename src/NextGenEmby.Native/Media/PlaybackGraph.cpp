#include "pch.h"
#include "PlaybackGraph.h"

namespace winrt::NextGenEmby::Native::implementation
{
    void PlaybackGraph::Open(NextGenEmby::Native::NativePlaybackOpenRequest const& request)
    {
        if (request == nullptr)
        {
            throw winrt::hresult_invalid_argument(L"Playback request is required.");
        }

        m_input.Open(request.DirectStreamUrl());
        m_url = request.DirectStreamUrl();
        m_positionTicks = request.StartPositionTicks();
        m_open = true;
        m_paused = false;
    }

    void PlaybackGraph::Pause()
    {
        if (m_open)
        {
            m_paused = true;
        }
    }

    void PlaybackGraph::Resume()
    {
        if (m_open)
        {
            m_paused = false;
        }
    }

    void PlaybackGraph::Seek(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Seek position cannot be negative.");
        }

        m_positionTicks = positionTicks;
    }

    void PlaybackGraph::Stop() noexcept
    {
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
}
