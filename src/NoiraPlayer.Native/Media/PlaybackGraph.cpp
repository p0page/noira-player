#include "pch.h"
#include "PlaybackGraph.h"
#include "FramePacing.h"
#include "../NativePlaybackDiagnostics.h"

#include <algorithm>
#include <chrono>
#include <string>
#include <utility>

namespace winrt::NoiraPlayer::Native::implementation
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

    void PlaybackGraph::Open(PlaybackGraphOpenRequest const& request)
    {
        AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open enter");
        if (request.DirectStreamUrl.empty())
        {
            throw winrt::hresult_invalid_argument(L"Playback direct stream URL is required.");
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
                std::to_wstring(request.DirectStreamUrl.size()));
            m_mediaSource.Open(request.DirectStreamUrl);
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
                request.AudioStreamIndex,
                request.HasAudioStreamIndex);
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioDecoder.Open end");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioRenderer.Open begin");
            m_audioRenderer.Open(request.AudioStreamIndex, request.HasAudioStreamIndex);
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioRenderer.Open end");
            auto subtitleStreamIndex = request.HasSubtitleStreamIndex
                ? std::optional<int32_t>{request.SubtitleStreamIndex}
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
            auto startPositionTicks = (std::max<int64_t>)(0, request.StartPositionTicks);
            if (startPositionTicks > 0)
            {
                AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open Seek startup begin");
                m_videoDecoder.Seek(startPositionTicks);
                m_audioDecoder.Flush(startPositionTicks);
                m_subtitleDecoder.Flush();
                SetVideoPrerollTarget(startPositionTicks);
                AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open Seek startup end");
            }

            m_url = request.DirectStreamUrl;
            m_positionTicks = startPositionTicks;
            auto sourceVideo = m_mediaSource.BestVideoStreamSnapshot();
            m_preferredVideoFrameRate = request.VideoFrameRate > 0.0
                ? request.VideoFrameRate
                : (sourceVideo ? sourceVideo->FrameRate : 0.0);
            m_hasSeenVideoFrameColor = false;
            m_requestedHdrOutput = false;
            m_hdrOutputActive = false;
            m_open = true;
            m_paused = false;
            ResetRuntimeStats();
            ApplyFramePacingPolicyMetrics();
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
            ResetAudioAheadWait();
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
            ResetVideoClock();
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
        ApplyFramePacingPolicyMetrics();
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
        ResetAudioAheadWait();
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

    std::optional<FfmpegVideoStreamSnapshot> PlaybackGraph::VideoSourceSnapshot() const
    {
        std::lock_guard lock(m_graphMutex);
        return m_mediaSource.BestVideoStreamSnapshot();
    }

    std::vector<FfmpegStreamSnapshot> PlaybackGraph::SourceTrackSnapshots() const
    {
        std::lock_guard lock(m_graphMutex);
        return m_mediaSource.StreamSnapshots();
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
        auto completedRenderLoopWaitReason = RenderLoopWaitReason::Default;
        auto completedRenderLoopWaitDurationMs = 0.0;
        auto completedRenderLoopWaitTargetMs = 0.0;

        while (true)
        {
            {
                std::unique_lock lock(m_graphMutex);
                m_stateChanged.wait(lock, [this]()
                {
                    return m_stopRenderLoop || (m_open && !m_paused);
                });

                m_lastCompletedRenderLoopWaitReason = completedRenderLoopWaitReason;
                if (completedRenderLoopWaitReason == RenderLoopWaitReason::AudioAhead)
                {
                    m_qualityMetrics.RecordAudioAheadWaitPassMs(completedRenderLoopWaitDurationMs, completedRenderLoopWaitTargetMs);
                }

                completedRenderLoopWaitReason = RenderLoopWaitReason::Default;
                completedRenderLoopWaitDurationMs = 0.0;
                completedRenderLoopWaitTargetMs = 0.0;

                if (m_stopRenderLoop)
                {
                    return;
                }
            }

            auto renderLoopWait = std::chrono::steady_clock::duration{PlaybackFramePacing::RenderLoopWait()};
            auto renderLoopWaitUseTimer = PlaybackFramePacing::ShouldUseRenderLoopTimer(renderLoopWait);
            auto renderLoopWaitReason = RenderLoopWaitReason::Default;

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

                    renderLoopWait = m_nextRenderLoopWait;
                    renderLoopWaitUseTimer = m_nextRenderLoopWaitUseTimer;
                    renderLoopWaitReason = m_nextRenderLoopWaitReason;
                    m_nextRenderLoopWait = PlaybackFramePacing::RenderLoopWait();
                    m_nextRenderLoopWaitUseTimer =
                        PlaybackFramePacing::ShouldUseRenderLoopTimer(m_nextRenderLoopWait);
                    m_nextRenderLoopWaitReason = RenderLoopWaitReason::Default;
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

            auto waitStartedAt = std::chrono::steady_clock::now();
            if (renderLoopWaitUseTimer)
            {
                m_renderLoopWaiter.WaitFor(renderLoopWait);
            }
            else
            {
                std::this_thread::sleep_for(renderLoopWait);
            }

            completedRenderLoopWaitReason = renderLoopWaitReason;
            completedRenderLoopWaitDurationMs = 0.0;
            completedRenderLoopWaitTargetMs = 0.0;
            if (renderLoopWaitReason == RenderLoopWaitReason::AudioAhead)
            {
                auto waitEndedAt = std::chrono::steady_clock::now();
                completedRenderLoopWaitDurationMs = std::chrono::duration<double, std::milli>(
                    waitEndedAt - waitStartedAt).count();
                completedRenderLoopWaitTargetMs = std::chrono::duration<double, std::milli>(renderLoopWait).count();
            }
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
                if (m_pendingVideoFrame->Texture.Get() != nullptr)
                {
                    ++m_qualityMetrics.HardwareDecodedVideoFrames;
                }
                else
                {
                    ++m_qualityMetrics.SoftwareDecodedVideoFrames;
                }
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
                ResetVideoClock();
                auto hasQueuedAudio = m_audioRenderer.QueuedBufferCount() > 0;
                if (PlaybackFramePacing::ShouldWaitForAudio(
                    frame.PositionTicks,
                    *audioPosition,
                    hasQueuedAudio))
                {
                    auto audioAheadWaitDuration = PlaybackFramePacing::AudioAheadWaitDuration(
                        frame.PositionTicks,
                        *audioPosition,
                        hasQueuedAudio);
                    if (!m_audioAheadWaitStartedAt)
                    {
                        m_audioAheadWaitStartedAt = std::chrono::steady_clock::now();
                        m_audioAheadWaitTargetMs =
                            std::chrono::duration<double, std::milli>(audioAheadWaitDuration).count();
                    }

                    m_nextRenderLoopWait = audioAheadWaitDuration;
                    m_nextRenderLoopWaitUseTimer = m_nextRenderLoopWait > std::chrono::steady_clock::duration::zero();
                    m_nextRenderLoopWaitReason = RenderLoopWaitReason::AudioAhead;
                    ++m_audioAheadWaitPassCount;

                    ++m_videoAheadWaitCount;
                    ++m_audioAheadWaitCount;
                    ++m_qualityMetrics.VideoAheadWaitCount;
                    ++m_qualityMetrics.AudioAheadWaitCount;
                    LogRuntimeStatsIfDue();
                    return true;
                }

                RecordAudioAheadWaitIfNeeded(frame.PositionTicks - *audioPosition);
                if (PlaybackFramePacing::ShouldDropLateFrame(
                    frame.PositionTicks,
                    *audioPosition,
                    hasQueuedAudio,
                    m_preferredVideoFrameRate))
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
            else
            {
                ResetAudioAheadWait();
                if (ShouldWaitForVideoClock(frame))
                {
                    ++m_videoAheadWaitCount;
                    ++m_videoClockWaitCount;
                    ++m_qualityMetrics.VideoAheadWaitCount;
                    ++m_qualityMetrics.VideoClockWaitCount;
                    LogRuntimeStatsIfDue();
                    return true;
                }
            }

            EnsureHdrOutputForFrame(frame);
            auto rendered = m_videoRenderer.Render(frame, m_hdrOutputActive);
            m_positionTicks = frame.PositionTicks;
            UpdateSubtitleCue();
            auto presentStartedAt = std::chrono::steady_clock::now();
            auto presented = m_deviceResources.Present();
            auto renderedAt = std::chrono::steady_clock::now();
            if (presented)
            {
                auto presentDuration = std::chrono::duration<double, std::milli>(
                    renderedAt - presentStartedAt).count();
                m_qualityMetrics.RecordPresentDurationMs(presentDuration);
            }

            if (rendered && presented)
            {
                if (m_lastRenderedFrameAt.time_since_epoch().count() != 0)
                {
                    auto elapsed = std::chrono::duration<double, std::milli>(
                        renderedAt - m_lastRenderedFrameAt).count();
                    m_qualityMetrics.RecordRenderIntervalMs(elapsed);
                    if (m_lastCompletedRenderLoopWaitReason == RenderLoopWaitReason::AudioAhead)
                    {
                        m_qualityMetrics.RecordRenderIntervalAfterAudioAheadWaitMs(elapsed);
                    }
                    else
                    {
                        m_qualityMetrics.RecordRenderIntervalAfterNonAudioWaitMs(elapsed);
                    }
                }

                m_lastRenderedFrameAt = renderedAt;
                ++m_renderedVideoFrameCount;
                ++m_qualityMetrics.RenderedVideoFrames;
            }

            m_pendingVideoFrame.reset();
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
        m_audioAheadWaitCount = 0;
        m_videoClockWaitCount = 0;
        m_videoStarvedPassCount = 0;
        m_audioStarvedPassCount = 0;
        m_seekPrerollDroppedVideoFrameCount = 0;
        m_qualityMetrics.Reset();
        m_lastRuntimeStatsLog = {};
        m_lastRenderedFrameAt = {};
        m_nextRenderLoopWaitReason = RenderLoopWaitReason::Default;
        m_lastCompletedRenderLoopWaitReason = RenderLoopWaitReason::Default;
        ResetAudioAheadWait();
        ResetVideoClock();
    }

    void PlaybackGraph::ResetVideoClock() noexcept
    {
        m_videoClockStartedAt = {};
        m_videoClockStartPositionTicks = 0;
    }

    void PlaybackGraph::ResetAudioAheadWait() noexcept
    {
        m_audioAheadWaitStartedAt.reset();
        m_audioAheadWaitTargetMs.reset();
        m_audioAheadWaitPassCount = 0;
    }

    void PlaybackGraph::RecordAudioAheadWaitIfNeeded(int64_t finalDeltaTicks) noexcept
    {
        if (!m_audioAheadWaitStartedAt)
        {
            return;
        }

        auto now = std::chrono::steady_clock::now();
        auto durationMs = std::chrono::duration<double, std::milli>(
            now - *m_audioAheadWaitStartedAt).count();
        auto finalDeltaMs = static_cast<double>(finalDeltaTicks) / 10000.0;
        m_qualityMetrics.RecordAudioAheadWaitMs(
            durationMs,
            m_audioAheadWaitTargetMs.value_or(0.0),
            finalDeltaMs,
            m_audioAheadWaitPassCount);
        ResetAudioAheadWait();
    }

    bool PlaybackGraph::ShouldWaitForVideoClock(DecodedVideoFrame const& frame)
    {
        auto now = std::chrono::steady_clock::now();
        if (m_videoClockStartedAt.time_since_epoch().count() == 0 ||
            frame.PositionTicks <= m_videoClockStartPositionTicks)
        {
            m_videoClockStartedAt = now;
            m_videoClockStartPositionTicks = frame.PositionTicks;
            return false;
        }

        auto elapsedTicks = static_cast<int64_t>(
            std::chrono::duration<double>(now - m_videoClockStartedAt).count() * 10000000.0);
        auto shouldWait = PlaybackFramePacing::ShouldWaitForVideoClock(
            frame.PositionTicks,
            m_videoClockStartPositionTicks,
            elapsedTicks);
        if (shouldWait)
        {
            m_nextRenderLoopWait = PlaybackFramePacing::VideoClockWaitDuration(
                frame.PositionTicks,
                m_videoClockStartPositionTicks,
                elapsedTicks);
            m_nextRenderLoopWaitUseTimer = m_nextRenderLoopWait > std::chrono::steady_clock::duration::zero();
            m_nextRenderLoopWaitReason = RenderLoopWaitReason::VideoClock;
        }

        return shouldWait;
    }

    void PlaybackGraph::ApplyFramePacingPolicyMetrics() noexcept
    {
        m_qualityMetrics.FramePacingSourceFrameRate = m_preferredVideoFrameRate;
        m_qualityMetrics.LateFrameDropToleranceMs =
            static_cast<double>(PlaybackFramePacing::LateFrameDropToleranceTicks(m_preferredVideoFrameRate)) / 10000.0;
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
            L" audioAheadWait=" + std::to_wstring(m_audioAheadWaitCount) +
            L" videoClockWait=" + std::to_wstring(m_videoClockWaitCount) +
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
