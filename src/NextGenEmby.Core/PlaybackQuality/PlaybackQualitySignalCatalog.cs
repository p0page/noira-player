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

    public sealed class PlaybackQualitySignalDescriptor
    {
        public PlaybackQualitySignalDescriptor(string signal, string kind)
        {
            Signal = signal;
            Kind = kind;
        }

        public string Signal { get; }

        public string Kind { get; }
    }

    public static class PlaybackQualitySignalCatalog
    {
        private static readonly IReadOnlyList<PlaybackQualityReportSignalDescriptor> ReportSignalList =
            new List<PlaybackQualityReportSignalDescriptor>
            {
                new PlaybackQualityReportSignalDescriptor("environment.collectorVersion", "environment", "collectorVersion"),
                new PlaybackQualityReportSignalDescriptor("environment.playerCoreVersion", "environment", "playerCoreVersion"),
                new PlaybackQualityReportSignalDescriptor("environment.sourceRevision", "environment", "sourceRevision"),
                new PlaybackQualityReportSignalDescriptor("environment.buildConfiguration", "environment", "buildConfiguration"),
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
                new PlaybackQualityReportSignalDescriptor("startup.commandReceivedAt", "startup", "commandReceivedAt"),
                new PlaybackQualityReportSignalDescriptor("startup.playbackStartedAt", "startup", "playbackStartedAt"),
                new PlaybackQualityReportSignalDescriptor("startup.startupDurationMs", "startup", "startupDurationMs"),
                new PlaybackQualityReportSignalDescriptor("timing.renderedVideoFrames", "timing", "renderedVideoFrames"),
                new PlaybackQualityReportSignalDescriptor("timing.droppedVideoFrames", "timing", "droppedVideoFrames"),
                new PlaybackQualityReportSignalDescriptor("timing.expectedFrameDurationMs", "timing", "expectedFrameDurationMs"),
                new PlaybackQualityReportSignalDescriptor("timing.maxFrameGapMs", "timing", "maxFrameGapMs"),
                new PlaybackQualityReportSignalDescriptor("timing.framePacingSourceFrameRate", "timing", "framePacingSourceFrameRate"),
                new PlaybackQualityReportSignalDescriptor("timing.lateFrameDropToleranceMs", "timing", "lateFrameDropToleranceMs"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalMsP95", "timing", "renderIntervalMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalMsP99", "timing", "renderIntervalMsP99"),
                new PlaybackQualityReportSignalDescriptor("sync.audioClockTicks", "sync", "audioClockTicks"),
                new PlaybackQualityReportSignalDescriptor("sync.videoPositionTicks", "sync", "videoPositionTicks"),
                new PlaybackQualityReportSignalDescriptor("sync.audioVideoDriftMsP50", "sync", "audioVideoDriftMsP50"),
                new PlaybackQualityReportSignalDescriptor("sync.audioVideoDriftMsP95", "sync", "audioVideoDriftMsP95"),
                new PlaybackQualityReportSignalDescriptor("sync.audioVideoDriftMsP99", "sync", "audioVideoDriftMsP99"),
                new PlaybackQualityReportSignalDescriptor("sync.audioVideoDriftMsMax", "sync", "audioVideoDriftMsMax"),
                new PlaybackQualityReportSignalDescriptor("buffers.submittedAudioFrames", "buffers", "submittedAudioFrames"),
                new PlaybackQualityReportSignalDescriptor("buffers.queuedAudioBuffers", "buffers", "queuedAudioBuffers"),
                new PlaybackQualityReportSignalDescriptor("buffers.videoStarvedPasses", "buffers", "videoStarvedPasses"),
                new PlaybackQualityReportSignalDescriptor("buffers.audioStarvedPasses", "buffers", "audioStarvedPasses"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.actualHdrOutput", "colorPipeline", "actualHdrOutput"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.swapChainFormat", "colorPipeline", "swapChainFormat"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.swapChainColorSpace", "colorPipeline", "swapChainColorSpace"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.isTenBitSwapChain", "colorPipeline", "isTenBitSwapChain"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.dxgiInput", "colorPipeline", "dxgiInput"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.dxgiOutput", "colorPipeline", "dxgiOutput"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.conversionStatus", "colorPipeline", "conversionStatus"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.isVideoProcessorColorSpaceValidated", "colorPipeline", "isVideoProcessorColorSpaceValidated"),
                new PlaybackQualityReportSignalDescriptor("colorPipeline.forceSdrOutput", "colorPipeline", "forceSdrOutput"),
                new PlaybackQualityReportSignalDescriptor("display.hdrStatus", "display", "hdrStatus"),
                new PlaybackQualityReportSignalDescriptor("display.refreshRateHz", "display", "refreshRateHz")
            };

        private static readonly IReadOnlyList<PlaybackQualitySignalDescriptor> ModelSignalList =
            new List<PlaybackQualitySignalDescriptor>
            {
                new PlaybackQualitySignalDescriptor("sample.status", "model"),
                new PlaybackQualitySignalDescriptor("cadence.clockSpeedAdjustmentPercent", "model"),
                new PlaybackQualitySignalDescriptor("cadence.isFractionalCadence", "model"),
                new PlaybackQualitySignalDescriptor("sync.clockDeltaMs", "model"),
                new PlaybackQualitySignalDescriptor("sync.driftDirection", "model"),
                new PlaybackQualitySignalDescriptor("framePacing.renderIntervalP95FrameRatio", "model"),
                new PlaybackQualitySignalDescriptor("framePacing.renderIntervalP99FrameRatio", "model"),
                new PlaybackQualitySignalDescriptor("framePacing.maxFrameGapFrameRatio", "model"),
                new PlaybackQualitySignalDescriptor("framePacing.droppedVideoFramePercent", "model"),
                new PlaybackQualitySignalDescriptor("framePacing.lateFrameDropToleranceFrameRatio", "model")
            };

        private static readonly IReadOnlyList<PlaybackQualitySignalDescriptor> KnownSignalList =
            CreateKnownSignals();

        public static IReadOnlyList<PlaybackQualityReportSignalDescriptor> ReportSignals => ReportSignalList;

        public static IReadOnlyList<PlaybackQualitySignalDescriptor> ModelSignals => ModelSignalList;

        public static IReadOnlyList<PlaybackQualitySignalDescriptor> KnownSignals => KnownSignalList;

        private static IReadOnlyList<PlaybackQualitySignalDescriptor> CreateKnownSignals()
        {
            var signals = new List<PlaybackQualitySignalDescriptor>();
            foreach (var signal in ReportSignalList)
            {
                signals.Add(new PlaybackQualitySignalDescriptor(signal.Signal, "report"));
            }

            foreach (var signal in ModelSignalList)
            {
                signals.Add(signal);
            }

            return signals;
        }
    }
}
