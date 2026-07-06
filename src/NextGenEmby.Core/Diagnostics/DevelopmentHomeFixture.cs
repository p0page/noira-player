using System;
using System.Collections.Generic;
using System.Linq;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Diagnostics
{
    public static class DevelopmentHomeFixture
    {
        private const string ArtworkTag = "qa";
        private const long HourTicks = TimeSpan.TicksPerHour;
        private const long MinuteTicks = TimeSpan.TicksPerMinute;

        public static DevelopmentHomeFixtureSnapshot Create()
        {
            var artworkUris = new Dictionary<string, string>(StringComparer.Ordinal);

            var movies = new[]
            {
                MediaItem(artworkUris, "qa-movie-aurora", "Aurora Protocol", "Movie", 2026, 118, "qa-poster-01.png", "qa-wide-01.png", resumeMinutes: 42),
                MediaItem(artworkUris, "qa-movie-midnight", "Midnight Signal", "Movie", 2025, 104, "qa-poster-02.png", "qa-wide-02.png"),
                MediaItem(artworkUris, "qa-movie-harbor", "Harbor Run", "Movie", 2024, 96, "qa-poster-03.png", "qa-wide-03.png"),
                MediaItem(artworkUris, "qa-movie-afterimage", "Afterimage", "Movie", 2026, 111, "qa-poster-04.png", "qa-wide-04.png"),
                MediaItem(artworkUris, "qa-movie-orbit", "Quiet Orbit", "Movie", 2023, 126, "qa-poster-05.png", "qa-wide-05.png"),
                MediaItem(artworkUris, "qa-movie-summit", "Summit Line", "Movie", 2022, 92, "qa-poster-06.png", "qa-wide-06.png")
            };
            var shows = new[]
            {
                MediaItem(artworkUris, "qa-show-northline", "Northline", "Series", 2026, 48, "qa-poster-07.png", "qa-wide-07.png"),
                MediaItem(artworkUris, "qa-show-roomtone", "Room Tone", "Series", 2025, 52, "qa-poster-08.png", "qa-wide-08.png"),
                MediaItem(artworkUris, "qa-show-horizon", "Horizon House", "Series", 2024, 45, "qa-poster-09.png", "qa-wide-09.png"),
                MediaItem(artworkUris, "qa-episode-signal", "Northline S1:E4", "Episode", 2026, 49, "qa-poster-10.png", "qa-wide-10.png", resumeMinutes: 12),
                MediaItem(artworkUris, "qa-episode-roomtone", "Room Tone S2:E1", "Episode", 2025, 51, "qa-poster-11.png", "qa-wide-11.png")
            };
            var documentaries = new[]
            {
                MediaItem(artworkUris, "qa-doc-ocean", "Ocean Archive", "Movie", 2024, 88, "qa-poster-12.png", "qa-wide-12.png"),
                MediaItem(artworkUris, "qa-doc-city", "City at Night", "Movie", 2023, 73, "qa-poster-13.png", "qa-wide-13.png"),
                MediaItem(artworkUris, "qa-doc-sound", "Sound Room", "Movie", 2022, 81, "qa-poster-14.png", "qa-wide-14.png")
            };

            var allLatest = movies.Concat(shows).Concat(documentaries).Take(14).ToList();
            var libraryViews = new[]
            {
                LibraryView(artworkUris, "qa-library-movies", "Hot Movies", "movies", "qa-wide-01.png"),
                LibraryView(artworkUris, "qa-library-tv", "Hot TV Series", "tvshows", "qa-wide-07.png"),
                LibraryView(artworkUris, "qa-library-douban", "Douban Top Rated", "movies", "qa-wide-03.png"),
                LibraryView(artworkUris, "qa-library-netflix", "Netflix", "tvshows", "qa-wide-08.png"),
                LibraryView(artworkUris, "qa-library-anime", "Anime", "tvshows", "qa-wide-09.png"),
                LibraryView(artworkUris, "qa-library-docs", "Documentaries", "movies", "qa-wide-12.png")
            };

            var libraryPreviews = new Dictionary<string, IReadOnlyList<EmbyMediaItem>>(StringComparer.Ordinal)
            {
                ["qa-library-movies"] = movies.Take(6).ToList(),
                ["qa-library-tv"] = shows.Take(5).ToList(),
                ["qa-library-douban"] = movies.Skip(1).Take(5).ToList(),
                ["qa-library-netflix"] = shows.Skip(1).Take(4).ToList(),
                ["qa-library-anime"] = shows.Reverse().Take(4).ToList(),
                ["qa-library-docs"] = documentaries.Take(3).ToList()
            };

            var configuredRows = new List<DevelopmentHomeMediaRow>
            {
                Row(artworkUris, "Hot Movies", "movies", "", "qa-section-hot-movies", "qa-section-hot-movies-parent", "qa-wide-01.png", movies),
                Row(artworkUris, "Hot TV Series", "tvshows", "", "qa-section-hot-tv", "qa-section-hot-tv-parent", "qa-wide-07.png", shows),
                Row(artworkUris, "Douban Top Rated", "movies", "", "qa-section-douban", "qa-section-douban-parent", "qa-wide-03.png", movies.Skip(1).Concat(documentaries).Take(7).ToList()),
                Row(artworkUris, "Netflix", "tvshows", "", "qa-section-netflix", "qa-section-netflix-parent", "qa-wide-08.png", shows.Skip(1).Take(4).ToList())
            };

            var popularRows = new List<DevelopmentHomeMediaRow>
            {
                Row(artworkUris, "Popular in Hot Movies", "movies", "qa-library-movies", "", "qa-popular-movies-parent", "qa-wide-02.png", movies.Skip(1).Take(5).ToList()),
                Row(artworkUris, "Popular in Hot TV Series", "tvshows", "qa-library-tv", "", "qa-popular-tv-parent", "qa-wide-10.png", shows.Take(5).ToList())
            };

            return new DevelopmentHomeFixtureSnapshot(
                continueItems: new[] { movies[0], shows[3] },
                nextUpItems: new[] { shows[3], shows[4] },
                latestItems: allLatest,
                libraryViews: libraryViews,
                libraryPreviews: libraryPreviews,
                configuredRows: configuredRows,
                popularRows: popularRows,
                artworkUris: artworkUris);
        }

        public static string ArtworkKey(string itemId, string imageType)
        {
            return (itemId ?? "") + "|" + (imageType ?? "");
        }

        private static EmbyMediaItem MediaItem(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            string type,
            int year,
            int runtimeMinutes,
            string posterAsset,
            string backdropAsset,
            int resumeMinutes = 0)
        {
            AddArtwork(artworkUris, id, "Primary", posterAsset);
            AddArtwork(artworkUris, id, "Backdrop", backdropAsset);
            AddArtwork(artworkUris, id, "Thumb", backdropAsset);

            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = type,
                ProductionYear = year,
                RunTimeTicks = runtimeMinutes * MinuteTicks,
                PrimaryImageTag = ArtworkTag,
                PrimaryImageItemId = id,
                BackdropImageTag = ArtworkTag,
                BackdropImageItemId = id,
                ThumbImageTag = ArtworkTag,
                ThumbImageItemId = id,
                UserData = new EmbyUserData
                {
                    PlaybackPositionTicks = Math.Max(0, resumeMinutes) * MinuteTicks,
                    PlayedPercentage = resumeMinutes <= 0 || runtimeMinutes <= 0
                        ? null
                        : (double)resumeMinutes / runtimeMinutes * 100d
                }
            };
        }

        private static EmbyLibraryView LibraryView(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            string collectionType,
            string thumbAsset)
        {
            AddArtwork(artworkUris, id, "Thumb", thumbAsset);

            return new EmbyLibraryView
            {
                Id = id,
                Name = name,
                CollectionType = collectionType,
                ThumbImageTag = ArtworkTag,
                ThumbImageItemId = id
            };
        }

        private static DevelopmentHomeMediaRow Row(
            IDictionary<string, string> artworkUris,
            string title,
            string collectionType,
            string parentId,
            string sectionId,
            string parentItemId,
            string parentThumbAsset,
            IReadOnlyList<EmbyMediaItem> items)
        {
            AddArtwork(artworkUris, parentItemId, "Thumb", parentThumbAsset);
            var parentItem = new EmbyMediaItem
            {
                Id = parentItemId,
                Name = title,
                Type = "Folder",
                ThumbImageTag = ArtworkTag,
                ThumbImageItemId = parentItemId
            };

            return new DevelopmentHomeMediaRow(
                title,
                collectionType,
                parentId,
                sectionId,
                parentItem,
                items);
        }

        private static void AddArtwork(
            IDictionary<string, string> artworkUris,
            string itemId,
            string imageType,
            string assetName)
        {
            artworkUris[ArtworkKey(itemId, imageType)] = "ms-appx:///Assets/QaHome/" + assetName;
        }
    }
}
