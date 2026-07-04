namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyMediaStream
    {
        public int Index { get; set; }
        public EmbyStreamKind Kind { get; set; }
        public string Codec { get; set; } = "";
        public string Language { get; set; } = "";
        public string ChannelLayout { get; set; } = "";
        public string DisplayTitle { get; set; } = "";
        public bool IsExternal { get; set; }
    }
}
