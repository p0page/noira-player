#include "pch.h"
#include "PlaybackGraph.h"
#include "FramePacing.h"
#include "SubtitleSwitchTransaction.h"
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

    class ScopeExit final
    {
    public:
        explicit ScopeExit(std::function<void()> action) : m_action(std::move(action)) {}
        ~ScopeExit() noexcept
        {
            if (m_action)
            {
                m_action();
            }
        }

        ScopeExit(ScopeExit const&) = delete;
        ScopeExit& operator=(ScopeExit const&) = delete;

    private:
        std::function<void()> m_action;
    };

    PlaybackTransportCallMetrics ToPlaybackTransportCallMetrics(
        FfmpegTransportCallSnapshot const& snapshot) noexcept
    {
        return PlaybackTransportCallMetrics{
            snapshot.ReadCalls,
            snapshot.SeekCalls,
            snapshot.ReadWaitMs,
            snapshot.SeekWaitMs,
            snapshot.SeekDistanceBytes,
            snapshot.SizeQueryCalls,
            snapshot.DataSeekCalls,
            snapshot.ForwardDataSeekCalls,
            snapshot.BackwardDataSeekCalls,
            snapshot.NoOpDataSeekCalls,
            snapshot.SizeQueryWaitMs,
            snapshot.DataSeekWaitMs,
            snapshot.ForwardDataSeekWaitMs,
            snapshot.BackwardDataSeekWaitMs,
            snapshot.NoOpDataSeekWaitMs,
            snapshot.DataSeekDistanceBytes,
            snapshot.ForwardDataSeekDistanceBytes,
            snapshot.BackwardDataSeekDistanceBytes,
            snapshot.Provider,
            snapshot.EvidenceAvailable};
    }

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
        auto graphOpenStartedAt = std::chrono::steady_clock::now();
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
            m_lastVideoSourceSnapshot.reset();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open lock acquired");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open CreateDevice begin");
            m_deviceResources.CreateDevice();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open CreateDevice end");
            AppendNativePlaybackDiagnostic(
                L"PlaybackGraph.Open MediaSource.Open begin urlLength=" +
                std::to_wstring(request.DirectStreamUrl.size()));
            m_mediaSource.Open(request.DirectStreamUrl);
            m_lastVideoSourceSnapshot = m_mediaSource.BestVideoStreamSnapshot();
            auto sourceTracks = m_mediaSource.StreamSnapshots();
            std::vector<int32_t> switchCacheStreams;
            for (auto const& track : sourceTracks)
            {
                if (track.Kind == "Audio" || track.Kind == "Subtitle")
                {
                    switchCacheStreams.push_back(track.StreamIndex);
                }
            }
            if (request.EnableSwitchPacketCache)
            {
                m_mediaSource.ConfigureSwitchPacketCache(switchCacheStreams);
            }
            m_switchPacketCacheEnabled = request.EnableSwitchPacketCache;
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open MediaSource.Open end");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open VideoDecoder.Open begin");
            m_videoDecoder.Open(
                m_mediaSource,
                0,
                m_deviceResources.Device(),
                m_deviceResources.Context());
            if (m_lastVideoSourceSnapshot)
            {
                m_lastVideoSourceSnapshot = EnrichVideoSourceSnapshot(*m_lastVideoSourceSnapshot);
            }
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open VideoDecoder.Open end");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioDecoder.Open begin");
            m_audioDecoder.Open(
                m_mediaSource,
                request.AudioStreamIndex,
                request.HasAudioStreamIndex);
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioDecoder.Open end");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioRenderer.Open begin");
            auto selectedAudioStreamIndex = m_audioDecoder.SelectedStreamIndex();
            m_audioRenderer.Open(
                selectedAudioStreamIndex.value_or(0),
                selectedAudioStreamIndex.has_value());
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
            m_mediaSource.ConfigureSeekReplayCache(
                request.EnableSeekPacketCache,
                m_lastVideoSourceSnapshot
                    ? m_lastVideoSourceSnapshot->StreamIndex
                    : -1);
            m_lastSeekReplaySnapshot = {};
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open ClearToBlack begin");
            m_videoRenderer.ClearToBlack();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open ClearToBlack end");
            auto startPositionTicks = (std::max<int64_t>)(0, request.StartPositionTicks);
            auto startupSeekDurationMs = 0.0;
            auto startupSeekBytesRead = uint64_t{0};
            auto const transportCallsBeforeStartupSeek = m_mediaSource.TransportCallSnapshot();
            if (startPositionTicks > 0)
            {
                AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open Seek startup begin");
                auto const startupSeekStartedAt = std::chrono::steady_clock::now();
                auto const transportBytesBeforeStartupSeek = m_mediaSource.TransportBytesRead();
                m_videoDecoder.Seek(startPositionTicks);
                auto const transportBytesAfterStartupSeek = m_mediaSource.TransportBytesRead();
                if (transportBytesAfterStartupSeek >= transportBytesBeforeStartupSeek)
                {
                    startupSeekBytesRead = transportBytesAfterStartupSeek - transportBytesBeforeStartupSeek;
                }
                else
                {
                    AppendNativePlaybackDiagnostic(
                        L"PlaybackGraph.Open startup seek transport byte counter regressed before=" +
                        std::to_wstring(transportBytesBeforeStartupSeek) +
                        L" after=" + std::to_wstring(transportBytesAfterStartupSeek));
                }
                m_audioDecoder.Flush(startPositionTicks);
                m_subtitleDecoder.Flush();
                SetVideoPrerollTarget(startPositionTicks);
                startupSeekDurationMs = std::chrono::duration<double, std::milli>(
                    std::chrono::steady_clock::now() - startupSeekStartedAt).count();
                AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open Seek startup end");
            }
            auto const startupSeekTransportCalls = SubtractTransportCallSnapshots(
                transportCallsBeforeStartupSeek,
                m_mediaSource.TransportCallSnapshot());

            m_url = request.DirectStreamUrl;
            m_positionTicks = startPositionTicks;
            m_positionSnapshotTicks.store(startPositionTicks, std::memory_order_relaxed);
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
            auto ffmpegOpenTiming = m_mediaSource.OpenTimingSnapshot();
            m_qualityMetrics.FfmpegOpenInputDurationMs = ffmpegOpenTiming.OpenInputDurationMs;
            m_qualityMetrics.FfmpegStreamInfoDurationMs = ffmpegOpenTiming.StreamInfoDurationMs;
            m_qualityMetrics.NativeStartupSeekDurationMs = startupSeekDurationMs;
            m_qualityMetrics.FfmpegOpenInputBytesRead = ffmpegOpenTiming.OpenInputBytesRead;
            m_qualityMetrics.FfmpegStreamInfoBytesRead = ffmpegOpenTiming.StreamInfoBytesRead;
            m_qualityMetrics.NativeStartupSeekBytesRead = startupSeekBytesRead;
            m_qualityMetrics.StartupTransportProvider = ffmpegOpenTiming.OpenInputTransportCalls.Provider;
            m_qualityMetrics.StartupTransportCallEvidenceAvailable =
                ffmpegOpenTiming.OpenInputTransportCalls.EvidenceAvailable;
            m_qualityMetrics.FfmpegOpenInputTransportCalls =
                ToPlaybackTransportCallMetrics(ffmpegOpenTiming.OpenInputTransportCalls);
            m_qualityMetrics.FfmpegStreamInfoTransportCalls =
                ToPlaybackTransportCallMetrics(ffmpegOpenTiming.StreamInfoTransportCalls);
            m_qualityMetrics.NativeStartupSeekTransportCalls =
                ToPlaybackTransportCallMetrics(startupSeekTransportCalls);
            auto timeline = m_mediaSource.TimelineSnapshot(sourceVideo ? sourceVideo->StreamIndex : -1);
            m_qualityMetrics.ContainerStartTimeTicks = timeline.ContainerStartTimeTicks;
            m_qualityMetrics.VideoStreamStartTimeTicks = timeline.StreamStartTimeTicks;
            m_qualityMetrics.SeekDemuxTargetTicks = timeline.LastSeekDemuxTargetTicks;
            ApplyFramePacingPolicyMetrics();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open RenderNextFrame begin");
            auto const readTimingBeforeFirstFrame = m_mediaSource.ReadTimingSnapshot();
            auto const transportBytesBeforeFirstFrame = m_mediaSource.TransportBytesRead();
            auto const transportCallsBeforeFirstFrame = m_mediaSource.TransportCallSnapshot();
            auto const firstFrameStartedAt = std::chrono::steady_clock::now();
            auto renderedFirstFrame = RenderNextFrame();
            m_qualityMetrics.NativeFirstFrameDurationMs =
                std::chrono::duration<double, std::milli>(
                    std::chrono::steady_clock::now() - firstFrameStartedAt).count();
            auto const readTimingAfterFirstFrame = m_mediaSource.ReadTimingSnapshot();
            auto const transportBytesAfterFirstFrame = m_mediaSource.TransportBytesRead();
            auto const transportCallsAfterFirstFrame = m_mediaSource.TransportCallSnapshot();
            m_qualityMetrics.NativeFirstFrameTransportCalls = ToPlaybackTransportCallMetrics(
                SubtractTransportCallSnapshots(
                    transportCallsBeforeFirstFrame,
                    transportCallsAfterFirstFrame));
            if (transportBytesAfterFirstFrame >= transportBytesBeforeFirstFrame)
            {
                m_qualityMetrics.NativeFirstFrameTransportBytesRead =
                    transportBytesAfterFirstFrame - transportBytesBeforeFirstFrame;
            }
            else
            {
                AppendNativePlaybackDiagnostic(
                    L"PlaybackGraph.Open first frame transport byte counter regressed before=" +
                    std::to_wstring(transportBytesBeforeFirstFrame) +
                    L" after=" + std::to_wstring(transportBytesAfterFirstFrame));
            }
            m_qualityMetrics.NativeFirstFrameDemuxReadDurationMs = (std::max)(
                0.0,
                readTimingAfterFirstFrame.ReadFrameDurationMs -
                    readTimingBeforeFirstFrame.ReadFrameDurationMs);
            m_qualityMetrics.NativeFirstFrameDemuxPacketCount =
                readTimingAfterFirstFrame.PacketCount - readTimingBeforeFirstFrame.PacketCount;
            m_qualityMetrics.NativeFirstFrameDemuxBytes =
                readTimingAfterFirstFrame.Bytes - readTimingBeforeFirstFrame.Bytes;
            m_playbackReadTimingBaseline = readTimingAfterFirstFrame;
            m_playbackTransportCallBaseline = transportCallsAfterFirstFrame;
            m_hasPlaybackReadBaseline = true;
            m_qualityMetrics.NativeFirstFramePresentDurationMs =
                m_qualityMetrics.Snapshot().PresentDurationMsMax;
            AppendNativePlaybackDiagnostic(renderedFirstFrame
                ? L"PlaybackGraph.Open RenderNextFrame end true"
                : L"PlaybackGraph.Open RenderNextFrame end false");
            StartVideoDecodeWorkerOrFallback();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open StartRenderLoop begin");
            StartRenderLoop();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open StartRenderLoop end");
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioRenderer.Start begin");
            m_audioRenderer.Start();
            AppendNativePlaybackDiagnostic(L"PlaybackGraph.Open AudioRenderer.Start end");
            m_qualityMetrics.NativeGraphOpenDurationMs =
                std::chrono::duration<double, std::milli>(
                    std::chrono::steady_clock::now() - graphOpenStartedAt).count();
        }
        catch (...)
        {
            {
                std::lock_guard lock(m_graphMutex);
                auto source = m_mediaSource.BestVideoStreamSnapshot();
                if (source)
                {
                    m_lastVideoSourceSnapshot = EnrichVideoSourceSnapshot(*source);
                }
            }
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
            m_renderIntervalTracker.BreakContinuity();
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
            m_renderIntervalTracker.BreakContinuity();
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

        PlaybackGraphSeekTiming timing;
        auto const lockStartedAt = std::chrono::steady_clock::now();
        std::unique_lock lock(m_graphMutex);
        timing.LockWaitDurationMs = std::chrono::duration<double, std::milli>(
            std::chrono::steady_clock::now() - lockStartedAt).count();
        auto const executionStartedAt = std::chrono::steady_clock::now();

        auto const quiesceStartedAt = std::chrono::steady_clock::now();
        auto restartVideoDecodeWorker = StopVideoDecodeWorkerForMutation();
        timing.QuiesceDurationMs = std::chrono::duration<double, std::milli>(
            std::chrono::steady_clock::now() - quiesceStartedAt).count();
        ScopeExit restartVideoDecodeWorkerOnExit([this, restartVideoDecodeWorker, executionStartedAt, &timing]()
        {
            auto const restartStartedAt = std::chrono::steady_clock::now();
            RestartVideoDecodeWorkerAfterMutation(restartVideoDecodeWorker);
            timing.WorkerRestartDurationMs = std::chrono::duration<double, std::milli>(
                std::chrono::steady_clock::now() - restartStartedAt).count();
            timing.ExecutionDurationMs = std::chrono::duration<double, std::milli>(
                std::chrono::steady_clock::now() - executionStartedAt).count();
            m_lastSeekTimingSnapshot = timing;
        });
        auto const previousPositionTicks = m_positionTicks;
        auto const replayPreparationStartedAt = std::chrono::steady_clock::now();
        m_lastSeekReplaySnapshot = m_mediaSource.TryPrepareSeekReplay(
            positionTicks,
            previousPositionTicks);
        timing.ReplayPreparationDurationMs = std::chrono::duration<double, std::milli>(
            std::chrono::steady_clock::now() - replayPreparationStartedAt).count();

        auto const stateResetStartedAt = std::chrono::steady_clock::now();
        m_positionTicks = positionTicks;
        m_positionSnapshotTicks.store(positionTicks, std::memory_order_relaxed);
        m_pendingVideoFrame.reset();
        ResetRuntimeStats(true);
        m_seekPresentationTracker.BeginSeek(m_renderedVideoFrameCount);
        ApplyFramePacingPolicyMetrics();
        m_audioRenderer.Flush();
        timing.StateResetDurationMs = std::chrono::duration<double, std::milli>(
            std::chrono::steady_clock::now() - stateResetStartedAt).count();

        auto const mediaRepositionStartedAt = std::chrono::steady_clock::now();
        if (m_lastSeekReplaySnapshot.Hit)
        {
            m_videoDecoder.Flush(positionTicks);
        }
        else
        {
            m_videoDecoder.Seek(positionTicks);
        }
        timing.MediaRepositionDurationMs = std::chrono::duration<double, std::milli>(
            std::chrono::steady_clock::now() - mediaRepositionStartedAt).count();
        auto sourceVideo = m_mediaSource.BestVideoStreamSnapshot();
        auto timeline = m_mediaSource.TimelineSnapshot(sourceVideo ? sourceVideo->StreamIndex : -1);
        m_qualityMetrics.SeekDemuxTargetTicks =
            m_lastSeekReplaySnapshot.Hit ? -1 : timeline.LastSeekDemuxTargetTicks;

        auto const dependentDecoderFlushStartedAt = std::chrono::steady_clock::now();
        m_audioDecoder.Flush(positionTicks);
        m_subtitleDecoder.Flush();
        m_subtitleRenderer.ClearCue();
        timing.DependentDecoderFlushDurationMs = std::chrono::duration<double, std::milli>(
            std::chrono::steady_clock::now() - dependentDecoderFlushStartedAt).count();

        auto const prerollRenderStartedAt = std::chrono::steady_clock::now();
        SetVideoPrerollTarget(positionTicks);
        RenderNextFrame();
        timing.PrerollRenderDurationMs = std::chrono::duration<double, std::milli>(
            std::chrono::steady_clock::now() - prerollRenderStartedAt).count();
        m_stateChanged.notify_all();
    }

    void PlaybackGraph::Stop() noexcept
    {
        StopRenderLoop();

        std::lock_guard lock(m_graphMutex);
        if (m_videoDecodeWorker)
        {
            m_videoDecodeWorker->Stop();
            m_videoDecodeWorker.reset();
        }
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
        m_positionSnapshotTicks.store(0, std::memory_order_relaxed);
        m_open = false;
        m_switchPacketCacheEnabled = false;
        m_lastSeekReplaySnapshot = {};
        m_paused = false;
        m_stopRenderLoop = false;
        m_videoPrerollTargetTicks.reset();
        m_seekPresentationTracker.Reset();
        ResetRuntimeStats();
    }

    PlaybackGraphSwitchTiming PlaybackGraph::SwitchAudioStream(int32_t audioStreamIndex)
    {
        auto const requestedAt = std::chrono::steady_clock::now();
        std::unique_lock lock(m_graphMutex);
        auto const acquiredAt = std::chrono::steady_clock::now();
        PlaybackGraphSwitchTiming timing;
        timing.PacketCacheEnabled = m_switchPacketCacheEnabled;
        timing.LockWaitDurationMs =
            std::chrono::duration<double, std::milli>(acquiredAt - requestedAt).count();
        if (!m_open)
        {
            throw winrt::hresult_error(E_FAIL, L"Playback is not open.");
        }

        auto restartVideoDecodeWorker = StopVideoDecodeWorkerForMutation();
        ScopeExit restartVideoDecodeWorkerOnExit([this, restartVideoDecodeWorker]()
        {
            RestartVideoDecodeWorkerAfterMutation(restartVideoDecodeWorker);
        });

        auto shouldResumeAudio = !m_paused;
        m_audioRenderer.Stop();
        m_audioDecoder.Close();
        m_pendingVideoFrame.reset();
        ResetAudioAheadWait();
        auto const quiescedAt = std::chrono::steady_clock::now();
        timing.QuiesceDurationMs =
            std::chrono::duration<double, std::milli>(quiescedAt - acquiredAt).count();
        auto const packetCache = m_mediaSource.SwitchPacketCacheSnapshot(
            audioStreamIndex,
            m_positionTicks,
            true);
        auto const useSwitchPacketCache = packetCache.HasCoverage;
        timing.PacketCacheHit = packetCache.HasCoverage;
        timing.PacketCachePacketCount = packetCache.PacketCount;
        timing.PacketCacheBytes = packetCache.Bytes;
        timing.PacketCacheWindowDurationTicks = packetCache.WindowDurationTicks;
        if (!useSwitchPacketCache)
        {
            auto const seekStartedAt = std::chrono::steady_clock::now();
            m_videoDecoder.Seek(m_positionTicks);
            SetVideoPrerollTarget(m_positionTicks);
            timing.SeekDurationMs =
                std::chrono::duration<double, std::milli>(
                    std::chrono::steady_clock::now() - seekStartedAt).count();
        }
        auto const decoderStartedAt = std::chrono::steady_clock::now();
        m_audioDecoder.Open(m_mediaSource, audioStreamIndex, true);
        auto selectedAudioStreamIndex = m_audioDecoder.SelectedStreamIndex();
        if (!selectedAudioStreamIndex.has_value() ||
            selectedAudioStreamIndex.value() != audioStreamIndex)
        {
            m_audioDecoder.Close();
            throw winrt::hresult_error(E_FAIL, L"Requested audio stream was not selected.");
        }

        m_audioDecoder.Flush(m_positionTicks);
        auto const decoderOpenedAt = std::chrono::steady_clock::now();
        timing.DecoderOpenDurationMs =
            std::chrono::duration<double, std::milli>(decoderOpenedAt - decoderStartedAt).count();
        m_audioRenderer.Open(selectedAudioStreamIndex.value(), true);
        if (shouldResumeAudio)
        {
            m_audioRenderer.Start();
        }

        m_stateChanged.notify_all();
        auto const completedAt = std::chrono::steady_clock::now();
        timing.RendererOpenDurationMs =
            std::chrono::duration<double, std::milli>(completedAt - decoderOpenedAt).count();
        timing.ExecutionDurationMs =
            std::chrono::duration<double, std::milli>(completedAt - acquiredAt).count();
        return timing;
    }

    PlaybackGraphSwitchTiming PlaybackGraph::SwitchSubtitleStream(std::optional<int32_t> subtitleStreamIndex)
    {
        auto const requestedAt = std::chrono::steady_clock::now();
        std::unique_lock lock(m_graphMutex);
        auto const acquiredAt = std::chrono::steady_clock::now();
        PlaybackGraphSwitchTiming timing;
        timing.PacketCacheEnabled = m_switchPacketCacheEnabled;
        timing.LockWaitDurationMs =
            std::chrono::duration<double, std::milli>(acquiredAt - requestedAt).count();
        if (!m_open)
        {
            throw winrt::hresult_error(E_FAIL, L"Playback is not open.");
        }

        auto restartVideoDecodeWorker = StopVideoDecodeWorkerForMutation();
        ScopeExit restartVideoDecodeWorkerOnExit([this, restartVideoDecodeWorker]()
        {
            RestartVideoDecodeWorkerAfterMutation(restartVideoDecodeWorker);
        });

        auto previousSelection = m_subtitleRenderer.SelectedStreamIndex();
        auto resumePositionTicks = m_positionTicks;
        auto useSwitchPacketCache = false;
        SubtitleSwitchOperations operations;
        operations.DisableSelection = [this, &timing]
        {
            auto const startedAt = std::chrono::steady_clock::now();
            m_subtitleDecoder.Close();
            m_subtitleRenderer.Disable();
            timing.QuiesceDurationMs += std::chrono::duration<double, std::milli>(
                std::chrono::steady_clock::now() - startedAt).count();
        };
        operations.OpenDecoder = [this, resumePositionTicks, &timing, &useSwitchPacketCache](int32_t streamIndex)
        {
            auto const startedAt = std::chrono::steady_clock::now();
            auto const packetCache = m_mediaSource.SwitchPacketCacheSnapshot(
                streamIndex,
                resumePositionTicks,
                false);
            useSwitchPacketCache = packetCache.HasCoverage;
            timing.PacketCacheHit = packetCache.HasCoverage;
            timing.PacketCachePacketCount = packetCache.PacketCount;
            timing.PacketCacheBytes = packetCache.Bytes;
            timing.PacketCacheWindowDurationTicks = packetCache.WindowDurationTicks;
            m_subtitleDecoder.Open(m_mediaSource, streamIndex);
            timing.DecoderOpenDurationMs += std::chrono::duration<double, std::milli>(
                std::chrono::steady_clock::now() - startedAt).count();
        };
        operations.SelectedDecoderStream = [this]
        {
            return m_subtitleDecoder.SelectedStreamIndex();
        };
        operations.ShouldRebasePlayback = [this]
        {
            return !m_switchPacketCacheEnabled;
        };
        operations.SeekVideo = [this, resumePositionTicks, &timing, &useSwitchPacketCache]
        {
            if (useSwitchPacketCache)
            {
                return;
            }
            auto const startedAt = std::chrono::steady_clock::now();
            m_pendingVideoFrame.reset();
            ResetAudioAheadWait();
            ResetVideoClock();
            m_audioRenderer.Flush();
            m_videoDecoder.Seek(resumePositionTicks);
            timing.SeekDurationMs += std::chrono::duration<double, std::milli>(
                std::chrono::steady_clock::now() - startedAt).count();
        };
        operations.FlushAudioDecoder = [this, resumePositionTicks, &timing, &useSwitchPacketCache]
        {
            if (useSwitchPacketCache)
            {
                return;
            }
            auto const startedAt = std::chrono::steady_clock::now();
            m_audioDecoder.Flush(resumePositionTicks);
            timing.QuiesceDurationMs += std::chrono::duration<double, std::milli>(
                std::chrono::steady_clock::now() - startedAt).count();
        };
        operations.FlushSubtitleDecoder = [this, &timing]
        {
            auto const startedAt = std::chrono::steady_clock::now();
            m_subtitleDecoder.Flush();
            timing.QuiesceDurationMs += std::chrono::duration<double, std::milli>(
                std::chrono::steady_clock::now() - startedAt).count();
        };
        operations.SelectRenderer = [this, resumePositionTicks, &timing](int32_t streamIndex)
        {
            auto const startedAt = std::chrono::steady_clock::now();
            if (!m_switchPacketCacheEnabled)
            {
                SetVideoPrerollTarget(resumePositionTicks);
            }
            m_subtitleRenderer.SwitchStream(streamIndex);
            timing.RendererOpenDurationMs += std::chrono::duration<double, std::milli>(
                std::chrono::steady_clock::now() - startedAt).count();
        };

        auto result = RunSubtitleSwitchTransaction(
            previousSelection,
            subtitleStreamIndex,
            operations);
        m_stateChanged.notify_all();
        if (result.Disposition == SubtitleSwitchDisposition::Completed)
        {
            auto const completedAt = std::chrono::steady_clock::now();
            timing.ExecutionDurationMs =
                std::chrono::duration<double, std::milli>(completedAt - acquiredAt).count();
            return timing;
        }

        if (result.Disposition == SubtitleSwitchDisposition::RestoredPrevious)
        {
            std::rethrow_exception(result.SwitchFailure);
        }

        if (result.Disposition == SubtitleSwitchDisposition::Disabled)
        {
            throw winrt::hresult_error(
                E_FAIL,
                L"Subtitle switch failed and previous selection could not be restored; subtitles are disabled.");
        }

        throw winrt::hresult_error(E_UNEXPECTED, L"Subtitle switch returned an unknown result.");
    }

    int64_t PlaybackGraph::CurrentPositionTicks() const noexcept
    {
        return m_positionSnapshotTicks.load(std::memory_order_relaxed);
    }

    uint64_t PlaybackGraph::SubtitleCueRenderCount() const noexcept
    {
        std::lock_guard lock(m_graphMutex);
        return m_subtitleCueRenderCount;
    }

    uint64_t PlaybackGraph::SubtitleDecodedCueCount() const noexcept
    {
        std::lock_guard lock(m_graphMutex);
        return m_subtitleDecoder.DecodedCueCount();
    }

    std::optional<int32_t> PlaybackGraph::SelectedAudioStreamIndex() const noexcept
    {
        std::lock_guard lock(m_graphMutex);
        return m_audioRenderer.SelectedStreamIndex();
    }

    std::optional<int32_t> PlaybackGraph::SelectedSubtitleStreamIndex() const noexcept
    {
        std::lock_guard lock(m_graphMutex);
        return m_subtitleRenderer.SelectedStreamIndex();
    }

    SeekPresentationSnapshot PlaybackGraph::SeekPresentationSnapshot() const noexcept
    {
        std::lock_guard lock(m_graphMutex);
        return m_seekPresentationTracker.Snapshot();
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
        snapshot.VideoDecodeDeviceMode = m_videoDecoder.UsesIndependentDecodeDevice()
            ? "independent-d3d11"
            : (m_videoDecoder.UsesHardwareDecode() ? "render-device-d3d11" : "software");
        snapshot.VideoDecodeSynchronizationMode = m_videoDecoder.UsesIndependentDecodeDevice()
            ? "shared-fence"
            : "none";
        snapshot.VideoDecoderSendPacketEagainCount = m_videoDecoder.SendPacketEagainCount();
        snapshot.VideoDecoderDoubleEagainRetryCount = m_videoDecoder.DoubleEagainRetryCount();
        snapshot.VideoDecoderDoubleEagainRecoveryCount = m_videoDecoder.DoubleEagainRecoveryCount();
        snapshot.VideoDecoderDoubleEagainExhaustedCount = m_videoDecoder.DoubleEagainExhaustedCount();
        if (m_videoDecodeWorker)
        {
            auto const queue = m_videoDecodeWorker->Snapshot();
            snapshot.VideoDecodeWorkerActive = !queue.Stopped;
            snapshot.VideoDecodeQueueCapacity = queue.Capacity;
            snapshot.VideoDecodeQueueMaxDepth = queue.MaxDepth;
            snapshot.VideoDecodeQueueProducerWaitCount = queue.ProducerWaitCount;
        }
        auto const readTiming = m_mediaSource.ReadTimingSnapshot();
        if (m_hasPlaybackReadBaseline)
        {
            auto const playbackReadTiming = SubtractReadTimingSnapshots(
                m_playbackReadTimingBaseline,
                readTiming);
            snapshot.PlaybackDemuxReadDurationMs = playbackReadTiming.ReadFrameDurationMs;
            snapshot.PlaybackDemuxPacketCount = playbackReadTiming.PacketCount;
            snapshot.PlaybackDemuxBytes = playbackReadTiming.Bytes;
            snapshot.PlaybackTransportCalls = ToPlaybackTransportCallMetrics(
                SubtractTransportCallSnapshots(
                    m_playbackTransportCallBaseline,
                    m_mediaSource.TransportCallSnapshot()));
        }
        snapshot.ReadErrorCount = readTiming.Recovery.ReadErrorCount;
        snapshot.ReadRetryCount = readTiming.Recovery.ReadRetryCount;
        snapshot.ReadRecoveryCount = readTiming.Recovery.ReadRecoveryCount;
        snapshot.MaxConsecutiveReadErrors = readTiming.Recovery.MaxConsecutiveReadErrors;
        snapshot.LastReadErrorCode = readTiming.Recovery.LastReadErrorCode;
        snapshot.FatalReadErrorCode = readTiming.Recovery.FatalReadErrorCode;
        snapshot.LastReadRecoveryDurationMs = readTiming.Recovery.LastReadRecoveryDurationMs;
        return snapshot;
    }

    std::optional<FfmpegVideoStreamSnapshot> PlaybackGraph::VideoSourceSnapshot() const
    {
        std::lock_guard lock(m_graphMutex);
        auto source = m_mediaSource.BestVideoStreamSnapshot();
        if (source)
        {
            return EnrichVideoSourceSnapshot(*source);
        }

        return m_lastVideoSourceSnapshot;
    }

    FfmpegVideoStreamSnapshot PlaybackGraph::EnrichVideoSourceSnapshot(
        FfmpegVideoStreamSnapshot snapshot) const noexcept
    {
        auto configuration = m_videoDecoder.DolbyVisionConfigurationSnapshot();
        if (!configuration || !configuration->IsPresent)
        {
            return snapshot;
        }

        snapshot.IsDolbyVision = true;
        snapshot.DolbyVisionProfile = configuration->Profile;
        snapshot.DolbyVisionCompatibilityId = configuration->BaseLayerSignalCompatibilityId;
        snapshot.HasHdr10BaseLayer = configuration->BaseLayerSignalCompatibilityId == 1;
        snapshot.HasHlgBaseLayer = configuration->BaseLayerSignalCompatibilityId == 4;

        if (snapshot.HasHdr10BaseLayer)
        {
            snapshot.HdrKind = "DolbyVisionWithHdr10Fallback";
            snapshot.VideoRange = "HDR10_Dolby_Vision";
        }
        else if (snapshot.HasHlgBaseLayer)
        {
            snapshot.HdrKind = "DolbyVisionWithHlgFallback";
            snapshot.VideoRange = "HLG_Dolby_Vision";
        }
        else
        {
            snapshot.HdrKind = "DolbyVisionUnsupported";
            snapshot.VideoRange = "Dolby_Vision";
        }

        return snapshot;
    }

    std::vector<FfmpegStreamSnapshot> PlaybackGraph::SourceTrackSnapshots() const
    {
        std::lock_guard lock(m_graphMutex);
        return m_mediaSource.StreamSnapshots();
    }

    FfmpegTimelineSnapshot PlaybackGraph::TimelineSnapshot() const
    {
        std::lock_guard lock(m_graphMutex);
        auto video = m_mediaSource.BestVideoStreamSnapshot();
        return m_mediaSource.TimelineSnapshot(video ? video->StreamIndex : -1);
    }

    FfmpegSeekReplayAttemptSnapshot PlaybackGraph::LastSeekReplaySnapshot() const
    {
        std::lock_guard lock(m_graphMutex);
        return m_lastSeekReplaySnapshot;
    }

    PlaybackGraphSeekTiming PlaybackGraph::LastSeekTimingSnapshot() const
    {
        std::lock_guard lock(m_graphMutex);
        return m_lastSeekTimingSnapshot;
    }

    void PlaybackGraph::StartVideoDecodeWorker(bool waitUntilReady)
    {
        if (!m_videoDecoder.UsesIndependentDecodeDevice())
        {
            return;
        }

        if (!m_videoDecodeWorker)
        {
            m_videoDecodeWorker = std::make_unique<VideoDecodeWorker>([this]()
            {
                return m_videoDecoder.TryReadFrame();
            });
        }

        m_videoDecodeWorker->Start();
        if (!waitUntilReady)
        {
            return;
        }

        auto const readyDeadline = std::chrono::steady_clock::now() + 5s;
        while (std::chrono::steady_clock::now() < readyDeadline)
        {
            auto snapshot = m_videoDecodeWorker->Snapshot();
            if (snapshot.Depth > 0 || snapshot.EndOfStream || snapshot.Failed)
            {
                return;
            }

            std::this_thread::sleep_for(1ms);
        }

        AppendNativePlaybackDiagnostic(
            L"PlaybackGraph independent video decode worker did not become ready within 5 seconds");
    }

    void PlaybackGraph::StartVideoDecodeWorkerOrFallback() noexcept
    {
        try
        {
            StartVideoDecodeWorker(true);
        }
        catch (...)
        {
            AppendNativePlaybackDiagnostic(
                L"PlaybackGraph could not start the independent video decode worker; falling back to synchronous decode");
            if (m_videoDecodeWorker)
            {
                m_videoDecodeWorker->Stop();
                m_videoDecodeWorker.reset();
            }
        }
    }

    bool PlaybackGraph::StopVideoDecodeWorkerForMutation() noexcept
    {
        if (!m_videoDecodeWorker)
        {
            return false;
        }

        m_videoDecodeWorker->Stop();
        return true;
    }

    void PlaybackGraph::RestartVideoDecodeWorkerAfterMutation(bool restart) noexcept
    {
        if (!restart)
        {
            return;
        }

        try
        {
            StartVideoDecodeWorker(false);
        }
        catch (...)
        {
            AppendNativePlaybackDiagnostic(
                L"PlaybackGraph could not restart the independent video decode worker; falling back to synchronous decode");
            m_videoDecodeWorker.reset();
        }
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
        m_mediaSource.Interrupt();
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
        auto completedAudioAheadWaitGeneration = uint64_t{0};
        std::optional<std::chrono::steady_clock::time_point> completedAudioAheadWaitEndedAt;

        while (true)
        {
            {
                std::unique_lock lock(m_graphMutex);
                m_stateChanged.wait(lock, [this]()
                {
                    return m_stopRenderLoop || (m_open && !m_paused);
                });

                auto completedAudioAheadWaitIsCurrent =
                    completedRenderLoopWaitReason == RenderLoopWaitReason::AudioAhead &&
                    PlaybackFramePacing::ShouldRecordAudioAheadWaitPass(
                        completedAudioAheadWaitGeneration,
                        m_audioAheadWaitGeneration,
                        m_audioAheadWaitStartedAt.has_value());
                m_lastCompletedRenderLoopWaitReason = completedRenderLoopWaitReason;
                if (completedRenderLoopWaitReason == RenderLoopWaitReason::AudioAhead && !completedAudioAheadWaitIsCurrent)
                {
                    m_lastCompletedRenderLoopWaitReason = RenderLoopWaitReason::Default;
                }
                m_lastAudioAheadWaitEndedAt = completedAudioAheadWaitIsCurrent
                    ? completedAudioAheadWaitEndedAt
                    : std::nullopt;
                if (completedAudioAheadWaitIsCurrent)
                {
                    auto passOversleepMs =
                        m_qualityMetrics.RecordAudioAheadWaitPassMs(completedRenderLoopWaitDurationMs, completedRenderLoopWaitTargetMs);
                    m_audioAheadWaitOversleepMs += passOversleepMs;
                }

                completedRenderLoopWaitReason = RenderLoopWaitReason::Default;
                completedRenderLoopWaitDurationMs = 0.0;
                completedRenderLoopWaitTargetMs = 0.0;
                completedAudioAheadWaitGeneration = 0;
                completedAudioAheadWaitEndedAt.reset();

                if (m_stopRenderLoop)
                {
                    return;
                }
            }

            auto renderLoopWait = std::chrono::steady_clock::duration{PlaybackFramePacing::RenderLoopWait()};
            auto renderLoopWaitUseTimer = PlaybackFramePacing::ShouldUseRenderLoopTimer(renderLoopWait);
            auto renderLoopWaitReason = RenderLoopWaitReason::Default;
            auto audioAheadWaitGeneration = uint64_t{0};

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
                    if (renderLoopWaitReason == RenderLoopWaitReason::AudioAhead)
                    {
                        audioAheadWaitGeneration = m_audioAheadWaitGeneration;
                    }
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
                completedAudioAheadWaitGeneration = audioAheadWaitGeneration;
                completedAudioAheadWaitEndedAt = waitEndedAt;
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
                std::optional<DecodedVideoFrame> frame;
                auto decodeDurationMs = 0.0;
                auto workerWaitingForFrame = false;
                if (m_videoDecodeWorker)
                {
                    auto queuedFrame = m_videoDecodeWorker->TryPop();
                    switch (queuedFrame.Status)
                    {
                    case VideoFrameQueuePopStatus::Item:
                        frame = std::move(queuedFrame.Value->Frame);
                        decodeDurationMs = queuedFrame.Value->DecodeDurationMs;
                        break;
                    case VideoFrameQueuePopStatus::Failed:
                        std::rethrow_exception(queuedFrame.Error);
                    case VideoFrameQueuePopStatus::Empty:
                    case VideoFrameQueuePopStatus::Stopped:
                    case VideoFrameQueuePopStatus::StaleGeneration:
                        workerWaitingForFrame = true;
                        break;
                    case VideoFrameQueuePopStatus::EndOfStream:
                        break;
                    }
                }
                else
                {
                    auto decodeStartedAt = std::chrono::steady_clock::now();
                    frame = m_videoDecoder.TryReadFrame();
                    decodeDurationMs = std::chrono::duration<double, std::milli>(
                        std::chrono::steady_clock::now() - decodeStartedAt).count();
                }

                if (!frame)
                {
                    auto hasQueuedAudio = m_audioRenderer.QueuedBufferCount() > 0;
                    if (hasQueuedAudio)
                    {
                        ++m_videoStarvedPassCount;
                        ++m_qualityMetrics.VideoStarvedPasses;
                    }
                    else if (m_audioDecoder.IsOpen())
                    {
                        ++m_audioStarvedPassCount;
                        ++m_qualityMetrics.AudioStarvedPasses;
                    }

                    LogRuntimeStatsIfDue();
                    return workerWaitingForFrame || hasQueuedAudio;
                }

                m_qualityMetrics.RecordVideoDecodeDurationMs(decodeDurationMs);
                m_qualityMetrics.RecordVideoDecodePacketReadDurationMs(frame->DecodePacketReadDurationMs);
                m_qualityMetrics.RecordVideoDecodeSendPacketDurationMs(frame->DecodeSendPacketDurationMs);
                m_qualityMetrics.RecordVideoDecodeReceiveFrameDurationMs(frame->DecodeReceiveFrameDurationMs);
                m_qualityMetrics.RecordVideoDecodeFrameMaterializeDurationMs(frame->DecodeFrameMaterializeDurationMs);

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

            auto audioPosition = m_audioRenderer.CurrentPositionTicks();
            if (audioPosition)
            {
                m_positionSnapshotTicks.store(*audioPosition, std::memory_order_relaxed);
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
                        m_audioAheadWaitTargetMs = 0.0;
                        m_audioAheadWaitOversleepMs = 0.0;
                    }

                    m_audioAheadWaitTargetMs = PlaybackFramePacing::AccumulateAudioAheadWaitTargetMs(
                        m_audioAheadWaitTargetMs.value_or(0.0),
                        audioAheadWaitDuration);
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

            if (!m_videoDecoder.WaitForFrame(frame))
            {
                throw winrt::hresult_error(
                    E_FAIL,
                    L"Synchronization of an independent D3D11VA frame failed.");
            }

            EnsureHdrOutputForFrame(frame);
            auto renderStartedAt = std::chrono::steady_clock::now();
            auto const renderSample = m_videoRenderer.Render(frame, m_hdrOutputActive);
            auto const rendered = renderSample.Path != VideoRenderPath::None;
            auto renderEndedAt = std::chrono::steady_clock::now();
            m_qualityMetrics.RecordVideoRenderDurationMs(
                std::chrono::duration<double, std::milli>(renderEndedAt - renderStartedAt).count());
            if (rendered)
            {
                m_qualityMetrics.RecordVideoRenderPhaseSample(renderSample);
            }
            m_positionTicks = frame.PositionTicks;
            if (!audioPosition)
            {
                m_positionSnapshotTicks.store(frame.PositionTicks, std::memory_order_relaxed);
            }
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
                if (auto elapsed = m_renderIntervalTracker.Observe(renderedAt))
                {
                    m_qualityMetrics.RecordRenderIntervalMs(*elapsed);
                    if (m_lastCompletedRenderLoopWaitReason == RenderLoopWaitReason::AudioAhead)
                    {
                        m_qualityMetrics.RecordRenderIntervalAfterAudioAheadWaitMs(*elapsed);
                    }
                    else
                    {
                        m_qualityMetrics.RecordRenderIntervalAfterNonAudioWaitMs(*elapsed);
                    }
                }

                if (m_lastAudioAheadWaitEndedAt)
                {
                    auto endToPresentMs = std::chrono::duration<double, std::milli>(
                        renderedAt - *m_lastAudioAheadWaitEndedAt).count();
                    m_qualityMetrics.RecordAudioAheadWaitEndToPresentMs(endToPresentMs);
                    m_lastAudioAheadWaitEndedAt.reset();
                }

                ++m_renderedVideoFrameCount;
                ++m_qualityMetrics.RenderedVideoFrames;
                m_seekPresentationTracker.RecordPresentedFrame(
                    m_seekPresentationTracker.CurrentGeneration(),
                    m_renderedVideoFrameCount,
                    frame.PositionTicks);
                auto seekPresentation = m_seekPresentationTracker.Snapshot();
                m_qualityMetrics.FirstPresentedPositionTicks =
                    seekPresentation.ActualPositionTicks.value_or(-1);
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
                m_audioRenderer.DrainPendingFrame();
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
        auto decodedCueCount = m_subtitleDecoder.DecodedCueCount();
        if (decodedCueCount > m_lastLoggedSubtitleDecodedCueCount)
        {
            AppendNativePlaybackDiagnostic(
                L"PlaybackGraph.Subtitle decodedCueCount=" +
                std::to_wstring(decodedCueCount) +
                L" positionTicks=" +
                std::to_wstring(m_positionTicks));
            m_lastLoggedSubtitleDecodedCueCount = decodedCueCount;
        }
        if (auto cue = m_subtitleDecoder.TryGetCueAt(m_positionTicks))
        {
            m_subtitleRenderer.SetCue(*cue);
        }
        else
        {
            m_subtitleRenderer.ClearCue();
        }

        if (m_subtitleRenderer.RenderAt(m_positionTicks))
        {
            ++m_subtitleCueRenderCount;
        }
    }

    void PlaybackGraph::ResetRuntimeStats(bool preserveOpenTiming) noexcept
    {
        m_renderPassCount = 0;
        m_renderedVideoFrameCount = 0;
        m_decodedVideoFrameCount = 0;
        m_submittedAudioFrameCount = 0;
        m_subtitleCueRenderCount = 0;
        m_lastLoggedSubtitleDecodedCueCount = 0;
        m_droppedVideoFrameCount = 0;
        m_videoAheadWaitCount = 0;
        m_audioAheadWaitCount = 0;
        m_videoClockWaitCount = 0;
        m_videoStarvedPassCount = 0;
        m_audioStarvedPassCount = 0;
        m_seekPrerollDroppedVideoFrameCount = 0;
        if (preserveOpenTiming)
        {
            m_qualityMetrics.ResetRuntimeSamplesPreservingOpenTiming();
        }
        else
        {
            m_qualityMetrics.Reset();
            m_playbackReadTimingBaseline = {};
            m_playbackTransportCallBaseline = {};
            m_hasPlaybackReadBaseline = false;
        }
        m_lastRuntimeStatsLog = {};
        m_renderIntervalTracker.BreakContinuity();
        m_nextRenderLoopWaitReason = RenderLoopWaitReason::Default;
        m_lastCompletedRenderLoopWaitReason = RenderLoopWaitReason::Default;
        m_lastAudioAheadWaitEndedAt.reset();
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
        ++m_audioAheadWaitGeneration;
        m_audioAheadWaitStartedAt.reset();
        m_audioAheadWaitTargetMs.reset();
        m_audioAheadWaitOversleepMs = 0.0;
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
            m_audioAheadWaitOversleepMs,
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
