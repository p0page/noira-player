namespace NoiraPlayer.Core.Emby
{
    public sealed class EmbyItemsQuery
    {
        public string ParentId { get; set; } = "";
        public string IncludeItemTypes { get; set; } = "";
        public string CollectionTypes { get; set; } = "";
        public string MediaTypes { get; set; } = "";
        public string SearchTerm { get; set; } = "";
        public string SortBy { get; set; } = "SortName";
        public string SortOrder { get; set; } = "Ascending";
        public string Filters { get; set; } = "";
        public string GenreIds { get; set; } = "";
        public string Genres { get; set; } = "";
        public string PersonIds { get; set; } = "";
        public string StudioIds { get; set; } = "";
        public string Studios { get; set; } = "";
        public string Tags { get; set; } = "";
        public string ArtistIds { get; set; } = "";
        public string AlbumArtistIds { get; set; } = "";
        public string Ids { get; set; } = "";
        public int StartIndex { get; set; }
        public int Limit { get; set; } = 50;
        public bool Recursive { get; set; } = true;
        public bool? IsFavorite { get; set; }
        public bool? IsPlayed { get; set; }
        public bool? IsFolder { get; set; }
    }
}
