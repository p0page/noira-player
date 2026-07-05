namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyMediaItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Overview { get; set; } = "";
        public int? ProductionYear { get; set; }
        public long? RunTimeTicks { get; set; }
        public string PrimaryImageTag { get; set; } = "";
        public string BackdropImageTag { get; set; } = "";
        public string ParentId { get; set; } = "";
        public string SeriesId { get; set; } = "";
        public int? IndexNumber { get; set; }
        public int? ParentIndexNumber { get; set; }
        public int? ChildCount { get; set; }
        public EmbyUserData UserData { get; set; } = new EmbyUserData();
    }
}
