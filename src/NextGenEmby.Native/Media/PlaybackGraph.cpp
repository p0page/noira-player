#include "pch.h"
#include "PlaybackGraph.h"

#include <chrono>
#include <utility>

namespace winrt::NextGenEmby::Native::implementation
{
    using namespace std::chrono_literals;

    PlaybackGraph::PlaybackGraph(
        DxDeviceResources& deviceResources,
        PlaybackGraphStateChangedHandler stateChanged)
        : m_deviceResources(deviceResources),
          m_graphStateChanged(std::move(stateChanged)),
          m_videoRenderer(deviceResources)
    {
    }

    PlaybackGraph::~PlaybackGraph()
    {
        Stop();
    }

    void PlaybackGraph::Open(NextGenEmby::Native::NativePlaybackOpenRequest const& request)
    {
        if (request == nullptr)
        {
            throw winrt::hresult_invalid_argument(L"Playback request is required.");
        }

        Stop();

        try
        {
            std::lock_guard lock(m_graphMutex);
            m_deviceResources.CreateDevice();
            m_input.Open(request.DirectStreamUrl());
            m_videoDecoder.Open(
                request.DirectStreamUrl(),
                0,
                m_deviceResources.Device(),
                m_deviceResources.Context());
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
            StartRenderLoop();
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
        std::lock_guard lock(m_graphMutex);
        if (m_open)
        {
            m_paused = true;
            m_audioRenderer.Pause();
            m_stateChanged.notify_all();
        }
    }

    void PlaybackGraph::Resume()
    {
        std::lock_guard lock(m_graphMutex);
        if (m_open)
        {
            m_paused = false;
            m_audioRenderer.Resume();
            m_stateChanged.notify_all();
        }
    }

    void PlaybackGraph::Seek(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Seek position cannot be negative.");
        }

        std::lock_guard lock(m_graphMutex);
        m_positionTicks = positionTicks;
        m_videoDecoder.Seek(positionTicks);
        RenderNextFrame();
        m_subtitleRenderer.RenderAt(m_positionTicks);
        m_stateChanged.notify_all();
    }

    void PlaybackGraph::Stop() noexcept
    {
        StopRenderLoop();

        std::lock_guard lock(m_graphMutex);
        m_audioRenderer.Stop();
        m_subtitleRenderer.Disable();
        m_videoDecoder.Close();
        m_videoRenderer.ClearToBlack();
        m_input.Close();
        m_url.clear();
        m_positionTicks = 0;
        m_open = false;
        m_paused = false;
        m_stopRenderLoop = false;
    }

    int64_t PlaybackGraph::CurrentPositionTicks() const noexcept
    {
        std::lock_guard lock(m_graphMutex);
        return m_positionTicks;
    }

    void PlaybackGraph::StartRenderLoop()
    {
        m_stopRenderLoop = false;
        m_renderThread = std::thread([this]()
        {
            RenderLoop();
        });
        m_stateChanged.notify_all();
    }

    void PlaybackGraph::StopRenderLoop() noexcept
    {
        {
            std::lock_guard lock(m_graphMutex);
            m_stopRenderLoop = true;
        }

        m_stateChanged.notify_all();

        if (m_renderThread.joinable())
        {
            m_renderThread.join();
        }
    }

    void PlaybackGraph::RenderLoop() noexcept
    {
        while (true)
        {
            {
                std::unique_lock lock(m_graphMutex);
                m_stateChanged.wait(lock, [this]()
                {
                    return m_stopRenderLoop || (m_open && !m_paused);
                });

                if (m_stopRenderLoop)
                {
                    return;
                }
            }

            try
            {
                bool reachedEnd = false;
                {
                    std::lock_guard lock(m_graphMutex);
                    if (m_stopRenderLoop)
                    {
                        return;
                    }

                    if (!m_open || m_paused)
                    {
                        continue;
                    }

                    if (!RenderNextFrame())
                    {
                        m_open = false;
                        m_stopRenderLoop = true;
                        reachedEnd = true;
                    }
                }

                if (reachedEnd)
                {
                    NotifyStateChanged(PlaybackGraphState::Stopped, L"Playback ended.");
                    return;
                }
            }
            catch (winrt::hresult_error const& error)
            {
                {
                    std::lock_guard lock(m_graphMutex);
                    m_open = false;
                    m_stopRenderLoop = true;
                }

                NotifyStateChanged(PlaybackGraphState::Failed, error.message());
                return;
            }
            catch (std::exception const& error)
            {
                {
                    std::lock_guard lock(m_graphMutex);
                    m_open = false;
                    m_stopRenderLoop = true;
                }

                NotifyStateChanged(PlaybackGraphState::Failed, winrt::to_hstring(error.what()));
                return;
            }
            catch (...)
            {
                {
                    std::lock_guard lock(m_graphMutex);
                    m_open = false;
                    m_stopRenderLoop = true;
                }

                NotifyStateChanged(PlaybackGraphState::Failed, L"Native render loop failed.");
                return;
            }

            std::this_thread::sleep_for(33ms);
        }
    }

    bool PlaybackGraph::RenderNextFrame()
    {
        if (auto frame = m_videoDecoder.TryReadFrame())
        {
            m_videoRenderer.Render(*frame);
            m_positionTicks = frame->PositionTicks;
            m_subtitleRenderer.RenderAt(m_positionTicks);
            return true;
        }

        return false;
    }

    void PlaybackGraph::NotifyStateChanged(
        PlaybackGraphState state,
        winrt::hstring const& message) const noexcept
    {
        if (!m_graphStateChanged)
        {
            return;
        }

        try
        {
            m_graphStateChanged(state, message);
        }
        catch (...)
        {
        }
    }
}
