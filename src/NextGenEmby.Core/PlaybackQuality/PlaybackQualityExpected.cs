namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityExpected
    {
        public double FrameRate { get; set; }
        public string HdrOutput { get; set; } = "";
        public string DxgiInput { get; set; } = "";
        public string DxgiOutput { get; set; } = "";
        public long? MaxDroppedFrames { get; set; }
        public double? MaxFrameGapMs { get; set; }
        public double? MaxAudioVideoDriftMsP95 { get; set; }
        public long? MaxVideoStarvedPasses { get; set; }
        public long? MaxAudioStarvedPasses { get; set; }
        public bool RequireValidatedConversion { get; set; } = true;
        public bool RequireMatchedDisplayRefreshRate { get; set; }
    }
}
