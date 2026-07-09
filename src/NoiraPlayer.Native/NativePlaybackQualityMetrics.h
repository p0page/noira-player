#pragma once

#include "NativePlaybackQualityMetrics.g.h"

namespace winrt::NoiraPlayer::Native::implementation
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

        double AudioAheadWaitDurationMsP50() const noexcept { return m_audioAheadWaitDurationMsP50; }
        void AudioAheadWaitDurationMsP50(double value) noexcept { m_audioAheadWaitDurationMsP50 = value; }

        double AudioAheadWaitDurationMsP95() const noexcept { return m_audioAheadWaitDurationMsP95; }
        void AudioAheadWaitDurationMsP95(double value) noexcept { m_audioAheadWaitDurationMsP95 = value; }

        double AudioAheadWaitDurationMsP99() const noexcept { return m_audioAheadWaitDurationMsP99; }
        void AudioAheadWaitDurationMsP99(double value) noexcept { m_audioAheadWaitDurationMsP99 = value; }

        double AudioAheadWaitDurationMsMax() const noexcept { return m_audioAheadWaitDurationMsMax; }
        void AudioAheadWaitDurationMsMax(double value) noexcept { m_audioAheadWaitDurationMsMax = value; }

        double AudioAheadWaitTargetMsP50() const noexcept { return m_audioAheadWaitTargetMsP50; }
        void AudioAheadWaitTargetMsP50(double value) noexcept { m_audioAheadWaitTargetMsP50 = value; }

        double AudioAheadWaitTargetMsP95() const noexcept { return m_audioAheadWaitTargetMsP95; }
        void AudioAheadWaitTargetMsP95(double value) noexcept { m_audioAheadWaitTargetMsP95 = value; }

        double AudioAheadWaitTargetMsP99() const noexcept { return m_audioAheadWaitTargetMsP99; }
        void AudioAheadWaitTargetMsP99(double value) noexcept { m_audioAheadWaitTargetMsP99 = value; }

        double AudioAheadWaitTargetMsMax() const noexcept { return m_audioAheadWaitTargetMsMax; }
        void AudioAheadWaitTargetMsMax(double value) noexcept { m_audioAheadWaitTargetMsMax = value; }

        double AudioAheadWaitOversleepMsP50() const noexcept { return m_audioAheadWaitOversleepMsP50; }
        void AudioAheadWaitOversleepMsP50(double value) noexcept { m_audioAheadWaitOversleepMsP50 = value; }

        double AudioAheadWaitOversleepMsP95() const noexcept { return m_audioAheadWaitOversleepMsP95; }
        void AudioAheadWaitOversleepMsP95(double value) noexcept { m_audioAheadWaitOversleepMsP95 = value; }

        double AudioAheadWaitOversleepMsP99() const noexcept { return m_audioAheadWaitOversleepMsP99; }
        void AudioAheadWaitOversleepMsP99(double value) noexcept { m_audioAheadWaitOversleepMsP99 = value; }

        double AudioAheadWaitOversleepMsMax() const noexcept { return m_audioAheadWaitOversleepMsMax; }
        void AudioAheadWaitOversleepMsMax(double value) noexcept { m_audioAheadWaitOversleepMsMax = value; }

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
        double m_audioAheadWaitDurationMsP50{0.0};
        double m_audioAheadWaitDurationMsP95{0.0};
        double m_audioAheadWaitDurationMsP99{0.0};
        double m_audioAheadWaitDurationMsMax{0.0};
        double m_audioAheadWaitTargetMsP50{0.0};
        double m_audioAheadWaitTargetMsP95{0.0};
        double m_audioAheadWaitTargetMsP99{0.0};
        double m_audioAheadWaitTargetMsMax{0.0};
        double m_audioAheadWaitOversleepMsP50{0.0};
        double m_audioAheadWaitOversleepMsP95{0.0};
        double m_audioAheadWaitOversleepMsP99{0.0};
        double m_audioAheadWaitOversleepMsMax{0.0};
        double m_framePacingSourceFrameRate{0.0};
        double m_lateFrameDropToleranceMs{0.0};
        double m_audioVideoDriftMsP50{0.0};
        double m_audioVideoDriftMsP95{0.0};
        double m_audioVideoDriftMsP99{0.0};
        double m_audioVideoDriftMsMax{0.0};
    };
}

namespace winrt::NoiraPlayer::Native::factory_implementation
{
    struct NativePlaybackQualityMetrics :
        NativePlaybackQualityMetricsT<NativePlaybackQualityMetrics, implementation::NativePlaybackQualityMetrics>
    {
    };
}
