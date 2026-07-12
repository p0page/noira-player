namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityMetricsSnapshot
    {
        public ulong RenderPasses { get; set; }
        public ulong DecodedVideoFrames { get; set; }
        public ulong HardwareDecodedVideoFrames { get; set; }
        public ulong SoftwareDecodedVideoFrames { get; set; }
        public ulong RenderedVideoFrames { get; set; }
        public ulong SubmittedAudioFrames { get; set; }
        public int SelectedAudioStreamIndex { get; set; } = -1;
        public ulong SubtitleDecodedCueCount { get; set; }
        public ulong SubtitleCueRenderCount { get; set; }
        public int SelectedSubtitleStreamIndex { get; set; } = -1;
        public ulong DroppedVideoFrames { get; set; }
        public ulong SeekPrerollDroppedFrames { get; set; }
        public ulong VideoAheadWaitCount { get; set; }
        public ulong AudioAheadWaitCount { get; set; }
        public ulong VideoClockWaitCount { get; set; }
        public ulong VideoStarvedPasses { get; set; }
        public ulong AudioStarvedPasses { get; set; }
        public ulong QueuedAudioBuffers { get; set; }
        public long AudioClockTicks { get; set; }
        public long VideoPositionTicks { get; set; }
        public double NativeGraphOpenDurationMs { get; set; }
        public double FfmpegOpenInputDurationMs { get; set; }
        public double FfmpegStreamInfoDurationMs { get; set; }
        public double NativeStartupSeekDurationMs { get; set; }
        public double NativeFirstFrameDurationMs { get; set; }
        public long? ContainerStartTimeTicks { get; set; }
        public long? VideoStreamStartTimeTicks { get; set; }
        public long? SeekDemuxTargetTicks { get; set; }
        public long? FirstPresentedPositionTicks { get; set; }
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
            "sum-positive-pass-oversleep-v2";
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
        public double AudioVideoDriftMsP50 { get; set; }
        public double AudioVideoDriftMsP95 { get; set; }
        public double AudioVideoDriftMsP99 { get; set; }
        public double AudioVideoDriftMsMax { get; set; }
        public string LastInteractionScenario { get; set; } = "";
        public ulong LastInteractionSequence { get; set; }
        public double LastInteractionLockWaitDurationMs { get; set; }
        public double LastInteractionExecutionDurationMs { get; set; }
        public double LastInteractionQuiesceDurationMs { get; set; }
        public double LastInteractionSeekDurationMs { get; set; }
        public double LastInteractionDecoderOpenDurationMs { get; set; }
        public double LastInteractionRendererOpenDurationMs { get; set; }
        public bool LastInteractionPacketCacheHit { get; set; }
        public bool LastInteractionPacketCacheEnabled { get; set; }
        public ulong LastInteractionPacketCachePacketCount { get; set; }
        public ulong LastInteractionPacketCacheBytes { get; set; }
        public long LastInteractionPacketCacheWindowDurationTicks { get; set; }
    }
}
