using System.Collections.Generic;

namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityReportSignalDescriptor
    {
        public PlaybackQualityReportSignalDescriptor(
            string signal,
            string section,
            string property)
        {
            Signal = signal;
            Section = section;
            Property = property;
        }

        public string Signal { get; }

        public string Section { get; }

        public string Property { get; }
    }

    public static class PlaybackQualitySignalCatalog
    {
        private static readonly IReadOnlyList<PlaybackQualityReportSignalDescriptor> ReportSignalList =
            new List<PlaybackQualityReportSignalDescriptor>
            {
                new PlaybackQualityReportSignalDescriptor("source.codec", "source", "codec"),
                new PlaybackQualityReportSignalDescriptor("source.width", "source", "width"),
                new PlaybackQualityReportSignalDescriptor("source.height", "source", "height"),
                new PlaybackQualityReportSignalDescriptor("source.frameRate", "source", "frameRate"),
                new PlaybackQualityReportSignalDescriptor("source.hdrKind", "source", "hdrKind"),
                new PlaybackQualityReportSignalDescriptor("source.hdrPlaybackStrategy", "source", "hdrPlaybackStrategy"),
                new PlaybackQualityReportSignalDescriptor("source.isHdr", "source", "isHdr"),
                new PlaybackQualityReportSignalDescriptor("source.isDirectPlayable", "source", "isDirectPlayable"),
                new PlaybackQualityReportSignalDescriptor("source.isDolbyVision", "source", "isDolbyVision"),
                new PlaybackQualityReportSignalDescriptor("source.dolbyVisionProfile", "source", "dolbyVisionProfile"),
                new PlaybackQualityReportSignalDescriptor("source.dolbyVisionCompatibilityId", "source", "dolbyVisionCompatibilityId"),
                new PlaybackQualityReportSignalDescriptor("source.hasHdr10BaseLayer", "source", "hasHdr10BaseLayer"),
                new PlaybackQualityReportSignalDescriptor("source.hasHlgBaseLayer", "source", "hasHlgBaseLayer"),
                new PlaybackQualityReportSignalDescriptor("startup.startupDurationMs", "startup", "startupDurationMs"),
                new PlaybackQualityReportSignalDescriptor("timing.renderedVideoFrames", "timing", "renderedVideoFrames"),
                new PlaybackQualityReportSignalDescriptor("timing.droppedVideoFrames", "timing", "droppedVideoFrames"),
                new PlaybackQualityReportSignalDescriptor("timing.expectedFrameDurationMs", "timing", "expectedFrameDurationMs"),
                new PlaybackQualityReportSignalDescriptor("timing.maxFrameGapMs", "timing", "maxFrameGapMs"),
                new PlaybackQualityReportSignalDescriptor("timing.framePacingSourceFrameRate", "timing", "framePacingSourceFrameRate"),
                new PlaybackQualityReportSignalDescriptor("timing.lateFrameDropToleranceMs", "timing", "lateFrameDropToleranceMs"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalMsP95", "timing", "renderIntervalMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalMsP99", "timing", "renderIntervalMsP99"),
                new PlaybackQualityReportSignalDescriptor("sync.audioVideoDriftMsP95", "sync", "audioVideoDriftMsP95"),
                new PlaybackQualityReportSignalDescriptor("buffers.submittedAudioFrames", "buffers", "submittedAudioFrames"),
                new PlaybackQualityReportSignalDescriptor("buffers.queuedAudioBuffers", "buffers", "queuedAudioBuffers"),
                new PlaybackQualityReportSignalDescriptor("buffers.videoStarvedPasses", "buffers", "videoStarvedPasses"),
                new PlaybackQualityReportSignalDescriptor("buffers.audioStarvedPasses", "buffers", "audioStarvedPasses"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.actualHdrOutput", "colorPipeline", "actualHdrOutput"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.dxgiInput", "colorPipeline", "dxgiInput"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.dxgiOutput", "colorPipeline", "dxgiOutput"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.conversionStatus", "colorPipeline", "conversionStatus"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.forceSdrOutput", "colorPipeline", "forceSdrOutput"),
                new PlaybackQualityReportSignalDescriptor("display.refreshRateHz", "display", "refreshRateHz")
            };

        public static IReadOnlyList<PlaybackQualityReportSignalDescriptor> ReportSignals => ReportSignalList;
    }
}
