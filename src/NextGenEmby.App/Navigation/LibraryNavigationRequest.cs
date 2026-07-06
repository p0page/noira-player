using System;
using System.Collections.Generic;
using NextGenEmby.Core.Emby;

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
            : this(title, collectionType, includeItemTypes, parentId, sectionId, LibraryNavigationQuery.Empty)
        {
        }

        public LibraryNavigationRequest(
            string title,
            string collectionType,
            string includeItemTypes,
            string parentId,
            string sectionId,
            LibraryNavigationQuery query,
            IReadOnlyList<EmbyMediaItem>? developmentItems = null,
            IReadOnlyDictionary<string, string>? developmentArtworkUris = null,
            string containerItemType = "")
        {
            Title = title ?? "";
            CollectionType = collectionType ?? "";
            IncludeItemTypes = includeItemTypes ?? "";
            ParentId = parentId ?? "";
            SectionId = sectionId ?? "";
            ContainerItemType = containerItemType ?? "";
            Query = query ?? LibraryNavigationQuery.Empty;
            DevelopmentItems = developmentItems ?? Array.Empty<EmbyMediaItem>();
            DevelopmentArtworkUris = developmentArtworkUris ?? new Dictionary<string, string>();
        }

        public string Title { get; }

        public string CollectionType { get; }

        public string IncludeItemTypes { get; }

        public string ParentId { get; }

        public string SectionId { get; }

        public string ContainerItemType { get; }

        public LibraryNavigationQuery Query { get; }

        public IReadOnlyList<EmbyMediaItem> DevelopmentItems { get; }

        public IReadOnlyDictionary<string, string> DevelopmentArtworkUris { get; }

        public string RestoreFocusItemId { get; set; } = "";

        public bool IsMovies => CollectionType == "movies";

        public bool IsTv => CollectionType == "tvshows";

        public LibraryNavigationRequest WithDevelopmentFixture(
            IReadOnlyList<EmbyMediaItem> items,
            IReadOnlyDictionary<string, string> artworkUris)
        {
            return new LibraryNavigationRequest(
                Title,
                CollectionType,
                IncludeItemTypes,
                ParentId,
                SectionId,
                Query,
                items,
                artworkUris,
                ContainerItemType);
        }
    }

    public sealed class LibraryNavigationQuery
    {
        public static LibraryNavigationQuery Empty { get; } = new LibraryNavigationQuery();

        public LibraryNavigationQuery(
            string collectionTypes = "",
            string mediaTypes = "",
            string filters = "",
            string genreIds = "",
            string genres = "",
            string personIds = "",
            string studioIds = "",
            string studios = "",
            string tags = "",
            string artistIds = "",
            string albumArtistIds = "",
            string ids = "",
            bool? isFavorite = null,
            bool? isPlayed = null,
            bool? isFolder = null,
            bool requireItemTypeMatch = false)
        {
            CollectionTypes = collectionTypes ?? "";
            MediaTypes = mediaTypes ?? "";
            Filters = filters ?? "";
            GenreIds = genreIds ?? "";
            Genres = genres ?? "";
            PersonIds = personIds ?? "";
            StudioIds = studioIds ?? "";
            Studios = studios ?? "";
            Tags = tags ?? "";
            ArtistIds = artistIds ?? "";
            AlbumArtistIds = albumArtistIds ?? "";
            Ids = ids ?? "";
            IsFavorite = isFavorite;
            IsPlayed = isPlayed;
            IsFolder = isFolder;
            RequireItemTypeMatch = requireItemTypeMatch;
        }

        public string CollectionTypes { get; }

        public string MediaTypes { get; }

        public string Filters { get; }

        public string GenreIds { get; }

        public string Genres { get; }

        public string PersonIds { get; }

        public string StudioIds { get; }

        public string Studios { get; }

        public string Tags { get; }

        public string ArtistIds { get; }

        public string AlbumArtistIds { get; }

        public string Ids { get; }

        public bool? IsFavorite { get; }

        public bool? IsPlayed { get; }

        public bool? IsFolder { get; }

        public bool RequireItemTypeMatch { get; }
    }
}
