namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityExpected
    {
        public string Codec { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public double FrameRate { get; set; }
        public string HdrKind { get; set; } = "";
        public string VideoRange { get; set; } = "";
        public string ColorPrimaries { get; set; } = "";
        public string ColorTransfer { get; set; } = "";
        public string ColorSpace { get; set; } = "";
        public string HdrPlaybackStrategy { get; set; } = "";
        public bool? IsHdr { get; set; }
        public bool? IsDirectPlayable { get; set; }
        public bool? IsDolbyVision { get; set; }
        public int? DolbyVisionProfile { get; set; }
        public int? DolbyVisionCompatibilityId { get; set; }
        public bool? HasHdr10BaseLayer { get; set; }
        public bool? HasHlgBaseLayer { get; set; }
        public string HdrOutput { get; set; } = "";
        public string DxgiInput { get; set; } = "";
        public string DxgiOutput { get; set; } = "";
        public double? MaxStartupDurationMs { get; set; }
        public double? MaxInteractionRecoveryDurationMs { get; set; }
        public long? MinRenderedVideoFrames { get; set; }
        public long? MaxDroppedFrames { get; set; }
        public double? MaxFrameGapMs { get; set; }
        public double? MaxRenderIntervalMsP95 { get; set; }
        public double? MaxRenderIntervalMsP99 { get; set; }
        public double? MaxAudioVideoDriftMsP95 { get; set; }
        public double? MaxSeekPositionErrorMs { get; set; }
        public double? MaxSeekRecoveryDurationMs { get; set; }
        public long? MaxVideoStarvedPasses { get; set; }
        public long? MaxAudioStarvedPasses { get; set; }
        public bool RequireValidatedConversion { get; set; } = true;
        public bool RequireMatchedDisplayRefreshRate { get; set; }
    }
}
