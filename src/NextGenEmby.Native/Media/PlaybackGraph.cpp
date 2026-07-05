#include "pch.h"
#include "PlaybackGraph.h"

#include <chrono>
#include <utility>

namespace winrt::NextGenEmby::Native::implementation
{
    using namespace std::chrono_literals;
    constexpr size_t MinimumQueuedAudioBuffers = 4;
    constexpr int64_t VideoAheadToleranceTicks = 400000;
    constexpr int64_t VideoDropToleranceTicks = 1000000;
    constexpr uint32_t MaxDroppedVideoFramesPerPass = 4;

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
            m_mediaSource.Open(request.DirectStreamUrl());
            m_videoDecoder.Open(
                m_mediaSource,
                0,
                m_deviceResources.Device(),
                m_deviceResources.Context());
            m_audioDecoder.Open(
                m_mediaSource,
                request.AudioStreamIndex(),
                request.HasAudioStreamIndex());
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
        m_pendingVideoFrame.reset();
        m_audioRenderer.Flush();
        m_videoDecoder.Seek(positionTicks);
        m_audioDecoder.Flush(positionTicks);
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
        m_audioDecoder.Close();
        m_videoDecoder.Close();
        m_videoRenderer.ClearToBlack();
        m_mediaSource.Close();
        m_pendingVideoFrame.reset();
        m_url.clear();
        m_positionTicks = 0;
        m_open = false;
        m_paused = false;
        m_stopRenderLoop = false;
    }

    void PlaybackGraph::SwitchAudioStream(int32_t audioStreamIndex)
    {
        std::lock_guard lock(m_graphMutex);
        if (!m_open)
        {
            throw winrt::hresult_error(E_FAIL, L"Playback is not open.");
        }

        auto shouldResumeAudio = !m_paused;
        m_audioRenderer.Stop();
        m_audioDecoder.Close();
        m_pendingVideoFrame.reset();
        m_videoDecoder.Seek(m_positionTicks);
        m_audioDecoder.Open(m_mediaSource, audioStreamIndex, true);
        m_audioDecoder.Flush(m_positionTicks);
        m_audioRenderer.Open(audioStreamIndex, true);
        if (shouldResumeAudio)
        {
            m_audioRenderer.Start();
        }

        m_stateChanged.notify_all();
    }

    void PlaybackGraph::SwitchSubtitleStream(std::optional<int32_t> subtitleStreamIndex)
    {
        std::lock_guard lock(m_graphMutex);
        if (!m_open)
        {
            throw winrt::hresult_error(E_FAIL, L"Playback is not open.");
        }

        if (subtitleStreamIndex.has_value())
        {
            m_subtitleRenderer.SwitchStream(subtitleStreamIndex.value());
        }
        else
        {
            m_subtitleRenderer.Disable();
        }
    }

    int64_t PlaybackGraph::CurrentPositionTicks() const noexcept
    {
        std::lock_guard lock(m_graphMutex);
        if (auto audioPosition = m_audioRenderer.CurrentPositionTicks())
        {
            return *audioPosition;
        }

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
        DecodeNextAudioFrame();

        auto droppedFrames = uint32_t{0};
        while (droppedFrames <= MaxDroppedVideoFramesPerPass)
        {
            if (!m_pendingVideoFrame)
            {
                auto frame = m_videoDecoder.TryReadFrame();
                if (!frame)
                {
                    return m_audioRenderer.QueuedBufferCount() > 0;
                }

                m_pendingVideoFrame = std::move(*frame);
            }

            auto const& frame = *m_pendingVideoFrame;
            if (auto audioPosition = m_audioRenderer.CurrentPositionTicks())
            {
                auto hasQueuedAudio = m_audioRenderer.QueuedBufferCount() > 0;
                if (hasQueuedAudio && frame.PositionTicks > *audioPosition + VideoAheadToleranceTicks)
                {
                    return true;
                }

                if (hasQueuedAudio && *audioPosition > frame.PositionTicks + VideoDropToleranceTicks)
                {
                    m_pendingVideoFrame.reset();
                    ++droppedFrames;
                    continue;
                }
            }

            m_videoRenderer.Render(frame);
            m_positionTicks = frame.PositionTicks;
            m_subtitleRenderer.RenderAt(m_positionTicks);
            m_pendingVideoFrame.reset();
            return true;
        }

        return true;
    }

    void PlaybackGraph::DecodeNextAudioFrame()
    {
        while (m_audioDecoder.IsOpen() &&
            m_audioRenderer.QueuedBufferCount() < MinimumQueuedAudioBuffers)
        {
            auto frame = m_audioDecoder.TryReadFrame();
            if (!frame)
            {
                return;
            }

            if (!m_audioRenderer.SubmitFrame(*frame))
            {
                return;
            }
        }
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
