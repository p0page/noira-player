namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityMetricsSnapshot
    {
        public ulong RenderPasses { get; set; }
        public ulong DecodedVideoFrames { get; set; }
        public ulong RenderedVideoFrames { get; set; }
        public ulong SubmittedAudioFrames { get; set; }
        public ulong DroppedVideoFrames { get; set; }
        public ulong SeekPrerollDroppedFrames { get; set; }
        public ulong VideoAheadWaitCount { get; set; }
        public ulong VideoStarvedPasses { get; set; }
        public ulong AudioStarvedPasses { get; set; }
        public ulong QueuedAudioBuffers { get; set; }
        public long AudioClockTicks { get; set; }
        public long VideoPositionTicks { get; set; }
        public double RenderIntervalMsP50 { get; set; }
        public double RenderIntervalMsP95 { get; set; }
        public double RenderIntervalMsP99 { get; set; }
        public double MaxFrameGapMs { get; set; }
        public double FramePacingSourceFrameRate { get; set; }
        public double LateFrameDropToleranceMs { get; set; }
        public double AudioVideoDriftMsP50 { get; set; }
        public double AudioVideoDriftMsP95 { get; set; }
        public double AudioVideoDriftMsP99 { get; set; }
        public double AudioVideoDriftMsMax { get; set; }
    }
}
