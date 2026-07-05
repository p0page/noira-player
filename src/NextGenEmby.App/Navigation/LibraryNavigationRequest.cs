namespace NextGenEmby.App.Navigation
{
    public sealed class LibraryNavigationRequest
    {
        public LibraryNavigationRequest(string title, string collectionType, string includeItemTypes)
        {
            Title = title ?? "";
            CollectionType = collectionType ?? "";
            IncludeItemTypes = includeItemTypes ?? "";
        }

        public string Title { get; }

        public string CollectionType { get; }

        public string IncludeItemTypes { get; }

        public bool IsMovies => CollectionType == "movies";

        public bool IsTv => CollectionType == "tvshows";
    }
}
