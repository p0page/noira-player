namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyLibraryView
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string CollectionType { get; set; } = "";

        public bool IsMovieLibrary => CollectionType == "movies";
        public bool IsTvLibrary => CollectionType == "tvshows";
    }
}
