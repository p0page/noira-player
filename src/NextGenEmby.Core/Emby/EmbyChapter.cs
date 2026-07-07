namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyChapter
    {
        public string Name { get; set; } = "";
        public long StartPositionTicks { get; set; }
        public string ImageTag { get; set; } = "";
    }
}
