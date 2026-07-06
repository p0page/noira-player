namespace NextGenEmby.App.Navigation
{
    public sealed class LibraryNavigationRequest
    {
        public LibraryNavigationRequest(string title, string collectionType, string includeItemTypes)
            : this(title, collectionType, includeItemTypes, "", "")
        {
        }

        public LibraryNavigationRequest(
            string title,
            string collectionType,
            string includeItemTypes,
            string parentId,
            string sectionId)
        {
            Title = title ?? "";
            CollectionType = collectionType ?? "";
            IncludeItemTypes = includeItemTypes ?? "";
            ParentId = parentId ?? "";
            SectionId = sectionId ?? "";
        }

        public string Title { get; }

        public string CollectionType { get; }

        public string IncludeItemTypes { get; }

        public string ParentId { get; }

        public string SectionId { get; }

        public bool IsMovies => CollectionType == "movies";

        public bool IsTv => CollectionType == "tvshows";
    }
}
