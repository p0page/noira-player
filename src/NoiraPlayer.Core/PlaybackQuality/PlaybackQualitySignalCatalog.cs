using System.Collections.Generic;

namespace NoiraPlayer.Core.PlaybackQuality
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
                new PlaybackQualityReportSignalDescriptor("error.code", "error", "code"),
                new PlaybackQualityReportSignalDescriptor("error.message", "error", "message"),
                new PlaybackQualityReportSignalDescriptor("error.operation", "error", "operation"),
                new PlaybackQualityReportSignalDescriptor("error.exceptionType", "error", "exceptionType"),
                new PlaybackQualityReportSignalDescriptor("error.failureClass", "error", "failureClass"),
                new PlaybackQualityReportSignalDescriptor("error.failureArea", "error", "failureArea"),
                new PlaybackQualityReportSignalDescriptor("error.isTerminal", "error", "isTerminal"),
                new PlaybackQualityReportSignalDescriptor("error.isRetriable", "error", "isRetriable"),
                new PlaybackQualityReportSignalDescriptor("skip.code", "skip", "code"),
                new PlaybackQualityReportSignalDescriptor("skip.reason", "skip", "reason"),
                new PlaybackQualityReportSignalDescriptor("skip.operation", "skip", "operation"),
                new PlaybackQualityReportSignalDescriptor("skip.failureClass", "skip", "failureClass"),
                new PlaybackQualityReportSignalDescriptor("skip.failureArea", "skip", "failureArea"),
                new PlaybackQualityReportSignalDescriptor("skip.isExpected", "skip", "isExpected"),
                new PlaybackQualityReportSignalDescriptor("skip.isRetriable", "skip", "isRetriable"),
                new PlaybackQualityReportSignalDescriptor("runtimeMetrics.status", "runtimeMetrics", "status"),
                new PlaybackQualityReportSignalDescriptor("runtimeMetrics.providerStatus", "runtimeMetrics", "providerStatus"),
                new PlaybackQualityReportSignalDescriptor("runtimeMetrics.reason", "runtimeMetrics", "reason"),
                new PlaybackQualityReportSignalDescriptor("runtimeMetrics.hasSnapshot", "runtimeMetrics", "hasSnapshot"),
                new PlaybackQualityReportSignalDescriptor("runtimeMetrics.hasPlaybackSample", "runtimeMetrics", "hasPlaybackSample"),
                new PlaybackQualityReportSignalDescriptor("runtimeMetrics.processWallClockMs", "runtimeMetrics", "processWallClockMs"),
                new PlaybackQualityReportSignalDescriptor("runtimeMetrics.processCpuTimeMs", "runtimeMetrics", "processCpuTimeMs"),
                new PlaybackQualityReportSignalDescriptor("runtimeMetrics.processCpuUtilizationRatio", "runtimeMetrics", "processCpuUtilizationRatio"),
                new PlaybackQualityReportSignalDescriptor("source.hasDirectStreamUrl", "source", "hasDirectStreamUrl"),
                new PlaybackQualityReportSignalDescriptor("source.directStreamProtocol", "source", "directStreamProtocol"),
                new PlaybackQualityReportSignalDescriptor("source.container", "source", "container"),
                new PlaybackQualityReportSignalDescriptor("source.bitrate", "source", "bitrate"),
                new PlaybackQualityReportSignalDescriptor("source.durationTicks", "source", "durationTicks"),
                new PlaybackQualityReportSignalDescriptor("source.containerStartTimeTicks", "source", "containerStartTimeTicks"),
                new PlaybackQualityReportSignalDescriptor("source.videoStreamStartTimeTicks", "source", "videoStreamStartTimeTicks"),
                new PlaybackQualityReportSignalDescriptor("source.codec", "source", "codec"),
                new PlaybackQualityReportSignalDescriptor("source.width", "source", "width"),
                new PlaybackQualityReportSignalDescriptor("source.height", "source", "height"),
                new PlaybackQualityReportSignalDescriptor("source.frameRate", "source", "frameRate"),
                new PlaybackQualityReportSignalDescriptor("source.videoRange", "source", "videoRange"),
                new PlaybackQualityReportSignalDescriptor("source.colorPrimaries", "source", "colorPrimaries"),
                new PlaybackQualityReportSignalDescriptor("source.colorTransfer", "source", "colorTransfer"),
                new PlaybackQualityReportSignalDescriptor("source.colorSpace", "source", "colorSpace"),
                new PlaybackQualityReportSignalDescriptor("source.hasChapterMetadata", "source", "hasChapterMetadata"),
                new PlaybackQualityReportSignalDescriptor("source.chapterCount", "source", "chapterCount"),
                new PlaybackQualityReportSignalDescriptor("source.chapters.name", "source.chapters", "name"),
                new PlaybackQualityReportSignalDescriptor("source.chapters.startPositionTicks", "source.chapters", "startPositionTicks"),
                new PlaybackQualityReportSignalDescriptor("source.chapters.imageTag", "source.chapters", "imageTag"),
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
                new PlaybackQualityReportSignalDescriptor("interaction.scenario", "interaction", "scenario"),
                new PlaybackQualityReportSignalDescriptor("interaction.attempted", "interaction", "attempted"),
                new PlaybackQualityReportSignalDescriptor("interaction.operationDurationMs", "interaction", "operationDurationMs"),
                new PlaybackQualityReportSignalDescriptor("interaction.lockWaitDurationMs", "interaction", "lockWaitDurationMs"),
                new PlaybackQualityReportSignalDescriptor("interaction.executionDurationMs", "interaction", "executionDurationMs"),
                new PlaybackQualityReportSignalDescriptor("interaction.quiesceDurationMs", "interaction", "quiesceDurationMs"),
                new PlaybackQualityReportSignalDescriptor("interaction.seekDurationMs", "interaction", "seekDurationMs"),
                new PlaybackQualityReportSignalDescriptor("interaction.decoderOpenDurationMs", "interaction", "decoderOpenDurationMs"),
                new PlaybackQualityReportSignalDescriptor("interaction.rendererOpenDurationMs", "interaction", "rendererOpenDurationMs"),
                new PlaybackQualityReportSignalDescriptor("interaction.packetCacheHit", "interaction", "packetCacheHit"),
                new PlaybackQualityReportSignalDescriptor("interaction.packetCacheEnabled", "interaction", "packetCacheEnabled"),
                new PlaybackQualityReportSignalDescriptor("interaction.packetCachePacketCount", "interaction", "packetCachePacketCount"),
                new PlaybackQualityReportSignalDescriptor("interaction.packetCacheBytes", "interaction", "packetCacheBytes"),
                new PlaybackQualityReportSignalDescriptor("interaction.packetCacheWindowDurationTicks", "interaction", "packetCacheWindowDurationTicks"),
                new PlaybackQualityReportSignalDescriptor("interaction.recoveryDurationMs", "interaction", "recoveryDurationMs"),
                new PlaybackQualityReportSignalDescriptor("interaction.cueRenderDurationMs", "interaction", "cueRenderDurationMs"),
                new PlaybackQualityReportSignalDescriptor("interaction.positionDeltaTicks", "interaction", "positionDeltaTicks"),
                new PlaybackQualityReportSignalDescriptor("interaction.submittedAudioFrameDelta", "interaction", "submittedAudioFrameDelta"),
                new PlaybackQualityReportSignalDescriptor("interaction.renderedVideoFrameDelta", "interaction", "renderedVideoFrameDelta"),
                new PlaybackQualityReportSignalDescriptor("interaction.subtitleCueRenderCountDelta", "interaction", "subtitleCueRenderCountDelta"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.load", "lifecycle", "load"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.play", "lifecycle", "play"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.pause", "lifecycle", "pause"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.resume", "lifecycle", "resume"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.seek", "lifecycle", "seek"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.stop", "lifecycle", "stop"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.endOfStream", "lifecycle", "endOfStream"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.audio-switch", "lifecycle", "audio-switch"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.subtitle-switch", "lifecycle", "subtitle-switch"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.subtitle-off", "lifecycle", "subtitle-off"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.error", "lifecycle", "error"),
                new PlaybackQualityReportSignalDescriptor("lifecycle.skip", "lifecycle", "skip"),
                new PlaybackQualityReportSignalDescriptor("position.requestedStartPositionTicks", "position", "requestedStartPositionTicks"),
                new PlaybackQualityReportSignalDescriptor("position.seekTargetPositionTicks", "position", "seekTargetPositionTicks"),
                new PlaybackQualityReportSignalDescriptor("position.seekDemuxTargetTicks", "position", "seekDemuxTargetTicks"),
                new PlaybackQualityReportSignalDescriptor("position.actualPositionTicks", "position", "actualPositionTicks"),
                new PlaybackQualityReportSignalDescriptor("position.firstPresentedPositionTicks", "position", "firstPresentedPositionTicks"),
                new PlaybackQualityReportSignalDescriptor("position.postSeekPositionTicks", "position", "postSeekPositionTicks"),
                new PlaybackQualityReportSignalDescriptor("position.postSeekAdvanced", "position", "postSeekAdvanced"),
                new PlaybackQualityReportSignalDescriptor("position.seekPositionErrorMs", "position", "seekPositionErrorMs"),
                new PlaybackQualityReportSignalDescriptor("position.seekOperationDurationMs", "position", "seekOperationDurationMs"),
                new PlaybackQualityReportSignalDescriptor("position.seekRecoveryDurationMs", "position", "seekRecoveryDurationMs"),
                new PlaybackQualityReportSignalDescriptor("tracks.videoTrackCount", "tracks", "videoTrackCount"),
                new PlaybackQualityReportSignalDescriptor("tracks.audioTrackCount", "tracks", "audioTrackCount"),
                new PlaybackQualityReportSignalDescriptor("tracks.subtitleTrackCount", "tracks", "subtitleTrackCount"),
                new PlaybackQualityReportSignalDescriptor("tracks.selectedVideoStreamIndex", "tracks", "selectedVideoStreamIndex"),
                new PlaybackQualityReportSignalDescriptor("tracks.selectedAudioStreamIndex", "tracks", "selectedAudioStreamIndex"),
                new PlaybackQualityReportSignalDescriptor("tracks.selectedSubtitleStreamIndex", "tracks", "selectedSubtitleStreamIndex"),
                new PlaybackQualityReportSignalDescriptor("tracks.subtitleDecodedCueCount", "tracks", "subtitleDecodedCueCount"),
                new PlaybackQualityReportSignalDescriptor("tracks.subtitleCueRenderCount", "tracks", "subtitleCueRenderCount"),
                new PlaybackQualityReportSignalDescriptor("tracks.isSubtitleDisabled", "tracks", "isSubtitleDisabled"),
                new PlaybackQualityReportSignalDescriptor("tracks.video.index", "tracks.video", "index"),
                new PlaybackQualityReportSignalDescriptor("tracks.video.codec", "tracks.video", "codec"),
                new PlaybackQualityReportSignalDescriptor("tracks.video.language", "tracks.video", "language"),
                new PlaybackQualityReportSignalDescriptor("tracks.video.displayTitle", "tracks.video", "displayTitle"),
                new PlaybackQualityReportSignalDescriptor("tracks.video.isExternal", "tracks.video", "isExternal"),
                new PlaybackQualityReportSignalDescriptor("tracks.video.isDefault", "tracks.video", "isDefault"),
                new PlaybackQualityReportSignalDescriptor("tracks.video.isForced", "tracks.video", "isForced"),
                new PlaybackQualityReportSignalDescriptor("tracks.video.realFrameRate", "tracks.video", "realFrameRate"),
                new PlaybackQualityReportSignalDescriptor("tracks.video.averageFrameRate", "tracks.video", "averageFrameRate"),
                new PlaybackQualityReportSignalDescriptor("tracks.audio.index", "tracks.audio", "index"),
                new PlaybackQualityReportSignalDescriptor("tracks.audio.codec", "tracks.audio", "codec"),
                new PlaybackQualityReportSignalDescriptor("tracks.audio.language", "tracks.audio", "language"),
                new PlaybackQualityReportSignalDescriptor("tracks.audio.channelLayout", "tracks.audio", "channelLayout"),
                new PlaybackQualityReportSignalDescriptor("tracks.audio.channels", "tracks.audio", "channels"),
                new PlaybackQualityReportSignalDescriptor("tracks.audio.displayTitle", "tracks.audio", "displayTitle"),
                new PlaybackQualityReportSignalDescriptor("tracks.audio.isExternal", "tracks.audio", "isExternal"),
                new PlaybackQualityReportSignalDescriptor("tracks.audio.isDefault", "tracks.audio", "isDefault"),
                new PlaybackQualityReportSignalDescriptor("tracks.audio.isForced", "tracks.audio", "isForced"),
                new PlaybackQualityReportSignalDescriptor("tracks.subtitles.index", "tracks.subtitles", "index"),
                new PlaybackQualityReportSignalDescriptor("tracks.subtitles.codec", "tracks.subtitles", "codec"),
                new PlaybackQualityReportSignalDescriptor("tracks.subtitles.language", "tracks.subtitles", "language"),
                new PlaybackQualityReportSignalDescriptor("tracks.subtitles.displayTitle", "tracks.subtitles", "displayTitle"),
                new PlaybackQualityReportSignalDescriptor("tracks.subtitles.isExternal", "tracks.subtitles", "isExternal"),
                new PlaybackQualityReportSignalDescriptor("tracks.subtitles.isDefault", "tracks.subtitles", "isDefault"),
                new PlaybackQualityReportSignalDescriptor("tracks.subtitles.isForced", "tracks.subtitles", "isForced"),
                new PlaybackQualityReportSignalDescriptor("timing.renderedVideoFrames", "timing", "renderedVideoFrames"),
                new PlaybackQualityReportSignalDescriptor("timing.hardwareDecodedVideoFrames", "timing", "hardwareDecodedVideoFrames"),
                new PlaybackQualityReportSignalDescriptor("timing.softwareDecodedVideoFrames", "timing", "softwareDecodedVideoFrames"),
                new PlaybackQualityReportSignalDescriptor("timing.droppedVideoFrames", "timing", "droppedVideoFrames"),
                new PlaybackQualityReportSignalDescriptor("timing.expectedFrameDurationMs", "timing", "expectedFrameDurationMs"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalMsP05", "timing", "renderIntervalMsP05"),
                new PlaybackQualityReportSignalDescriptor("timing.minFrameGapMs", "timing", "minFrameGapMs"),
                new PlaybackQualityReportSignalDescriptor("timing.maxFrameGapMs", "timing", "maxFrameGapMs"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalSampleCount", "timing", "renderIntervalSampleCount"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalOverExpected2MsCount", "timing", "renderIntervalOverExpected2MsCount"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalOverExpected4MsCount", "timing", "renderIntervalOverExpected4MsCount"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalUnderExpected2MsCount", "timing", "renderIntervalUnderExpected2MsCount"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalUnderExpected4MsCount", "timing", "renderIntervalUnderExpected4MsCount"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalAfterAudioAheadWaitSampleCount", "timing", "renderIntervalAfterAudioAheadWaitSampleCount"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalAfterAudioAheadWaitMsP95", "timing", "renderIntervalAfterAudioAheadWaitMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalAfterAudioAheadWaitMsP99", "timing", "renderIntervalAfterAudioAheadWaitMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalAfterAudioAheadWaitMsMax", "timing", "renderIntervalAfterAudioAheadWaitMsMax"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitEndToPresentSampleCount", "timing", "audioAheadWaitEndToPresentSampleCount"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitEndToPresentMsP50", "timing", "audioAheadWaitEndToPresentMsP50"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitEndToPresentMsP95", "timing", "audioAheadWaitEndToPresentMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitEndToPresentMsP99", "timing", "audioAheadWaitEndToPresentMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitEndToPresentMsMax", "timing", "audioAheadWaitEndToPresentMsMax"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalAfterNonAudioWaitSampleCount", "timing", "renderIntervalAfterNonAudioWaitSampleCount"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalAfterNonAudioWaitMsP95", "timing", "renderIntervalAfterNonAudioWaitMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalAfterNonAudioWaitMsP99", "timing", "renderIntervalAfterNonAudioWaitMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalAfterNonAudioWaitMsMax", "timing", "renderIntervalAfterNonAudioWaitMsMax"),
                new PlaybackQualityReportSignalDescriptor("timing.framePacingSourceFrameRate", "timing", "framePacingSourceFrameRate"),
                new PlaybackQualityReportSignalDescriptor("timing.lateFrameDropToleranceMs", "timing", "lateFrameDropToleranceMs"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalMsP95", "timing", "renderIntervalMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.renderIntervalMsP99", "timing", "renderIntervalMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.presentDurationMsP50", "timing", "presentDurationMsP50"),
                new PlaybackQualityReportSignalDescriptor("timing.presentDurationMsP95", "timing", "presentDurationMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.presentDurationMsP99", "timing", "presentDurationMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.presentDurationMsMax", "timing", "presentDurationMsMax"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitDurationMsP50", "timing", "audioAheadWaitDurationMsP50"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitDurationMsP95", "timing", "audioAheadWaitDurationMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitDurationMsP99", "timing", "audioAheadWaitDurationMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitDurationMsMax", "timing", "audioAheadWaitDurationMsMax"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitTargetMsP50", "timing", "audioAheadWaitTargetMsP50"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitTargetMsP95", "timing", "audioAheadWaitTargetMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitTargetMsP99", "timing", "audioAheadWaitTargetMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitTargetMsMax", "timing", "audioAheadWaitTargetMsMax"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitOversleepMsP50", "timing", "audioAheadWaitOversleepMsP50"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitOversleepMsP95", "timing", "audioAheadWaitOversleepMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitOversleepMsP99", "timing", "audioAheadWaitOversleepMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitOversleepMsMax", "timing", "audioAheadWaitOversleepMsMax"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitFinalDeltaAbsMsP50", "timing", "audioAheadWaitFinalDeltaAbsMsP50"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitFinalDeltaAbsMsP95", "timing", "audioAheadWaitFinalDeltaAbsMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitFinalDeltaAbsMsP99", "timing", "audioAheadWaitFinalDeltaAbsMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitFinalDeltaAbsMsMax", "timing", "audioAheadWaitFinalDeltaAbsMsMax"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitEpisodeCount", "timing", "audioAheadWaitEpisodeCount"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassesPerEpisodeP50", "timing", "audioAheadWaitPassesPerEpisodeP50"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassesPerEpisodeP95", "timing", "audioAheadWaitPassesPerEpisodeP95"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassesPerEpisodeP99", "timing", "audioAheadWaitPassesPerEpisodeP99"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassesPerEpisodeMax", "timing", "audioAheadWaitPassesPerEpisodeMax"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassDurationMsP50", "timing", "audioAheadWaitPassDurationMsP50"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassDurationMsP95", "timing", "audioAheadWaitPassDurationMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassDurationMsP99", "timing", "audioAheadWaitPassDurationMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassDurationMsMax", "timing", "audioAheadWaitPassDurationMsMax"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassTargetMsP50", "timing", "audioAheadWaitPassTargetMsP50"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassTargetMsP95", "timing", "audioAheadWaitPassTargetMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassTargetMsP99", "timing", "audioAheadWaitPassTargetMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassTargetMsMax", "timing", "audioAheadWaitPassTargetMsMax"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassOversleepMsP50", "timing", "audioAheadWaitPassOversleepMsP50"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassOversleepMsP95", "timing", "audioAheadWaitPassOversleepMsP95"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassOversleepMsP99", "timing", "audioAheadWaitPassOversleepMsP99"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitPassOversleepMsMax", "timing", "audioAheadWaitPassOversleepMsMax"),
                new PlaybackQualityReportSignalDescriptor("timing.videoAheadWaitCount", "timing", "videoAheadWaitCount"),
                new PlaybackQualityReportSignalDescriptor("timing.audioAheadWaitCount", "timing", "audioAheadWaitCount"),
                new PlaybackQualityReportSignalDescriptor("timing.videoClockWaitCount", "timing", "videoClockWaitCount"),
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
                new PlaybackQualitySignalDescriptor("framePacing.renderIntervalP95ExpectedErrorMs", "model"),
                new PlaybackQualitySignalDescriptor("framePacing.renderIntervalP99ExpectedErrorMs", "model"),
                new PlaybackQualitySignalDescriptor("framePacing.maxFrameGapExpectedErrorMs", "model"),
                new PlaybackQualitySignalDescriptor("framePacing.droppedVideoFramePercent", "model"),
                new PlaybackQualitySignalDescriptor("framePacing.lateFrameDropToleranceFrameRatio", "model")
            };

        private static readonly IReadOnlyList<PlaybackQualitySignalDescriptor> KnownSignalList =
            CreateKnownSignals();

        public static IReadOnlyList<PlaybackQualityReportSignalDescriptor> ReportSignals => ReportSignalList;

        public static IReadOnlyList<PlaybackQualitySignalDescriptor> ModelSignals => ModelSignalList;

        public static IReadOnlyList<PlaybackQualitySignalDescriptor> KnownSignals => KnownSignalList;

        public static bool IsReportSignal(string signal)
        {
            foreach (var descriptor in ReportSignalList)
            {
                if (string.Equals(
                    descriptor.Signal,
                    signal,
                    System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

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
