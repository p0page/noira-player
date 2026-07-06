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
        public string ThumbImageTag { get; set; } = "";
        public string PrimaryImageTag { get; set; } = "";
        public string BackdropImageTag { get; set; } = "";
        public string BannerImageTag { get; set; } = "";
        public string LogoImageTag { get; set; } = "";
        public string ThumbImageItemId { get; set; } = "";
        public string PrimaryImageItemId { get; set; } = "";
        public string BackdropImageItemId { get; set; } = "";
        public string BannerImageItemId { get; set; } = "";
        public string LogoImageItemId { get; set; } = "";
        public EmbyMediaItem ParentItem { get; set; } = new EmbyMediaItem();
    }
}
