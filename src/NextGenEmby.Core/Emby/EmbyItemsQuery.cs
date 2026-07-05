namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyItemsQuery
    {
        public string ParentId { get; set; } = "";
        public string IncludeItemTypes { get; set; } = "";
        public string SearchTerm { get; set; } = "";
        public string SortBy { get; set; } = "SortName";
        public string SortOrder { get; set; } = "Ascending";
        public string Filters { get; set; } = "";
        public int StartIndex { get; set; }
        public int Limit { get; set; } = 50;
        public bool Recursive { get; set; } = true;
    }
}
