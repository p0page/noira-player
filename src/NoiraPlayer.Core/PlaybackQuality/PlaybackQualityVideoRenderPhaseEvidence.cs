using System.Collections.Generic;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityVideoRenderPhaseEvidence
    {
        public static IReadOnlyList<string> Signals { get; } = new[]
        {
            "timing.videoRenderDirectCopyFrameCount",
            "timing.videoRenderVideoProcessorFrameCount",
            "timing.videoRenderBgraFrameCount",
            "timing.videoRenderPostProcessFrameCount",
            "timing.videoProcessorSetupCpuSampleCount",
            "timing.videoProcessorSetupCpuDurationMsP50",
            "timing.videoProcessorSetupCpuDurationMsP95",
            "timing.videoProcessorSetupCpuDurationMsP99",
            "timing.videoProcessorSetupCpuDurationMsMax",
            "timing.videoProcessorViewTargetCpuSampleCount",
            "timing.videoProcessorViewTargetCpuDurationMsP50",
            "timing.videoProcessorViewTargetCpuDurationMsP95",
            "timing.videoProcessorViewTargetCpuDurationMsP99",
            "timing.videoProcessorViewTargetCpuDurationMsMax",
            "timing.videoProcessorClearCpuSampleCount",
            "timing.videoProcessorClearCpuDurationMsP50",
            "timing.videoProcessorClearCpuDurationMsP95",
            "timing.videoProcessorClearCpuDurationMsP99",
            "timing.videoProcessorClearCpuDurationMsMax",
            "timing.videoProcessorBltCpuSampleCount",
            "timing.videoProcessorBltCpuDurationMsP50",
            "timing.videoProcessorBltCpuDurationMsP95",
            "timing.videoProcessorBltCpuDurationMsP99",
            "timing.videoProcessorBltCpuDurationMsMax",
            "timing.videoProcessorPostProcessCpuSampleCount",
            "timing.videoProcessorPostProcessCpuDurationMsP50",
            "timing.videoProcessorPostProcessCpuDurationMsP95",
            "timing.videoProcessorPostProcessCpuDurationMsP99",
            "timing.videoProcessorPostProcessCpuDurationMsMax"
        };

        public static bool HasPathSample(PlaybackQualityTiming timing)
        {
            return timing.VideoRenderDirectCopyFrameCount > 0 ||
                timing.VideoRenderVideoProcessorFrameCount > 0 ||
                timing.VideoRenderBgraFrameCount > 0 ||
                timing.VideoRenderPostProcessFrameCount > 0;
        }
    }
}
