#include "pch.h"
#include "PlaybackGraph.h"
#include "FramePacing.h"
#include "../NativePlaybackDiagnostics.h"

#include <algorithm>
#include <chrono>
#include <string>
#include <utility>

namespace winrt::NextGenEmby::Native::implementation
{
    using namespace std::chrono_literals;
    constexpr size_t MinimumQueuedAudioBuffers = 12;
    constexpr int64_t SeekPrerollToleranceTicks = 500000;
    constexpr uint32_t MaxDroppedVideoFramesPerPass = 4;
    constexpr uint32_t MaxSeekPrerollDroppedVideoFramesPerPass = 300;

    PlaybackGraph::PlaybackGraph(
        DxDeviceResources& deviceResources,
        PlaybackGraphStateChangedHandler stateChanged,
        PlaybackGraphHdrOutputChangedHandler hdrOutputChanged)
        : m_deviceResources(deviceResources),
          m_graphStateChanged(std::move(stateChanged)),
          m_hdrOutputChanged(std::move(hdrOutputChanged)),
          m_videoRenderer(deviceResources),
          m_subtitleRenderer(deviceResources)
    {
    }

    PlaybackGraph::~PlaybackGraph()
    {
        Stop();
    }

    void PlaybackGraph::Open(NextGenEmby::Native::NativePlaybackOpenRequest const& request)
    {
        AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open enter");
        if (request == nullptr)
        {
            throw winrt::hresult_invalid_argument(L"Playback request is required.");
        }

        AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open Stop begin");
        Stop();
        AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open Stop end");

        try
        {
            std::lock_guard lock(m_graphMutex);
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open lock acquired");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open CreateDevice begin");
            m_deviceResources.CreateDevice();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open CreateDevice end");
            AppendNativePlaybackDiagnostic(
                L"PlaybackGraph.Open MediaSource.Open begin urlLength=" +
                std::to_wstring(request.DirectStreamUrl().size()));
            m_mediaSource.Open(request.DirectStreamUrl());
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open MediaSource.Open end");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open VideoDecoder.Open begin");
            m_videoDecoder.Open(
                m_mediaSource,
                0,
                m_deviceResources.Device(),
                m_deviceResources.Context());
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open VideoDecoder.Open end");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioDecoder.Open begin");
            m_audioDecoder.Open(
                m_mediaSource,
                request.AudioStreamIndex(),
                request.HasAudioStreamIndex());
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioDecoder.Open end");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioRenderer.Open begin");
            m_audioRenderer.Open(request.AudioStreamIndex(), request.HasAudioStreamIndex());
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioRenderer.Open end");
            auto subtitleStreamIndex = request.HasSubtitleStreamIndex()
                ? std::optional<int32_t>{request.SubtitleStreamIndex()}
                : std::nullopt;
            if (subtitleStreamIndex)
            {
                AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open SubtitleDecoder.Open begin");
                m_subtitleDecoder.Open(m_mediaSource, *subtitleStreamIndex);
                AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open SubtitleDecoder.Open end");
            }

            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open SubtitleRenderer.Open begin");
            m_subtitleRenderer.Open(subtitleStreamIndex);
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open SubtitleRenderer.Open end");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open ClearToBlack begin");
            m_videoRenderer.ClearToBlack();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open ClearToBlack end");
            auto startPositionTicks = (std::max<int64_t>)(0, request.StartPositionTicks());
            if (startPositionTicks > 0)
            {
                AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open Seek startup begin");
                m_videoDecoder.Seek(startPositionTicks);
                m_audioDecoder.Flush(startPositionTicks);
                m_subtitleDecoder.Flush();
                SetVideoPrerollTarget(startPositionTicks);
                AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open Seek startup end");
            }

            m_url = request.DirectStreamUrl();
            m_positionTicks = startPositionTicks;
            m_preferredVideoFrameRate = request.VideoFrameRate();
            m_hasSeenVideoFrameColor = false;
            m_requestedHdrOutput = false;
            m_hdrOutputActive = false;
            m_open = true;
            m_paused = false;
            ResetRuntimeStats();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open RenderNextFrame begin");
            auto renderedFirstFrame = RenderNextFrame();
            AppendNativePlaybackDiagnostic(renderedFirstFrame
                ? L"PlaybackGraph.Open RenderNextFrame end true"
                : L"PlaybackGraph.Open RenderNextFrame end false");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open StartRenderLoop begin");
            StartRenderLoop();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open StartRenderLoop end");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioRenderer.Start begin");
            m_audioRenderer.Start();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioRenderer.Start end");
        }
        catch (...)
        {
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open exception begin Stop");
            Stop();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open exception end Stop");
            throw;
        }

        AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open success end");
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
        ResetRuntimeStats();
        m_audioRenderer.Flush();
        m_videoDecoder.Seek(positionTicks);
        m_audioDecoder.Flush(positionTicks);
        m_subtitleDecoder.Flush();
        m_subtitleRenderer.ClearCue();
        SetVideoPrerollTarget(positionTicks);
        RenderNextFrame();
        m_stateChanged.notify_all();
    }

    void PlaybackGraph::Stop() noexcept
    {
        StopRenderLoop();

        std::lock_guard lock(m_graphMutex);
        m_audioRenderer.Stop();
        m_subtitleRenderer.Disable();
        m_subtitleDecoder.Close();
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
        m_videoPrerollTargetTicks.reset();
        ResetRuntimeStats();
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
        SetVideoPrerollTarget(m_positionTicks);
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
            m_subtitleDecoder.Close();
            m_subtitleRenderer.Disable();
            m_subtitleDecoder.Open(m_mediaSource, subtitleStreamIndex.value());
            m_subtitleRenderer.SwitchStream(subtitleStreamIndex.value());
        }
        else
        {
            m_subtitleDecoder.Close();
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

    PlaybackQualityMetricsSnapshot PlaybackGraph::QualityMetricsSnapshot() const noexcept
    {
        std::lock_guard lock(m_graphMutex);
        auto snapshot = m_qualityMetrics.Snapshot();
        if (auto audioPosition = m_audioRenderer.CurrentPositionTicks())
        {
            snapshot.AudioClockTicks = *audioPosition;
        }

        snapshot.VideoPositionTicks = m_positionTicks;
        snapshot.QueuedAudioBuffers = m_audioRenderer.QueuedBufferCount();
        return snapshot;
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

            std::this_thread::sleep_for(PlaybackFramePacing::RenderLoopWait());
        }
    }

    bool PlaybackGraph::RenderNextFrame()
    {
        ++m_renderPassCount;
        ++m_qualityMetrics.RenderPasses;
        DecodeNextAudioFrame();

        auto droppedFrames = uint32_t{0};
        auto maxDroppedFramesThisPass = m_videoPrerollTargetTicks
            ? MaxSeekPrerollDroppedVideoFramesPerPass
            : MaxDroppedVideoFramesPerPass;
        while (droppedFrames <= maxDroppedFramesThisPass)
        {
            if (!m_pendingVideoFrame)
            {
                auto frame = m_videoDecoder.TryReadFrame();
                if (!frame)
                {
                    auto hasQueuedAudio = m_audioRenderer.QueuedBufferCount() > 0;
                    if (hasQueuedAudio)
                    {
                        ++m_videoStarvedPassCount;
                        ++m_qualityMetrics.VideoStarvedPasses;
                    }
                    else
                    {
                        ++m_audioStarvedPassCount;
                        ++m_qualityMetrics.AudioStarvedPasses;
                    }

                    LogRuntimeStatsIfDue();
                    return hasQueuedAudio;
                }

                m_pendingVideoFrame = std::move(*frame);
                ++m_decodedVideoFrameCount;
                ++m_qualityMetrics.DecodedVideoFrames;
            }

            auto const& frame = *m_pendingVideoFrame;
            if (m_videoPrerollTargetTicks &&
                frame.PositionTicks + SeekPrerollToleranceTicks < *m_videoPrerollTargetTicks)
            {
                m_pendingVideoFrame.reset();
                ++droppedFrames;
                ++m_droppedVideoFrameCount;
                ++m_seekPrerollDroppedVideoFrameCount;
                ++m_qualityMetrics.DroppedVideoFrames;
                ++m_qualityMetrics.SeekPrerollDroppedFrames;
                continue;
            }

            if (m_videoPrerollTargetTicks)
            {
                AppendNativePlaybackDiagnostic(
                    L"PlaybackGraph.SeekPreroll reached target=" + std::to_wstring(*m_videoPrerollTargetTicks) +
                    L" frame=" + std::to_wstring(frame.PositionTicks) +
                    L" dropped=" + std::to_wstring(m_seekPrerollDroppedVideoFrameCount));
                m_videoPrerollTargetTicks.reset();
            }

            if (auto audioPosition = m_audioRenderer.CurrentPositionTicks())
            {
                auto hasQueuedAudio = m_audioRenderer.QueuedBufferCount() > 0;
                if (PlaybackFramePacing::ShouldWaitForAudio(
                    frame.PositionTicks,
                    *audioPosition,
                    hasQueuedAudio))
                {
                    ++m_videoAheadWaitCount;
                    ++m_qualityMetrics.VideoAheadWaitCount;
                    LogRuntimeStatsIfDue();
                    return true;
                }

                if (PlaybackFramePacing::ShouldDropLateFrame(
                    frame.PositionTicks,
                    *audioPosition,
                    hasQueuedAudio))
                {
                    m_pendingVideoFrame.reset();
                    ++droppedFrames;
                    ++m_droppedVideoFrameCount;
                    ++m_qualityMetrics.DroppedVideoFrames;
                    continue;
                }

                m_qualityMetrics.AudioClockTicks = *audioPosition;
                m_qualityMetrics.VideoPositionTicks = frame.PositionTicks;
                m_qualityMetrics.RecordAudioVideoDriftTicks(frame.PositionTicks - *audioPosition);
            }

            EnsureHdrOutputForFrame(frame);
            m_videoRenderer.Render(frame, m_hdrOutputActive);
            m_positionTicks = frame.PositionTicks;
            UpdateSubtitleCue();
            m_deviceResources.Present();
            auto renderedAt = std::chrono::steady_clock::now();
            if (m_lastRenderedFrameAt.time_since_epoch().count() != 0)
            {
                auto elapsed = std::chrono::duration<double, std::milli>(
                    renderedAt - m_lastRenderedFrameAt).count();
                m_qualityMetrics.RecordRenderIntervalMs(elapsed);
            }

            m_lastRenderedFrameAt = renderedAt;
            m_pendingVideoFrame.reset();
            ++m_renderedVideoFrameCount;
            ++m_qualityMetrics.RenderedVideoFrames;
            m_qualityMetrics.QueuedAudioBuffers = m_audioRenderer.QueuedBufferCount();
            LogRuntimeStatsIfDue();
            return true;
        }

        LogRuntimeStatsIfDue();
        return true;
    }

    void PlaybackGraph::EnsureHdrOutputForFrame(DecodedVideoFrame const& frame)
    {
        auto decision = ResolveHdrOutputDecisionForFrame(
            m_hasSeenVideoFrameColor,
            m_requestedHdrOutput,
            frame.HdrKind,
            m_deviceResources.IsTenBitSwapChain());
        m_hasSeenVideoFrameColor = true;

        if (!decision.ShouldRequestDisplayChange)
        {
            return;
        }

        m_requestedHdrOutput = decision.DesiredHdrOutput;
        if (m_hdrOutputChanged)
        {
            AppendNativePlaybackDiagnostic(decision.DesiredHdrOutput
                ? L"PlaybackGraph.EnsureHdrOutputForFrame request HDR begin"
                : L"PlaybackGraph.EnsureHdrOutputForFrame request SDR begin");
            m_hdrOutputActive = m_hdrOutputChanged(
                decision.DesiredHdrOutput,
                m_preferredVideoFrameRate);
            AppendNativePlaybackDiagnostic(m_hdrOutputActive
                ? L"PlaybackGraph.EnsureHdrOutputForFrame request end active"
                : L"PlaybackGraph.EnsureHdrOutputForFrame request end inactive");
        }
        else
        {
            m_hdrOutputActive = false;
        }
    }

    uint32_t PlaybackGraph::DecodeNextAudioFrame()
    {
        auto submittedFrames = uint32_t{0};
        while (m_audioDecoder.IsOpen() &&
            m_audioRenderer.QueuedBufferCount() < MinimumQueuedAudioBuffers)
        {
            auto frame = m_audioDecoder.TryReadFrame();
            if (!frame)
            {
                break;
            }

            if (!m_audioRenderer.SubmitFrame(*frame))
            {
                break;
            }

            ++submittedFrames;
        }

        m_submittedAudioFrameCount += submittedFrames;
        m_qualityMetrics.SubmittedAudioFrames += submittedFrames;
        return submittedFrames;
    }

    void PlaybackGraph::UpdateSubtitleCue()
    {
        m_subtitleDecoder.PumpQueuedPackets();
        if (auto cue = m_subtitleDecoder.TryGetCueAt(m_positionTicks))
        {
            m_subtitleRenderer.SetTextCue(cue->Text, cue->StartTicks, cue->EndTicks);
        }
        else
        {
            m_subtitleRenderer.ClearCue();
        }

        m_subtitleRenderer.RenderAt(m_positionTicks);
    }

    void PlaybackGraph::ResetRuntimeStats() noexcept
    {
        m_renderPassCount = 0;
        m_renderedVideoFrameCount = 0;
        m_decodedVideoFrameCount = 0;
        m_submittedAudioFrameCount = 0;
        m_droppedVideoFrameCount = 0;
        m_videoAheadWaitCount = 0;
        m_videoStarvedPassCount = 0;
        m_audioStarvedPassCount = 0;
        m_seekPrerollDroppedVideoFrameCount = 0;
        m_qualityMetrics.Reset();
        m_lastRuntimeStatsLog = {};
        m_lastRenderedFrameAt = {};
    }

    void PlaybackGraph::SetVideoPrerollTarget(int64_t targetTicks) noexcept
    {
        if (targetTicks > 0)
        {
            m_videoPrerollTargetTicks = targetTicks;
        }
        else
        {
            m_videoPrerollTargetTicks.reset();
        }
    }

    void PlaybackGraph::LogRuntimeStatsIfDue()
    {
        auto now = std::chrono::steady_clock::now();
        if (m_lastRuntimeStatsLog.time_since_epoch().count() != 0 &&
            now - m_lastRuntimeStatsLog < 1s)
        {
            return;
        }

        m_lastRuntimeStatsLog = now;
        auto audioPosition = m_audioRenderer.CurrentPositionTicks();
        auto queuedAudio = m_audioRenderer.QueuedBufferCount();
        AppendNativePlaybackDiagnostic(
            L"PlaybackGraph.Stats position=" + std::to_wstring(m_positionTicks) +
            L" audioPosition=" + (audioPosition ? std::to_wstring(*audioPosition) : std::wstring(L"none")) +
            L" queuedAudio=" + std::to_wstring(queuedAudio) +
            L" pendingVideo=" + std::to_wstring(m_pendingVideoFrame ? 1 : 0) +
            L" passes=" + std::to_wstring(m_renderPassCount) +
            L" decodedVideo=" + std::to_wstring(m_decodedVideoFrameCount) +
            L" renderedVideo=" + std::to_wstring(m_renderedVideoFrameCount) +
            L" submittedAudio=" + std::to_wstring(m_submittedAudioFrameCount) +
            L" droppedVideo=" + std::to_wstring(m_droppedVideoFrameCount) +
            L" seekPrerollDropped=" + std::to_wstring(m_seekPrerollDroppedVideoFrameCount) +
            L" seekPrerollTarget=" + (m_videoPrerollTargetTicks ? std::to_wstring(*m_videoPrerollTargetTicks) : std::wstring(L"none")) +
            L" videoAheadWait=" + std::to_wstring(m_videoAheadWaitCount) +
            L" videoStarved=" + std::to_wstring(m_videoStarvedPassCount) +
            L" audioStarved=" + std::to_wstring(m_audioStarvedPassCount));
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
