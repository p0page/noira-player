namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyHomeSection
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string SectionType { get; set; } = "";
        public string CollectionType { get; set; } = "";
        public string ViewType { get; set; } = "";
        public string ScrollDirection { get; set; } = "";
        public EmbyMediaItem ParentItem { get; set; } = new EmbyMediaItem();
    }
}
