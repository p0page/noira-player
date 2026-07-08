namespace NoiraPlayer.Core.Emby
{
    public sealed class EmbyMediaStream
    {
        public int Index { get; set; }
        public EmbyStreamKind Kind { get; set; }
        public string Codec { get; set; } = "";
        public string Language { get; set; } = "";
        public string ChannelLayout { get; set; } = "";
        public int Channels { get; set; }
        public string DisplayTitle { get; set; } = "";
        public string VideoRange { get; set; } = "";
        public string ColorPrimaries { get; set; } = "";
        public string ColorTransfer { get; set; } = "";
        public string ColorSpace { get; set; } = "";
        public bool IsExternal { get; set; }
        public bool? IsDefault { get; set; }
        public bool? IsForced { get; set; }
        public double RealFrameRate { get; set; }
        public double AverageFrameRate { get; set; }
    }
}
