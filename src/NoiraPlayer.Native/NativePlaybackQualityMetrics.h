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

        uint64_t HardwareDecodedVideoFrames() const noexcept { return m_hardwareDecodedVideoFrames; }
        void HardwareDecodedVideoFrames(uint64_t value) noexcept { m_hardwareDecodedVideoFrames = value; }

        uint64_t SoftwareDecodedVideoFrames() const noexcept { return m_softwareDecodedVideoFrames; }
        void SoftwareDecodedVideoFrames(uint64_t value) noexcept { m_softwareDecodedVideoFrames = value; }

        uint64_t RenderedVideoFrames() const noexcept { return m_renderedVideoFrames; }
        void RenderedVideoFrames(uint64_t value) noexcept { m_renderedVideoFrames = value; }

        uint64_t SubmittedAudioFrames() const noexcept { return m_submittedAudioFrames; }
        void SubmittedAudioFrames(uint64_t value) noexcept { m_submittedAudioFrames = value; }

        uint64_t SubtitleDecodedCueCount() const noexcept { return m_subtitleDecodedCueCount; }
        void SubtitleDecodedCueCount(uint64_t value) noexcept { m_subtitleDecodedCueCount = value; }

        uint64_t SubtitleCueRenderCount() const noexcept { return m_subtitleCueRenderCount; }
        void SubtitleCueRenderCount(uint64_t value) noexcept { m_subtitleCueRenderCount = value; }

        int32_t SelectedSubtitleStreamIndex() const noexcept { return m_selectedSubtitleStreamIndex; }
        void SelectedSubtitleStreamIndex(int32_t value) noexcept { m_selectedSubtitleStreamIndex = value; }

        uint64_t DroppedVideoFrames() const noexcept { return m_droppedVideoFrames; }
        void DroppedVideoFrames(uint64_t value) noexcept { m_droppedVideoFrames = value; }

        uint64_t SeekPrerollDroppedFrames() const noexcept { return m_seekPrerollDroppedFrames; }
        void SeekPrerollDroppedFrames(uint64_t value) noexcept { m_seekPrerollDroppedFrames = value; }

        uint64_t VideoAheadWaitCount() const noexcept { return m_videoAheadWaitCount; }
        void VideoAheadWaitCount(uint64_t value) noexcept { m_videoAheadWaitCount = value; }

        uint64_t AudioAheadWaitCount() const noexcept { return m_audioAheadWaitCount; }
        void AudioAheadWaitCount(uint64_t value) noexcept { m_audioAheadWaitCount = value; }

        uint64_t VideoClockWaitCount() const noexcept { return m_videoClockWaitCount; }
        void VideoClockWaitCount(uint64_t value) noexcept { m_videoClockWaitCount = value; }

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

        double NativeGraphOpenDurationMs() const noexcept { return m_nativeGraphOpenDurationMs; }
        void NativeGraphOpenDurationMs(double value) noexcept { m_nativeGraphOpenDurationMs = value; }

        double FfmpegOpenInputDurationMs() const noexcept { return m_ffmpegOpenInputDurationMs; }
        void FfmpegOpenInputDurationMs(double value) noexcept { m_ffmpegOpenInputDurationMs = value; }

        double FfmpegStreamInfoDurationMs() const noexcept { return m_ffmpegStreamInfoDurationMs; }
        void FfmpegStreamInfoDurationMs(double value) noexcept { m_ffmpegStreamInfoDurationMs = value; }

        int64_t ContainerStartTimeTicks() const noexcept { return m_containerStartTimeTicks; }
        void ContainerStartTimeTicks(int64_t value) noexcept { m_containerStartTimeTicks = value; }
        int64_t VideoStreamStartTimeTicks() const noexcept { return m_videoStreamStartTimeTicks; }
        void VideoStreamStartTimeTicks(int64_t value) noexcept { m_videoStreamStartTimeTicks = value; }
        int64_t SeekDemuxTargetTicks() const noexcept { return m_seekDemuxTargetTicks; }
        void SeekDemuxTargetTicks(int64_t value) noexcept { m_seekDemuxTargetTicks = value; }
        int64_t FirstPresentedPositionTicks() const noexcept { return m_firstPresentedPositionTicks; }
        void FirstPresentedPositionTicks(int64_t value) noexcept { m_firstPresentedPositionTicks = value; }

        double RenderIntervalMsP05() const noexcept { return m_renderIntervalMsP05; }
        void RenderIntervalMsP05(double value) noexcept { m_renderIntervalMsP05 = value; }

        double RenderIntervalMsP50() const noexcept { return m_renderIntervalMsP50; }
        void RenderIntervalMsP50(double value) noexcept { m_renderIntervalMsP50 = value; }

        double RenderIntervalMsP95() const noexcept { return m_renderIntervalMsP95; }
        void RenderIntervalMsP95(double value) noexcept { m_renderIntervalMsP95 = value; }

        double RenderIntervalMsP99() const noexcept { return m_renderIntervalMsP99; }
        void RenderIntervalMsP99(double value) noexcept { m_renderIntervalMsP99 = value; }

        double MinFrameGapMs() const noexcept { return m_minFrameGapMs; }
        void MinFrameGapMs(double value) noexcept { m_minFrameGapMs = value; }

        double MaxFrameGapMs() const noexcept { return m_maxFrameGapMs; }
        void MaxFrameGapMs(double value) noexcept { m_maxFrameGapMs = value; }

        uint64_t RenderIntervalSampleCount() const noexcept { return m_renderIntervalSampleCount; }
        void RenderIntervalSampleCount(uint64_t value) noexcept { m_renderIntervalSampleCount = value; }

        uint64_t RenderIntervalOverExpected2MsCount() const noexcept { return m_renderIntervalOverExpected2MsCount; }
        void RenderIntervalOverExpected2MsCount(uint64_t value) noexcept { m_renderIntervalOverExpected2MsCount = value; }

        uint64_t RenderIntervalOverExpected4MsCount() const noexcept { return m_renderIntervalOverExpected4MsCount; }
        void RenderIntervalOverExpected4MsCount(uint64_t value) noexcept { m_renderIntervalOverExpected4MsCount = value; }

        uint64_t RenderIntervalUnderExpected2MsCount() const noexcept { return m_renderIntervalUnderExpected2MsCount; }
        void RenderIntervalUnderExpected2MsCount(uint64_t value) noexcept { m_renderIntervalUnderExpected2MsCount = value; }

        uint64_t RenderIntervalUnderExpected4MsCount() const noexcept { return m_renderIntervalUnderExpected4MsCount; }
        void RenderIntervalUnderExpected4MsCount(uint64_t value) noexcept { m_renderIntervalUnderExpected4MsCount = value; }

        uint64_t RenderIntervalAfterAudioAheadWaitSampleCount() const noexcept { return m_renderIntervalAfterAudioAheadWaitSampleCount; }
        void RenderIntervalAfterAudioAheadWaitSampleCount(uint64_t value) noexcept { m_renderIntervalAfterAudioAheadWaitSampleCount = value; }

        double RenderIntervalAfterAudioAheadWaitMsP95() const noexcept { return m_renderIntervalAfterAudioAheadWaitMsP95; }
        void RenderIntervalAfterAudioAheadWaitMsP95(double value) noexcept { m_renderIntervalAfterAudioAheadWaitMsP95 = value; }

        double RenderIntervalAfterAudioAheadWaitMsP99() const noexcept { return m_renderIntervalAfterAudioAheadWaitMsP99; }
        void RenderIntervalAfterAudioAheadWaitMsP99(double value) noexcept { m_renderIntervalAfterAudioAheadWaitMsP99 = value; }

        double RenderIntervalAfterAudioAheadWaitMsMax() const noexcept { return m_renderIntervalAfterAudioAheadWaitMsMax; }
        void RenderIntervalAfterAudioAheadWaitMsMax(double value) noexcept { m_renderIntervalAfterAudioAheadWaitMsMax = value; }

        uint64_t AudioAheadWaitEndToPresentSampleCount() const noexcept { return m_audioAheadWaitEndToPresentSampleCount; }
        void AudioAheadWaitEndToPresentSampleCount(uint64_t value) noexcept { m_audioAheadWaitEndToPresentSampleCount = value; }

        double AudioAheadWaitEndToPresentMsP50() const noexcept { return m_audioAheadWaitEndToPresentMsP50; }
        void AudioAheadWaitEndToPresentMsP50(double value) noexcept { m_audioAheadWaitEndToPresentMsP50 = value; }

        double AudioAheadWaitEndToPresentMsP95() const noexcept { return m_audioAheadWaitEndToPresentMsP95; }
        void AudioAheadWaitEndToPresentMsP95(double value) noexcept { m_audioAheadWaitEndToPresentMsP95 = value; }

        double AudioAheadWaitEndToPresentMsP99() const noexcept { return m_audioAheadWaitEndToPresentMsP99; }
        void AudioAheadWaitEndToPresentMsP99(double value) noexcept { m_audioAheadWaitEndToPresentMsP99 = value; }

        double AudioAheadWaitEndToPresentMsMax() const noexcept { return m_audioAheadWaitEndToPresentMsMax; }
        void AudioAheadWaitEndToPresentMsMax(double value) noexcept { m_audioAheadWaitEndToPresentMsMax = value; }

        uint64_t RenderIntervalAfterNonAudioWaitSampleCount() const noexcept { return m_renderIntervalAfterNonAudioWaitSampleCount; }
        void RenderIntervalAfterNonAudioWaitSampleCount(uint64_t value) noexcept { m_renderIntervalAfterNonAudioWaitSampleCount = value; }

        double RenderIntervalAfterNonAudioWaitMsP95() const noexcept { return m_renderIntervalAfterNonAudioWaitMsP95; }
        void RenderIntervalAfterNonAudioWaitMsP95(double value) noexcept { m_renderIntervalAfterNonAudioWaitMsP95 = value; }

        double RenderIntervalAfterNonAudioWaitMsP99() const noexcept { return m_renderIntervalAfterNonAudioWaitMsP99; }
        void RenderIntervalAfterNonAudioWaitMsP99(double value) noexcept { m_renderIntervalAfterNonAudioWaitMsP99 = value; }

        double RenderIntervalAfterNonAudioWaitMsMax() const noexcept { return m_renderIntervalAfterNonAudioWaitMsMax; }
        void RenderIntervalAfterNonAudioWaitMsMax(double value) noexcept { m_renderIntervalAfterNonAudioWaitMsMax = value; }

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

        double AudioAheadWaitFinalDeltaAbsMsP50() const noexcept { return m_audioAheadWaitFinalDeltaAbsMsP50; }
        void AudioAheadWaitFinalDeltaAbsMsP50(double value) noexcept { m_audioAheadWaitFinalDeltaAbsMsP50 = value; }

        double AudioAheadWaitFinalDeltaAbsMsP95() const noexcept { return m_audioAheadWaitFinalDeltaAbsMsP95; }
        void AudioAheadWaitFinalDeltaAbsMsP95(double value) noexcept { m_audioAheadWaitFinalDeltaAbsMsP95 = value; }

        double AudioAheadWaitFinalDeltaAbsMsP99() const noexcept { return m_audioAheadWaitFinalDeltaAbsMsP99; }
        void AudioAheadWaitFinalDeltaAbsMsP99(double value) noexcept { m_audioAheadWaitFinalDeltaAbsMsP99 = value; }

        double AudioAheadWaitFinalDeltaAbsMsMax() const noexcept { return m_audioAheadWaitFinalDeltaAbsMsMax; }
        void AudioAheadWaitFinalDeltaAbsMsMax(double value) noexcept { m_audioAheadWaitFinalDeltaAbsMsMax = value; }

        uint64_t AudioAheadWaitEpisodeCount() const noexcept { return m_audioAheadWaitEpisodeCount; }
        void AudioAheadWaitEpisodeCount(uint64_t value) noexcept { m_audioAheadWaitEpisodeCount = value; }

        double AudioAheadWaitPassesPerEpisodeP50() const noexcept { return m_audioAheadWaitPassesPerEpisodeP50; }
        void AudioAheadWaitPassesPerEpisodeP50(double value) noexcept { m_audioAheadWaitPassesPerEpisodeP50 = value; }

        double AudioAheadWaitPassesPerEpisodeP95() const noexcept { return m_audioAheadWaitPassesPerEpisodeP95; }
        void AudioAheadWaitPassesPerEpisodeP95(double value) noexcept { m_audioAheadWaitPassesPerEpisodeP95 = value; }

        double AudioAheadWaitPassesPerEpisodeP99() const noexcept { return m_audioAheadWaitPassesPerEpisodeP99; }
        void AudioAheadWaitPassesPerEpisodeP99(double value) noexcept { m_audioAheadWaitPassesPerEpisodeP99 = value; }

        double AudioAheadWaitPassesPerEpisodeMax() const noexcept { return m_audioAheadWaitPassesPerEpisodeMax; }
        void AudioAheadWaitPassesPerEpisodeMax(double value) noexcept { m_audioAheadWaitPassesPerEpisodeMax = value; }

        double AudioAheadWaitPassDurationMsP50() const noexcept { return m_audioAheadWaitPassDurationMsP50; }
        void AudioAheadWaitPassDurationMsP50(double value) noexcept { m_audioAheadWaitPassDurationMsP50 = value; }

        double AudioAheadWaitPassDurationMsP95() const noexcept { return m_audioAheadWaitPassDurationMsP95; }
        void AudioAheadWaitPassDurationMsP95(double value) noexcept { m_audioAheadWaitPassDurationMsP95 = value; }

        double AudioAheadWaitPassDurationMsP99() const noexcept { return m_audioAheadWaitPassDurationMsP99; }
        void AudioAheadWaitPassDurationMsP99(double value) noexcept { m_audioAheadWaitPassDurationMsP99 = value; }

        double AudioAheadWaitPassDurationMsMax() const noexcept { return m_audioAheadWaitPassDurationMsMax; }
        void AudioAheadWaitPassDurationMsMax(double value) noexcept { m_audioAheadWaitPassDurationMsMax = value; }

        double AudioAheadWaitPassTargetMsP50() const noexcept { return m_audioAheadWaitPassTargetMsP50; }
        void AudioAheadWaitPassTargetMsP50(double value) noexcept { m_audioAheadWaitPassTargetMsP50 = value; }

        double AudioAheadWaitPassTargetMsP95() const noexcept { return m_audioAheadWaitPassTargetMsP95; }
        void AudioAheadWaitPassTargetMsP95(double value) noexcept { m_audioAheadWaitPassTargetMsP95 = value; }

        double AudioAheadWaitPassTargetMsP99() const noexcept { return m_audioAheadWaitPassTargetMsP99; }
        void AudioAheadWaitPassTargetMsP99(double value) noexcept { m_audioAheadWaitPassTargetMsP99 = value; }

        double AudioAheadWaitPassTargetMsMax() const noexcept { return m_audioAheadWaitPassTargetMsMax; }
        void AudioAheadWaitPassTargetMsMax(double value) noexcept { m_audioAheadWaitPassTargetMsMax = value; }

        double AudioAheadWaitPassOversleepMsP50() const noexcept { return m_audioAheadWaitPassOversleepMsP50; }
        void AudioAheadWaitPassOversleepMsP50(double value) noexcept { m_audioAheadWaitPassOversleepMsP50 = value; }

        double AudioAheadWaitPassOversleepMsP95() const noexcept { return m_audioAheadWaitPassOversleepMsP95; }
        void AudioAheadWaitPassOversleepMsP95(double value) noexcept { m_audioAheadWaitPassOversleepMsP95 = value; }

        double AudioAheadWaitPassOversleepMsP99() const noexcept { return m_audioAheadWaitPassOversleepMsP99; }
        void AudioAheadWaitPassOversleepMsP99(double value) noexcept { m_audioAheadWaitPassOversleepMsP99 = value; }

        double AudioAheadWaitPassOversleepMsMax() const noexcept { return m_audioAheadWaitPassOversleepMsMax; }
        void AudioAheadWaitPassOversleepMsMax(double value) noexcept { m_audioAheadWaitPassOversleepMsMax = value; }

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
        uint64_t m_hardwareDecodedVideoFrames{0};
        uint64_t m_softwareDecodedVideoFrames{0};
        uint64_t m_renderedVideoFrames{0};
        uint64_t m_submittedAudioFrames{0};
        uint64_t m_subtitleDecodedCueCount{0};
        uint64_t m_subtitleCueRenderCount{0};
        int32_t m_selectedSubtitleStreamIndex{-1};
        uint64_t m_droppedVideoFrames{0};
        uint64_t m_seekPrerollDroppedFrames{0};
        uint64_t m_videoAheadWaitCount{0};
        uint64_t m_audioAheadWaitCount{0};
        uint64_t m_videoClockWaitCount{0};
        uint64_t m_videoStarvedPasses{0};
        uint64_t m_audioStarvedPasses{0};
        uint64_t m_queuedAudioBuffers{0};
        int64_t m_audioClockTicks{0};
        int64_t m_videoPositionTicks{0};
        double m_nativeGraphOpenDurationMs{0.0};
        double m_ffmpegOpenInputDurationMs{0.0};
        double m_ffmpegStreamInfoDurationMs{0.0};
        int64_t m_containerStartTimeTicks{0};
        int64_t m_videoStreamStartTimeTicks{0};
        int64_t m_seekDemuxTargetTicks{-1};
        int64_t m_firstPresentedPositionTicks{-1};
        double m_renderIntervalMsP05{0.0};
        double m_renderIntervalMsP50{0.0};
        double m_renderIntervalMsP95{0.0};
        double m_renderIntervalMsP99{0.0};
        double m_minFrameGapMs{0.0};
        double m_maxFrameGapMs{0.0};
        uint64_t m_renderIntervalSampleCount{0};
        uint64_t m_renderIntervalOverExpected2MsCount{0};
        uint64_t m_renderIntervalOverExpected4MsCount{0};
        uint64_t m_renderIntervalUnderExpected2MsCount{0};
        uint64_t m_renderIntervalUnderExpected4MsCount{0};
        uint64_t m_renderIntervalAfterAudioAheadWaitSampleCount{0};
        double m_renderIntervalAfterAudioAheadWaitMsP95{0.0};
        double m_renderIntervalAfterAudioAheadWaitMsP99{0.0};
        double m_renderIntervalAfterAudioAheadWaitMsMax{0.0};
        uint64_t m_audioAheadWaitEndToPresentSampleCount{0};
        double m_audioAheadWaitEndToPresentMsP50{0.0};
        double m_audioAheadWaitEndToPresentMsP95{0.0};
        double m_audioAheadWaitEndToPresentMsP99{0.0};
        double m_audioAheadWaitEndToPresentMsMax{0.0};
        uint64_t m_renderIntervalAfterNonAudioWaitSampleCount{0};
        double m_renderIntervalAfterNonAudioWaitMsP95{0.0};
        double m_renderIntervalAfterNonAudioWaitMsP99{0.0};
        double m_renderIntervalAfterNonAudioWaitMsMax{0.0};
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
        double m_audioAheadWaitFinalDeltaAbsMsP50{0.0};
        double m_audioAheadWaitFinalDeltaAbsMsP95{0.0};
        double m_audioAheadWaitFinalDeltaAbsMsP99{0.0};
        double m_audioAheadWaitFinalDeltaAbsMsMax{0.0};
        uint64_t m_audioAheadWaitEpisodeCount{0};
        double m_audioAheadWaitPassesPerEpisodeP50{0.0};
        double m_audioAheadWaitPassesPerEpisodeP95{0.0};
        double m_audioAheadWaitPassesPerEpisodeP99{0.0};
        double m_audioAheadWaitPassesPerEpisodeMax{0.0};
        double m_audioAheadWaitPassDurationMsP50{0.0};
        double m_audioAheadWaitPassDurationMsP95{0.0};
        double m_audioAheadWaitPassDurationMsP99{0.0};
        double m_audioAheadWaitPassDurationMsMax{0.0};
        double m_audioAheadWaitPassTargetMsP50{0.0};
        double m_audioAheadWaitPassTargetMsP95{0.0};
        double m_audioAheadWaitPassTargetMsP99{0.0};
        double m_audioAheadWaitPassTargetMsMax{0.0};
        double m_audioAheadWaitPassOversleepMsP50{0.0};
        double m_audioAheadWaitPassOversleepMsP95{0.0};
        double m_audioAheadWaitPassOversleepMsP99{0.0};
        double m_audioAheadWaitPassOversleepMsMax{0.0};
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
