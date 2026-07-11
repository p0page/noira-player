#pragma once

#include "AudioDecoder.h"
#include "AudioRenderer.h"
#include "DxDeviceResources.h"
#include "FfmpegMediaSource.h"
#include "PlaybackQualityMetrics.h"
#include "RenderLoopWaiter.h"
#include "SubtitleDecoder.h"
#include "SubtitleRenderer.h"
#include "VideoDecoder.h"
#include "VideoRenderer.h"

#include <chrono>
#include <atomic>
#include <condition_variable>
#include <functional>
#include <mutex>
#include <optional>
#include <thread>

namespace winrt::NoiraPlayer::Native::implementation
{
    enum class PlaybackGraphState
    {
        Stopped,
        Failed
    };

    using PlaybackGraphStateChangedHandler = std::function<void(PlaybackGraphState state, winrt::hstring const& message)>;
    using PlaybackGraphHdrOutputChangedHandler = std::function<bool(bool desiredHdrOutput, double preferredRefreshRate)>;

    struct PlaybackGraphOpenRequest
    {
        winrt::hstring DirectStreamUrl;
        int64_t StartPositionTicks{0};
        int32_t AudioStreamIndex{0};
        bool HasAudioStreamIndex{false};
        int32_t SubtitleStreamIndex{0};
        bool HasSubtitleStreamIndex{false};
        double VideoFrameRate{0.0};
    };

    struct HdrOutputDecision
    {
        bool ShouldRequestDisplayChange{false};
        bool DesiredHdrOutput{false};
    };

    inline HdrOutputDecision ResolveHdrOutputDecisionForFrame(
        bool hasSeenVideoFrameColor,
        bool previousDesiredHdrOutput,
        VideoHdrKind hdrKind,
        bool isTenBitSwapChain) noexcept
    {
        auto desiredHdrOutput = ShouldOutputHdr10ForFrame(hdrKind, isTenBitSwapChain);
        return HdrOutputDecision{
            !hasSeenVideoFrameColor || desiredHdrOutput != previousDesiredHdrOutput,
            desiredHdrOutput};
    }

    class PlaybackGraph
    {
    public:
        explicit PlaybackGraph(
            DxDeviceResources& deviceResources,
            PlaybackGraphStateChangedHandler stateChanged = nullptr,
            PlaybackGraphHdrOutputChangedHandler hdrOutputChanged = nullptr);
        ~PlaybackGraph();

        void Open(PlaybackGraphOpenRequest const& request);
        void Pause();
        void Resume();
        void Seek(int64_t positionTicks);
        void Stop() noexcept;
        void SwitchAudioStream(int32_t audioStreamIndex);
        void SwitchSubtitleStream(std::optional<int32_t> subtitleStreamIndex);
        int64_t CurrentPositionTicks() const noexcept;
        PlaybackQualityMetricsSnapshot QualityMetricsSnapshot() const noexcept;
        std::optional<FfmpegVideoStreamSnapshot> VideoSourceSnapshot() const;
        std::vector<FfmpegStreamSnapshot> SourceTrackSnapshots() const;

    private:
        void StartRenderLoop();
        void StopRenderLoop() noexcept;
        void RenderLoop() noexcept;
        bool RenderNextFrame();
        uint32_t DecodeNextAudioFrame();
        void UpdateSubtitleCue();
        void ResetRuntimeStats() noexcept;
        void ResetVideoClock() noexcept;
        void ResetAudioAheadWait() noexcept;
        void RecordAudioAheadWaitIfNeeded() noexcept;
        void ApplyFramePacingPolicyMetrics() noexcept;
        void SetVideoPrerollTarget(int64_t targetTicks) noexcept;
        bool ShouldWaitForVideoClock(DecodedVideoFrame const& frame);
        void EnsureHdrOutputForFrame(DecodedVideoFrame const& frame);
        void LogRuntimeStatsIfDue();
        void NotifyStateChanged(PlaybackGraphState state, winrt::hstring const& message) const noexcept;

        DxDeviceResources& m_deviceResources;
        PlaybackGraphStateChangedHandler m_graphStateChanged;
        PlaybackGraphHdrOutputChangedHandler m_hdrOutputChanged;
        FfmpegMediaSource m_mediaSource;
        VideoDecoder m_videoDecoder;
        AudioDecoder m_audioDecoder;
        VideoRenderer m_videoRenderer;
        AudioRenderer m_audioRenderer;
        SubtitleDecoder m_subtitleDecoder;
        SubtitleRenderer m_subtitleRenderer;
        std::optional<DecodedVideoFrame> m_pendingVideoFrame;
        winrt::hstring m_url;
        int64_t m_positionTicks{0};
        std::atomic<int64_t> m_positionSnapshotTicks{0};
        double m_preferredVideoFrameRate{0.0};
        bool m_hasSeenVideoFrameColor{false};
        bool m_requestedHdrOutput{false};
        bool m_hdrOutputActive{false};
        bool m_open{false};
        bool m_paused{false};
        bool m_stopRenderLoop{false};
        uint64_t m_renderPassCount{0};
        uint64_t m_renderedVideoFrameCount{0};
        uint64_t m_decodedVideoFrameCount{0};
        uint64_t m_submittedAudioFrameCount{0};
        uint64_t m_droppedVideoFrameCount{0};
        uint64_t m_seekPrerollDroppedVideoFrameCount{0};
        uint64_t m_videoAheadWaitCount{0};
        uint64_t m_audioAheadWaitCount{0};
        uint64_t m_videoClockWaitCount{0};
        uint64_t m_videoStarvedPassCount{0};
        uint64_t m_audioStarvedPassCount{0};
        PlaybackQualityMetrics m_qualityMetrics;
        std::optional<int64_t> m_videoPrerollTargetTicks;
        std::chrono::steady_clock::time_point m_lastRuntimeStatsLog{};
        std::chrono::steady_clock::time_point m_lastRenderedFrameAt{};
        std::optional<std::chrono::steady_clock::time_point> m_audioAheadWaitStartedAt;
        std::optional<double> m_audioAheadWaitTargetMs;
        std::chrono::steady_clock::duration m_nextRenderLoopWait{std::chrono::milliseconds(5)};
        bool m_nextRenderLoopWaitUseTimer{false};
        RenderLoopWaiter m_renderLoopWaiter;
        std::chrono::steady_clock::time_point m_videoClockStartedAt{};
        int64_t m_videoClockStartPositionTicks{0};
        std::thread m_renderThread;
        mutable std::mutex m_graphMutex;
        std::condition_variable m_stateChanged;
    };
}
