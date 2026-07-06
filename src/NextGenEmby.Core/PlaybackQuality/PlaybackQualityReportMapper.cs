using NextGenEmby.Core.Playback;
using NextGenEmby.Core.Emby;
using System.Linq;

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
            report.Timing.FramePacingSourceFrameRate = metrics.FramePacingSourceFrameRate;
            report.Timing.LateFrameDropToleranceMs = metrics.LateFrameDropToleranceMs;

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

        public static void ApplySource(
            PlaybackQualityReport report,
            PlaybackDescriptor descriptor)
        {
            var source = descriptor.MediaSource;
            var selectedAudio = descriptor.AudioStreamIndex.HasValue
                ? source.AudioStreams.FirstOrDefault(s => s.Index == descriptor.AudioStreamIndex.Value)
                : null;
            var audio = selectedAudio ?? source.AudioStreams.FirstOrDefault();
            var video = source.VideoStreams.FirstOrDefault();

            report.Source.ItemId = descriptor.ItemId;
            report.Source.MediaSourceId = source.Id;
            report.Source.Codec = FirstNonEmpty(video?.Codec, source.HdrProfile.Codec);
            report.Source.Width = source.Width;
            report.Source.Height = source.Height;
            report.Source.FrameRate = source.VideoFrameRate;
            report.Timing.ExpectedFrameDurationMs = source.VideoFrameRate > 0
                ? 1000.0 / source.VideoFrameRate
                : 0;
            report.Source.HdrKind = source.HdrProfile.Kind.ToString();
            report.Source.HdrPlaybackStrategy = source.HdrProfile.PlaybackStrategy;
            report.Source.IsHdr = source.HdrProfile.IsHdr;
            report.Source.IsDirectPlayable = source.HdrProfile.IsDirectPlayable;
            report.Source.IsDolbyVision = source.HdrProfile.IsDolbyVision;
            report.Source.DolbyVisionProfile = source.HdrProfile.DolbyVisionProfile;
            report.Source.DolbyVisionCompatibilityId = source.HdrProfile.DolbyVisionCompatibilityId;
            report.Source.HasHdr10BaseLayer = source.HdrProfile.HasHdr10BaseLayer;
            report.Source.HasHlgBaseLayer = source.HdrProfile.HasHlgBaseLayer;
            report.Source.AudioCodec = audio?.Codec ?? "";
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

        private static string FirstNonEmpty(string? first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first! : second ?? "";
        }
    }
}
