using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityReport
    {
        public int SchemaVersion { get; set; } = 1;
        public string MetricVersion { get; set; } = "software-quality-v1";
        public string RunId { get; set; } = "";
        public string Result { get; set; } = "observed";
        public List<string> FailureReasons { get; } = new List<string>();
        public PlaybackQualityAnalysis Analysis { get; set; } = new PlaybackQualityAnalysis();
        public List<PlaybackQualityCheck> Checks { get; } = new List<PlaybackQualityCheck>();
        public PlaybackQualityEnvironment Environment { get; set; } = new PlaybackQualityEnvironment();
        public PlaybackQualityExecutionEvidence Execution { get; set; } =
            new PlaybackQualityExecutionEvidence();
        public List<string> Limitations { get; } = new List<string>
        {
            "software-only: does not verify actual HDMI InfoFrame output",
            "software-only: does not verify display panel EOTF, luminance, or color accuracy",
            "software-only: does not detect TV-side tone mapping",
            "internal-timing: frame intervals are measured in the player, not by an external photodiode or HDMI capture device"
        };
        public PlaybackQualityExpected? Expected { get; set; }
        public PlaybackQualitySource Source { get; set; } = new PlaybackQualitySource();
        public PlaybackQualityStartup Startup { get; set; } = new PlaybackQualityStartup();
        public PlaybackQualityLifecycle Lifecycle { get; set; } = new PlaybackQualityLifecycle();
        public PlaybackQualityInteractionEvidence Interaction { get; set; } =
            new PlaybackQualityInteractionEvidence();
        public PlaybackQualityPosition Position { get; set; } = new PlaybackQualityPosition();
        public PlaybackQualityTracks Tracks { get; set; } = new PlaybackQualityTracks();
        public PlaybackQualityError Error { get; set; } = new PlaybackQualityError();
        public PlaybackQualitySkip Skip { get; set; } = new PlaybackQualitySkip();
        public PlaybackQualityRuntimeMetrics RuntimeMetrics { get; set; } = new PlaybackQualityRuntimeMetrics();
        public PlaybackQualityTiming Timing { get; set; } = new PlaybackQualityTiming();
        public PlaybackQualitySync Sync { get; set; } = new PlaybackQualitySync();
        public PlaybackQualityBuffers Buffers { get; set; } = new PlaybackQualityBuffers();
        public PlaybackQualityColorPipeline ColorPipeline { get; set; } = new PlaybackQualityColorPipeline();
        public PlaybackQualityDisplay Display { get; set; } = new PlaybackQualityDisplay();
    }

    public sealed class PlaybackQualityCheck
    {
        public string Name { get; set; } = "";
        public string Signal { get; set; } = "";
        public string Status { get; set; } = "not-applicable";
        public string FailureArea { get; set; } = "";
        public string FailureClass { get; set; } = "";
        public string Expected { get; set; } = "";
        public string Actual { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public sealed class PlaybackQualityAnalysis
    {
        public string PrimaryFailureArea { get; set; } = "none";
        public string SuggestedNextAction { get; set; } = "";
        public List<string> RelevantSignals { get; } = new List<string>();
        public List<string> IgnoredSignals { get; } = new List<string>();
    }

    public sealed class PlaybackQualityEnvironment
    {
        public string CollectorVersion { get; set; } = "";
        public string PlayerCoreVersion { get; set; } = "";
        public string SourceRevision { get; set; } = "";
        public string BuildConfiguration { get; set; } = "";
    }

    public sealed class PlaybackQualitySource
    {
        public string ItemId { get; set; } = "";
        public string MediaSourceId { get; set; } = "";
        public bool HasDirectStreamUrl { get; set; }
        public string DirectStreamProtocol { get; set; } = "";
        public string Container { get; set; } = "";
        public long Bitrate { get; set; }
        public long DurationTicks { get; set; }
        public long? ContainerStartTimeTicks { get; set; }
        public long? VideoStreamStartTimeTicks { get; set; }
        public string Codec { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public double FrameRate { get; set; }
        public string VideoRange { get; set; } = "";
        public string ColorPrimaries { get; set; } = "";
        public string ColorTransfer { get; set; } = "";
        public string ColorSpace { get; set; } = "";
        public bool HasChapterMetadata { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ChapterCount { get; set; }
        public List<PlaybackQualityChapter> Chapters { get; } = new List<PlaybackQualityChapter>();
        public string HdrKind { get; set; } = "";
        public string HdrPlaybackStrategy { get; set; } = "";
        public bool IsHdr { get; set; }
        public bool IsDirectPlayable { get; set; }
        public bool IsDolbyVision { get; set; }
        public int? DolbyVisionProfile { get; set; }
        public int? DolbyVisionCompatibilityId { get; set; }
        public bool HasHdr10BaseLayer { get; set; }
        public bool HasHlgBaseLayer { get; set; }
        public string AudioCodec { get; set; } = "";
    }

    public sealed class PlaybackQualityChapter
    {
        public string Name { get; set; } = "";
        public long StartPositionTicks { get; set; }
        public string ImageTag { get; set; } = "";
    }

    public sealed class PlaybackQualityStartup
    {
        public string CommandReceivedAt { get; set; } = "";
        public string PlaybackStartedAt { get; set; } = "";
        public double StartupDurationMs { get; set; }
        public List<PlaybackQualityStartupStage> Stages { get; } =
            new List<PlaybackQualityStartupStage>();
    }

    public sealed class PlaybackQualityStartupStage
    {
        public string Name { get; set; } = "";
        public string StartedAt { get; set; } = "";
        public string CompletedAt { get; set; } = "";
        public double DurationMs { get; set; }
        public List<PlaybackQualityStartupComponent> Components { get; } =
            new List<PlaybackQualityStartupComponent>();
    }

    public sealed class PlaybackQualityStartupComponent
    {
        public string Name { get; set; } = "";
        public double DurationMs { get; set; }
        public string Status { get; set; } = "measured";
        public ulong PacketCount { get; set; }
        public ulong TransportBytes { get; set; }
        public ulong PacketPayloadBytes { get; set; }
        public string TransportProvider { get; set; } = "";
        public string TransportCallEvidenceStatus { get; set; } = "not-applicable";
        public ulong? TransportReadCalls { get; set; }
        public ulong? TransportSeekCalls { get; set; }
        public double? TransportReadWaitMs { get; set; }
        public double? TransportSeekWaitMs { get; set; }
        public ulong? TransportSeekDistanceBytes { get; set; }
    }

    public sealed class PlaybackQualityLifecycle
    {
        public List<PlaybackQualityLifecycleEvent> Events { get; } =
            new List<PlaybackQualityLifecycleEvent>();
    }

    public sealed class PlaybackQualityLifecycleEvent
    {
        public string Operation { get; set; } = "";
        public string Status { get; set; } = "observed";
        public string State { get; set; } = "";
        public long? PositionTicks { get; set; }
        public string Message { get; set; } = "";
    }

    public sealed class PlaybackQualityInteractionEvidence
    {
        public string Scenario { get; set; } = "";
        public bool Attempted { get; set; }
        public double? OperationDurationMs { get; set; }
        public double? LockWaitDurationMs { get; set; }
        public double? ExecutionDurationMs { get; set; }
        public double? QuiesceDurationMs { get; set; }
        public double? SeekDurationMs { get; set; }
        public double? DecoderOpenDurationMs { get; set; }
        public double? RendererOpenDurationMs { get; set; }
        public bool? PacketCacheHit { get; set; }
        public bool? PacketCacheEnabled { get; set; }
        public ulong? PacketCachePacketCount { get; set; }
        public ulong? PacketCacheBytes { get; set; }
        public long? PacketCacheWindowDurationTicks { get; set; }
        public double? RecoveryDurationMs { get; set; }
        public double? CueRenderDurationMs { get; set; }
        public long? PositionDeltaTicks { get; set; }
        public ulong? SubmittedAudioFrameDelta { get; set; }
        public ulong? RenderedVideoFrameDelta { get; set; }
        public ulong? SubtitleCueRenderCountDelta { get; set; }
    }

    public sealed class PlaybackQualityRuntimeMetrics
    {
        public string Status { get; set; } = "unknown";
        public string ProviderStatus { get; set; } = "unknown";
        public string Reason { get; set; } = "";
        public bool HasSnapshot { get; set; }
        public bool HasPlaybackSample { get; set; }
        public double ProcessWallClockMs { get; set; }
        public double ProcessCpuTimeMs { get; set; }
        public double ProcessCpuUtilizationRatio { get; set; }
    }

    public sealed class PlaybackQualityPosition
    {
        public long? RequestedStartPositionTicks { get; set; }
        public long? SeekTargetPositionTicks { get; set; }
        public long? SeekDemuxTargetTicks { get; set; }
        public long? ActualPositionTicks { get; set; }
        public long? FirstPresentedPositionTicks { get; set; }
        public long? PostSeekPositionTicks { get; set; }
        public bool? PostSeekAdvanced { get; set; }
        public double? SeekPositionErrorMs { get; set; }
        public double? SeekOperationDurationMs { get; set; }
        public double? SeekRecoveryDurationMs { get; set; }
        public bool? SeekPacketCacheEnabled { get; set; }
        public bool? SeekPacketCacheHit { get; set; }
        public ulong? SeekPacketCachePacketCount { get; set; }
        public ulong? SeekPacketCacheBytes { get; set; }
        public long? SeekPacketCacheWindowDurationTicks { get; set; }
        public string SeekFallbackReason { get; set; } = "";
    }

    public sealed class PlaybackQualitySourceTimeline
    {
        public long? ContainerStartTimeTicks { get; set; }
        public long? VideoStreamStartTimeTicks { get; set; }
    }

    public sealed class PlaybackQualityTracks
    {
        public int VideoTrackCount { get; set; }
        public int AudioTrackCount { get; set; }
        public int SubtitleTrackCount { get; set; }
        public int? SelectedVideoStreamIndex { get; set; }
        public int? SelectedAudioStreamIndex { get; set; }
        public int? SelectedSubtitleStreamIndex { get; set; }
        public ulong SubtitleDecodedCueCount { get; set; }
        public ulong SubtitleCueRenderCount { get; set; }
        public bool IsSubtitleDisabled { get; set; } = true;
        public List<PlaybackQualityTrack> Video { get; } = new List<PlaybackQualityTrack>();
        public List<PlaybackQualityTrack> Audio { get; } = new List<PlaybackQualityTrack>();
        public List<PlaybackQualityTrack> Subtitles { get; } = new List<PlaybackQualityTrack>();
    }

    public sealed class PlaybackQualityTrack
    {
        public int Index { get; set; }
        public string Kind { get; set; } = "";
        public string Codec { get; set; } = "";
        public string Language { get; set; } = "";
        public string ChannelLayout { get; set; } = "";
        public int Channels { get; set; }
        public string DisplayTitle { get; set; } = "";
        public bool IsExternal { get; set; }
        public bool? IsDefault { get; set; }
        public bool? IsForced { get; set; }
        public double RealFrameRate { get; set; }
        public double AverageFrameRate { get; set; }
    }

    public sealed class PlaybackQualityError
    {
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
        public string Operation { get; set; } = "";
        public string ExceptionType { get; set; } = "";
        public string FailureClass { get; set; } = "";
        public string FailureArea { get; set; } = "error-handling";
        public bool IsTerminal { get; set; }
        public bool IsRetriable { get; set; }
    }

    public sealed class PlaybackQualitySkip
    {
        public string Code { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Operation { get; set; } = "";
        public string FailureClass { get; set; } = "";
        public string FailureArea { get; set; } = "evidence-collection";
        public bool IsExpected { get; set; }
        public bool IsRetriable { get; set; }
    }

    public sealed class PlaybackQualityTiming
    {
        public ulong RenderPasses { get; set; }
        public ulong DecodedVideoFrames { get; set; }
        public ulong HardwareDecodedVideoFrames { get; set; }
        public ulong SoftwareDecodedVideoFrames { get; set; }
        public ulong RenderedVideoFrames { get; set; }
        public ulong DroppedVideoFrames { get; set; }
        public ulong SeekPrerollDroppedFrames { get; set; }
        public ulong VideoAheadWaitCount { get; set; }
        public ulong AudioAheadWaitCount { get; set; }
        public ulong VideoClockWaitCount { get; set; }
        public double ExpectedFrameDurationMs { get; set; }
        public double RenderIntervalMsP05 { get; set; }
        public double RenderIntervalMsP50 { get; set; }
        public double RenderIntervalMsP95 { get; set; }
        public double RenderIntervalMsP99 { get; set; }
        public double MinFrameGapMs { get; set; }
        public double MaxFrameGapMs { get; set; }
        public ulong RenderIntervalSampleCount { get; set; }
        public ulong RenderIntervalOverExpected2MsCount { get; set; }
        public ulong RenderIntervalOverExpected4MsCount { get; set; }
        public ulong RenderIntervalUnderExpected2MsCount { get; set; }
        public ulong RenderIntervalUnderExpected4MsCount { get; set; }
        public ulong RenderIntervalAfterAudioAheadWaitSampleCount { get; set; }
        public double RenderIntervalAfterAudioAheadWaitMsP95 { get; set; }
        public double RenderIntervalAfterAudioAheadWaitMsP99 { get; set; }
        public double RenderIntervalAfterAudioAheadWaitMsMax { get; set; }
        public ulong AudioAheadWaitEndToPresentSampleCount { get; set; }
        public double AudioAheadWaitEndToPresentMsP50 { get; set; }
        public double AudioAheadWaitEndToPresentMsP95 { get; set; }
        public double AudioAheadWaitEndToPresentMsP99 { get; set; }
        public double AudioAheadWaitEndToPresentMsMax { get; set; }
        public ulong RenderIntervalAfterNonAudioWaitSampleCount { get; set; }
        public double RenderIntervalAfterNonAudioWaitMsP95 { get; set; }
        public double RenderIntervalAfterNonAudioWaitMsP99 { get; set; }
        public double RenderIntervalAfterNonAudioWaitMsMax { get; set; }
        public double PresentDurationMsP50 { get; set; }
        public double PresentDurationMsP95 { get; set; }
        public double PresentDurationMsP99 { get; set; }
        public double PresentDurationMsMax { get; set; }
        public double AudioAheadWaitDurationMsP50 { get; set; }
        public double AudioAheadWaitDurationMsP95 { get; set; }
        public double AudioAheadWaitDurationMsP99 { get; set; }
        public double AudioAheadWaitDurationMsMax { get; set; }
        public double AudioAheadWaitTargetMsP50 { get; set; }
        public double AudioAheadWaitTargetMsP95 { get; set; }
        public double AudioAheadWaitTargetMsP99 { get; set; }
        public double AudioAheadWaitTargetMsMax { get; set; }
        public string AudioAheadWaitOversleepSemantics { get; set; } =
            "episode-wall-minus-first-target-v1";
        public double AudioAheadWaitOversleepMsP50 { get; set; }
        public double AudioAheadWaitOversleepMsP95 { get; set; }
        public double AudioAheadWaitOversleepMsP99 { get; set; }
        public double AudioAheadWaitOversleepMsMax { get; set; }
        public double AudioAheadWaitFinalDeltaAbsMsP50 { get; set; }
        public double AudioAheadWaitFinalDeltaAbsMsP95 { get; set; }
        public double AudioAheadWaitFinalDeltaAbsMsP99 { get; set; }
        public double AudioAheadWaitFinalDeltaAbsMsMax { get; set; }
        public ulong AudioAheadWaitEpisodeCount { get; set; }
        public double AudioAheadWaitPassesPerEpisodeP50 { get; set; }
        public double AudioAheadWaitPassesPerEpisodeP95 { get; set; }
        public double AudioAheadWaitPassesPerEpisodeP99 { get; set; }
        public double AudioAheadWaitPassesPerEpisodeMax { get; set; }
        public double AudioAheadWaitPassDurationMsP50 { get; set; }
        public double AudioAheadWaitPassDurationMsP95 { get; set; }
        public double AudioAheadWaitPassDurationMsP99 { get; set; }
        public double AudioAheadWaitPassDurationMsMax { get; set; }
        public double AudioAheadWaitPassTargetMsP50 { get; set; }
        public double AudioAheadWaitPassTargetMsP95 { get; set; }
        public double AudioAheadWaitPassTargetMsP99 { get; set; }
        public double AudioAheadWaitPassTargetMsMax { get; set; }
        public double AudioAheadWaitPassOversleepMsP50 { get; set; }
        public double AudioAheadWaitPassOversleepMsP95 { get; set; }
        public double AudioAheadWaitPassOversleepMsP99 { get; set; }
        public double AudioAheadWaitPassOversleepMsMax { get; set; }
        public double FramePacingSourceFrameRate { get; set; }
        public double LateFrameDropToleranceMs { get; set; }
    }

    public sealed class PlaybackQualitySync
    {
        public long AudioClockTicks { get; set; }
        public long VideoPositionTicks { get; set; }
        public double AudioVideoDriftMsP50 { get; set; }
        public double AudioVideoDriftMsP95 { get; set; }
        public double AudioVideoDriftMsP99 { get; set; }
        public double AudioVideoDriftMsMax { get; set; }
    }

    public sealed class PlaybackQualityBuffers
    {
        public ulong SubmittedAudioFrames { get; set; }
        public ulong QueuedAudioBuffers { get; set; }
        public ulong VideoStarvedPasses { get; set; }
        public ulong AudioStarvedPasses { get; set; }
    }

    public sealed class PlaybackQualityColorPipeline
    {
        public string ExpectationProfile { get; set; } = "";
        public string ActualHdrOutput { get; set; } = "";
        public string SwapChainFormat { get; set; } = "";
        public string SwapChainColorSpace { get; set; } = "";
        public bool IsTenBitSwapChain { get; set; }
        public string DxgiInput { get; set; } = "";
        public string DxgiOutput { get; set; } = "";
        public string ConversionStatus { get; set; } = "";
        public bool IsVideoProcessorColorSpaceValidated { get; set; }
        public bool ForceSdrOutput { get; set; }
    }

    public sealed class PlaybackQualityDisplay
    {
        public string HdrStatus { get; set; } = "";
        public bool IsHdrDisplayAvailable { get; set; }
        public bool IsHdrOutputActive { get; set; }
        public double RefreshRateHz { get; set; }
        public string Message { get; set; } = "";
    }
}
