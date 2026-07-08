#pragma once

#include "NativePlaybackQualityMetrics.g.h"

namespace winrt::NextGenEmby::Native::implementation
{
    struct NativePlaybackQualityMetrics : NativePlaybackQualityMetricsT<NativePlaybackQualityMetrics>
    {
        NativePlaybackQualityMetrics() = default;

        uint64_t RenderPasses() const noexcept { return m_renderPasses; }
        void RenderPasses(uint64_t value) noexcept { m_renderPasses = value; }

        uint64_t DecodedVideoFrames() const noexcept { return m_decodedVideoFrames; }
        void DecodedVideoFrames(uint64_t value) noexcept { m_decodedVideoFrames = value; }

        uint64_t RenderedVideoFrames() const noexcept { return m_renderedVideoFrames; }
        void RenderedVideoFrames(uint64_t value) noexcept { m_renderedVideoFrames = value; }

        uint64_t SubmittedAudioFrames() const noexcept { return m_submittedAudioFrames; }
        void SubmittedAudioFrames(uint64_t value) noexcept { m_submittedAudioFrames = value; }

        uint64_t DroppedVideoFrames() const noexcept { return m_droppedVideoFrames; }
        void DroppedVideoFrames(uint64_t value) noexcept { m_droppedVideoFrames = value; }

        uint64_t SeekPrerollDroppedFrames() const noexcept { return m_seekPrerollDroppedFrames; }
        void SeekPrerollDroppedFrames(uint64_t value) noexcept { m_seekPrerollDroppedFrames = value; }

        uint64_t VideoAheadWaitCount() const noexcept { return m_videoAheadWaitCount; }
        void VideoAheadWaitCount(uint64_t value) noexcept { m_videoAheadWaitCount = value; }

        uint64_t VideoStarvedPasses() const noexcept { return m_videoStarvedPasses; }
        void VideoStarvedPasses(uint64_t value) noexcept { m_videoStarvedPasses = value; }

        uint64_t AudioStarvedPasses() const noexcept { return m_audioStarvedPasses; }
        void AudioStarvedPasses(uint64_t value) noexcept { m_audioStarvedPasses = value; }

        uint64_t QueuedAudioBuffers() const noexcept { return m_queuedAudioBuffers; }
        void QueuedAudioBuffers(uint64_t value) noexcept { m_queuedAudioBuffers = value; }

        int64_t AudioClockTicks() const noexcept { return m_audioClockTicks; }
        void AudioClockTicks(int64_t value) noexcept { m_audioClockTicks = value; }

        int64_t VideoPositionTicks() const noexcept { return m_videoPositionTicks; }
        void VideoPositionTicks(int64_t value) noexcept { m_videoPositionTicks = value; }

        double RenderIntervalMsP50() const noexcept { return m_renderIntervalMsP50; }
        void RenderIntervalMsP50(double value) noexcept { m_renderIntervalMsP50 = value; }

        double RenderIntervalMsP95() const noexcept { return m_renderIntervalMsP95; }
        void RenderIntervalMsP95(double value) noexcept { m_renderIntervalMsP95 = value; }

        double RenderIntervalMsP99() const noexcept { return m_renderIntervalMsP99; }
        void RenderIntervalMsP99(double value) noexcept { m_renderIntervalMsP99 = value; }

        double MaxFrameGapMs() const noexcept { return m_maxFrameGapMs; }
        void MaxFrameGapMs(double value) noexcept { m_maxFrameGapMs = value; }

        double PresentDurationMsP50() const noexcept { return m_presentDurationMsP50; }
        void PresentDurationMsP50(double value) noexcept { m_presentDurationMsP50 = value; }

        double PresentDurationMsP95() const noexcept { return m_presentDurationMsP95; }
        void PresentDurationMsP95(double value) noexcept { m_presentDurationMsP95 = value; }

        double PresentDurationMsP99() const noexcept { return m_presentDurationMsP99; }
        void PresentDurationMsP99(double value) noexcept { m_presentDurationMsP99 = value; }

        double PresentDurationMsMax() const noexcept { return m_presentDurationMsMax; }
        void PresentDurationMsMax(double value) noexcept { m_presentDurationMsMax = value; }

        double FramePacingSourceFrameRate() const noexcept { return m_framePacingSourceFrameRate; }
        void FramePacingSourceFrameRate(double value) noexcept { m_framePacingSourceFrameRate = value; }

        double LateFrameDropToleranceMs() const noexcept { return m_lateFrameDropToleranceMs; }
        void LateFrameDropToleranceMs(double value) noexcept { m_lateFrameDropToleranceMs = value; }

        double AudioVideoDriftMsP50() const noexcept { return m_audioVideoDriftMsP50; }
        void AudioVideoDriftMsP50(double value) noexcept { m_audioVideoDriftMsP50 = value; }

        double AudioVideoDriftMsP95() const noexcept { return m_audioVideoDriftMsP95; }
        void AudioVideoDriftMsP95(double value) noexcept { m_audioVideoDriftMsP95 = value; }

        double AudioVideoDriftMsP99() const noexcept { return m_audioVideoDriftMsP99; }
        void AudioVideoDriftMsP99(double value) noexcept { m_audioVideoDriftMsP99 = value; }

        double AudioVideoDriftMsMax() const noexcept { return m_audioVideoDriftMsMax; }
        void AudioVideoDriftMsMax(double value) noexcept { m_audioVideoDriftMsMax = value; }

    private:
        uint64_t m_renderPasses{0};
        uint64_t m_decodedVideoFrames{0};
        uint64_t m_renderedVideoFrames{0};
        uint64_t m_submittedAudioFrames{0};
        uint64_t m_droppedVideoFrames{0};
        uint64_t m_seekPrerollDroppedFrames{0};
        uint64_t m_videoAheadWaitCount{0};
        uint64_t m_videoStarvedPasses{0};
        uint64_t m_audioStarvedPasses{0};
        uint64_t m_queuedAudioBuffers{0};
        int64_t m_audioClockTicks{0};
        int64_t m_videoPositionTicks{0};
        double m_renderIntervalMsP50{0.0};
        double m_renderIntervalMsP95{0.0};
        double m_renderIntervalMsP99{0.0};
        double m_maxFrameGapMs{0.0};
        double m_presentDurationMsP50{0.0};
        double m_presentDurationMsP95{0.0};
        double m_presentDurationMsP99{0.0};
        double m_presentDurationMsMax{0.0};
        double m_framePacingSourceFrameRate{0.0};
        double m_lateFrameDropToleranceMs{0.0};
        double m_audioVideoDriftMsP50{0.0};
        double m_audioVideoDriftMsP95{0.0};
        double m_audioVideoDriftMsP99{0.0};
        double m_audioVideoDriftMsMax{0.0};
    };
}

namespace winrt::NextGenEmby::Native::factory_implementation
{
    struct NativePlaybackQualityMetrics :
        NativePlaybackQualityMetricsT<NativePlaybackQualityMetrics, implementation::NativePlaybackQualityMetrics>
    {
    };
}
