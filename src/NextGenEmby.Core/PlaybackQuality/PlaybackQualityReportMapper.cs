using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityReportMapper
    {
        public static void ApplyDisplayStatus(
            PlaybackQualityReport report,
            PlaybackDisplayStatus status)
        {
            report.Display.HdrStatus = status.HdrStatus.ToString();
            report.Display.IsHdrDisplayAvailable = status.IsHdrDisplayAvailable;
            report.Display.IsHdrOutputActive = status.IsHdrOutputActive;
            report.Display.RefreshRateHz = status.RefreshRateHz;
            report.Display.Message = status.Message;

            report.ColorPipeline.ActualHdrOutput = MapActualHdrOutput(status);
            report.ColorPipeline.SwapChainFormat = status.SwapChainFormat;
            report.ColorPipeline.SwapChainColorSpace = status.SwapChainColorSpace;
            report.ColorPipeline.IsTenBitSwapChain = status.IsTenBitSwapChain;
            report.ColorPipeline.IsVideoProcessorColorSpaceValidated =
                status.IsVideoProcessorColorSpaceValidated;
            report.ColorPipeline.DxgiInput = status.VideoProcessorInputColorSpace;
            report.ColorPipeline.DxgiOutput = status.VideoProcessorOutputColorSpace;
            report.ColorPipeline.ConversionStatus = status.VideoProcessorConversionStatus;
        }

        public static void ApplyMetrics(
            PlaybackQualityReport report,
            PlaybackQualityMetricsSnapshot metrics)
        {
            report.Timing.RenderPasses = metrics.RenderPasses;
            report.Timing.DecodedVideoFrames = metrics.DecodedVideoFrames;
            report.Timing.RenderedVideoFrames = metrics.RenderedVideoFrames;
            report.Timing.DroppedVideoFrames = metrics.DroppedVideoFrames;
            report.Timing.SeekPrerollDroppedFrames = metrics.SeekPrerollDroppedFrames;
            report.Timing.VideoAheadWaitCount = metrics.VideoAheadWaitCount;
            report.Timing.RenderIntervalMsP50 = metrics.RenderIntervalMsP50;
            report.Timing.RenderIntervalMsP95 = metrics.RenderIntervalMsP95;
            report.Timing.RenderIntervalMsP99 = metrics.RenderIntervalMsP99;
            report.Timing.MaxFrameGapMs = metrics.MaxFrameGapMs;

            report.Sync.AudioClockTicks = metrics.AudioClockTicks;
            report.Sync.VideoPositionTicks = metrics.VideoPositionTicks;
            report.Sync.AudioVideoDriftMsP50 = metrics.AudioVideoDriftMsP50;
            report.Sync.AudioVideoDriftMsP95 = metrics.AudioVideoDriftMsP95;
            report.Sync.AudioVideoDriftMsP99 = metrics.AudioVideoDriftMsP99;
            report.Sync.AudioVideoDriftMsMax = metrics.AudioVideoDriftMsMax;

            report.Buffers.SubmittedAudioFrames = metrics.SubmittedAudioFrames;
            report.Buffers.QueuedAudioBuffers = metrics.QueuedAudioBuffers;
            report.Buffers.VideoStarvedPasses = metrics.VideoStarvedPasses;
            report.Buffers.AudioStarvedPasses = metrics.AudioStarvedPasses;
        }

        private static string MapActualHdrOutput(PlaybackDisplayStatus status)
        {
            switch (status.HdrStatus)
            {
                case HdrOutputStatus.On:
                    return "Hdr10";
                case HdrOutputStatus.Off:
                    return "Sdr";
                case HdrOutputStatus.Unsupported:
                    return "Unsupported";
                case HdrOutputStatus.Failed:
                    return "Failed";
                default:
                    return "Unknown";
            }
        }
    }
}
