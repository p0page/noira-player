namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityTransportCallSnapshot
    {
        public string Provider { get; set; } = "";
        public bool EvidenceAvailable { get; set; }
        public ulong ReadCalls { get; set; }
        public ulong SeekCalls { get; set; }
        public double ReadWaitMs { get; set; }
        public double SeekWaitMs { get; set; }
        public ulong SeekDistanceBytes { get; set; }
    }

    public sealed class PlaybackQualityMetricsSnapshot
    {
        public bool ObservedVideoSourceAvailable { get; set; }
        public string ObservedVideoCodec { get; set; } = "";
        public uint ObservedVideoWidth { get; set; }
        public uint ObservedVideoHeight { get; set; }
        public double ObservedVideoFrameRate { get; set; }
        public string ObservedVideoRange { get; set; } = "";
        public string ObservedColorPrimaries { get; set; } = "";
        public string ObservedColorTransfer { get; set; } = "";
        public string ObservedColorSpace { get; set; } = "";
        public string ObservedHdrKind { get; set; } = "";
        public bool ObservedIsDolbyVision { get; set; }
        public uint ObservedDolbyVisionProfile { get; set; }
        public uint ObservedDolbyVisionCompatibilityId { get; set; }
        public bool ObservedHasHdr10BaseLayer { get; set; }
        public bool ObservedHasHlgBaseLayer { get; set; }
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
        public ulong FfmpegOpenInputBytesRead { get; set; }
        public ulong FfmpegStreamInfoBytesRead { get; set; }
        public ulong NativeStartupSeekBytesRead { get; set; }
        public ulong NativeFirstFrameTransportBytesRead { get; set; }
        public string StartupTransportProvider { get; set; } = "ffmpeg-builtin";
        public bool StartupTransportCallEvidenceAvailable { get; set; }
        public PlaybackQualityTransportCallSnapshot FfmpegOpenInputTransportCalls { get; set; } = new PlaybackQualityTransportCallSnapshot();
        public PlaybackQualityTransportCallSnapshot FfmpegStreamInfoTransportCalls { get; set; } = new PlaybackQualityTransportCallSnapshot();
        public PlaybackQualityTransportCallSnapshot NativeStartupSeekTransportCalls { get; set; } = new PlaybackQualityTransportCallSnapshot();
        public PlaybackQualityTransportCallSnapshot NativeFirstFrameTransportCalls { get; set; } = new PlaybackQualityTransportCallSnapshot();
        public double NativeFirstFrameDurationMs { get; set; }
        public double NativeFirstFrameDemuxReadDurationMs { get; set; }
        public double NativeFirstFramePresentDurationMs { get; set; }
        public ulong NativeFirstFrameDemuxPacketCount { get; set; }
        public ulong NativeFirstFrameDemuxBytes { get; set; }
        public double PlaybackDemuxReadDurationMs { get; set; }
        public ulong PlaybackDemuxPacketCount { get; set; }
        public ulong PlaybackDemuxBytes { get; set; }
        public PlaybackQualityTransportCallSnapshot PlaybackTransportCalls { get; set; } = new PlaybackQualityTransportCallSnapshot();
        public ulong ReadErrorCount { get; set; }
        public ulong ReadRetryCount { get; set; }
        public ulong ReadRecoveryCount { get; set; }
        public uint MaxConsecutiveReadErrors { get; set; }
        public int LastReadErrorCode { get; set; }
        public int FatalReadErrorCode { get; set; }
        public double LastReadRecoveryDurationMs { get; set; }
        public long? ContainerStartTimeTicks { get; set; }
        public long? VideoStreamStartTimeTicks { get; set; }
        public long? SeekDemuxTargetTicks { get; set; }
        public long? FirstPresentedPositionTicks { get; set; }
        public bool SeekPacketCacheEnabled { get; set; }
        public bool SeekPacketCacheHit { get; set; }
        public ulong SeekPacketCachePacketCount { get; set; }
        public ulong SeekPacketCacheBytes { get; set; }
        public long SeekPacketCacheWindowDurationTicks { get; set; }
        public string SeekFallbackReason { get; set; } = "";
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
        public double VideoDecodeDurationMsP50 { get; set; }
        public double VideoDecodeDurationMsP95 { get; set; }
        public double VideoDecodeDurationMsP99 { get; set; }
        public double VideoDecodeDurationMsMax { get; set; }
        public string VideoDecodeDeviceMode { get; set; } = "unknown";
        public string VideoDecodeSynchronizationMode { get; set; } = "none";
        public bool VideoDecodeWorkerActive { get; set; }
        public ulong VideoDecodeQueueCapacity { get; set; }
        public ulong VideoDecodeQueueMaxDepth { get; set; }
        public ulong VideoDecodeQueueProducerWaitCount { get; set; }
        public ulong VideoDecoderSendPacketEagainCount { get; set; }
        public ulong VideoDecoderDoubleEagainRetryCount { get; set; }
        public ulong VideoDecoderDoubleEagainRecoveryCount { get; set; }
        public ulong VideoDecoderDoubleEagainExhaustedCount { get; set; }
        public double VideoDecodePacketReadDurationMsP50 { get; set; }
        public double VideoDecodePacketReadDurationMsP95 { get; set; }
        public double VideoDecodeSendPacketDurationMsP50 { get; set; }
        public double VideoDecodeSendPacketDurationMsP95 { get; set; }
        public double VideoDecodeReceiveFrameDurationMsP50 { get; set; }
        public double VideoDecodeReceiveFrameDurationMsP95 { get; set; }
        public double VideoDecodeFrameMaterializeDurationMsP50 { get; set; }
        public double VideoDecodeFrameMaterializeDurationMsP95 { get; set; }
        public double VideoRenderDurationMsP50 { get; set; }
        public double VideoRenderDurationMsP95 { get; set; }
        public double VideoRenderDurationMsP99 { get; set; }
        public double VideoRenderDurationMsMax { get; set; }
        public ulong VideoRenderDirectCopyFrameCount { get; set; }
        public ulong VideoRenderVideoProcessorFrameCount { get; set; }
        public ulong VideoRenderBgraFrameCount { get; set; }
        public ulong VideoRenderPostProcessFrameCount { get; set; }
        public ulong VideoProcessorSetupCpuSampleCount { get; set; }
        public double VideoProcessorSetupCpuDurationMsP50 { get; set; }
        public double VideoProcessorSetupCpuDurationMsP95 { get; set; }
        public double VideoProcessorSetupCpuDurationMsP99 { get; set; }
        public double VideoProcessorSetupCpuDurationMsMax { get; set; }
        public ulong VideoProcessorViewTargetCpuSampleCount { get; set; }
        public double VideoProcessorViewTargetCpuDurationMsP50 { get; set; }
        public double VideoProcessorViewTargetCpuDurationMsP95 { get; set; }
        public double VideoProcessorViewTargetCpuDurationMsP99 { get; set; }
        public double VideoProcessorViewTargetCpuDurationMsMax { get; set; }
        public ulong VideoProcessorClearCpuSampleCount { get; set; }
        public double VideoProcessorClearCpuDurationMsP50 { get; set; }
        public double VideoProcessorClearCpuDurationMsP95 { get; set; }
        public double VideoProcessorClearCpuDurationMsP99 { get; set; }
        public double VideoProcessorClearCpuDurationMsMax { get; set; }
        public ulong VideoProcessorBltCpuSampleCount { get; set; }
        public double VideoProcessorBltCpuDurationMsP50 { get; set; }
        public double VideoProcessorBltCpuDurationMsP95 { get; set; }
        public double VideoProcessorBltCpuDurationMsP99 { get; set; }
        public double VideoProcessorBltCpuDurationMsMax { get; set; }
        public ulong VideoProcessorPostProcessCpuSampleCount { get; set; }
        public double VideoProcessorPostProcessCpuDurationMsP50 { get; set; }
        public double VideoProcessorPostProcessCpuDurationMsP95 { get; set; }
        public double VideoProcessorPostProcessCpuDurationMsP99 { get; set; }
        public double VideoProcessorPostProcessCpuDurationMsMax { get; set; }
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
