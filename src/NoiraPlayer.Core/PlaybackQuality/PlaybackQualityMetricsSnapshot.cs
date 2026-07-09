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
        public double RenderIntervalMsP50 { get; set; }
        public double RenderIntervalMsP95 { get; set; }
        public double RenderIntervalMsP99 { get; set; }
        public double MaxFrameGapMs { get; set; }
        public ulong RenderIntervalSampleCount { get; set; }
        public ulong RenderIntervalOverExpected2MsCount { get; set; }
        public ulong RenderIntervalOverExpected4MsCount { get; set; }
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
        public double AudioAheadWaitOversleepMsP50 { get; set; }
        public double AudioAheadWaitOversleepMsP95 { get; set; }
        public double AudioAheadWaitOversleepMsP99 { get; set; }
        public double AudioAheadWaitOversleepMsMax { get; set; }
        public double FramePacingSourceFrameRate { get; set; }
        public double LateFrameDropToleranceMs { get; set; }
        public double AudioVideoDriftMsP50 { get; set; }
        public double AudioVideoDriftMsP95 { get; set; }
        public double AudioVideoDriftMsP99 { get; set; }
        public double AudioVideoDriftMsMax { get; set; }
    }
}
