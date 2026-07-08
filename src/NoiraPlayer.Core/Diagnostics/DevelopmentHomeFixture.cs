using System;
using System.Collections.Generic;
using System.Linq;
using NoiraPlayer.Core.Emby;

namespace NoiraPlayer.Core.Diagnostics
{
    public static class DevelopmentHomeFixture
    {
        private const string ArtworkTag = "";
        private const long HourTicks = TimeSpan.TicksPerHour;
        private const long MinuteTicks = TimeSpan.TicksPerMinute;

        public static DevelopmentHomeFixtureSnapshot Create()
        {
            var artworkUris = new Dictionary<string, string>(StringComparer.Ordinal);

            var movies = new[]
            {
                MediaItem(artworkUris, "qa-movie-aurora", "Aurora Protocol", "Movie", 2026, 118, "", "", resumeMinutes: 42),
                MediaItem(artworkUris, "qa-movie-midnight", "Midnight Signal", "Movie", 2025, 104, "", ""),
                MediaItem(artworkUris, "qa-movie-harbor", "Harbor Run", "Movie", 2024, 96, "", ""),
                MediaItem(artworkUris, "qa-movie-afterimage", "Afterimage", "Movie", 2026, 111, "", ""),
                MediaItem(artworkUris, "qa-movie-orbit", "Quiet Orbit", "Movie", 2023, 126, "", ""),
                MediaItem(artworkUris, "qa-movie-summit", "Summit Line", "Movie", 2022, 92, "", "")
            };
            var noArtworkMovie = MediaItemWithoutArtwork("qa-movie-no-artwork", "No Poster Signal", "Movie", 2021, 107);
            var moviePreviewExtras = new[]
            {
                MediaItem(artworkUris, "qa-movie-meridian", "Glass Meridian", "Movie", 2024, 101, "", ""),
                MediaItem(artworkUris, "qa-movie-last-train", "Last Train North", "Movie", 2023, 116, "", ""),
                MediaItem(artworkUris, "qa-movie-paper-sun", "The Paper Sun", "Movie", 2022, 94, "", ""),
                MediaItem(artworkUris, "qa-movie-orchard", "Signal Orchard", "Movie", 2025, 109, "", ""),
                MediaItem(artworkUris, "qa-movie-low-tide", "Low Tide Static", "Movie", 2021, 99, "", ""),
                MediaItem(artworkUris, "qa-movie-field", "Field Recordings", "Movie", 2020, 83, "", ""),
                MediaItem(artworkUris, "qa-movie-night-transfer", "Night Transfer", "Movie", 2023, 103, "", ""),
                MediaItem(artworkUris, "qa-movie-eastern-relay", "Eastern Relay", "Movie", 2024, 97, "", "")
            };
            var shows = new[]
            {
                MediaItem(artworkUris, "qa-show-northline", "Northline", "Series", 2026, 48, "", ""),
                MediaItem(artworkUris, "qa-show-roomtone", "Room Tone", "Series", 2025, 52, "", ""),
                MediaItem(artworkUris, "qa-show-horizon", "Horizon House", "Series", 2024, 45, "", ""),
                MediaItem(artworkUris, "qa-episode-signal", "Northline S1:E4", "Episode", 2026, 49, "", "", resumeMinutes: 12),
                MediaItem(artworkUris, "qa-episode-roomtone", "Room Tone S2:E1", "Episode", 2025, 51, "", "")
            };
            var documentaries = new[]
            {
                MediaItem(artworkUris, "qa-doc-ocean", "Ocean Archive", "Movie", 2024, 88, "", ""),
                MediaItem(artworkUris, "qa-doc-city", "City at Night", "Movie", 2023, 73, "", ""),
                MediaItem(artworkUris, "qa-doc-sound", "Sound Room", "Movie", 2022, 81, "", "")
            };

            var allLatest = movies.Concat(shows).Concat(documentaries).Take(14).ToList();
            var libraryViews = new[]
            {
                LibraryView(artworkUris, "qa-library-movies", "Hot Movies", "movies", ""),
                LibraryView(artworkUris, "qa-library-tv", "Hot TV Series", "tvshows", ""),
                LibraryView(artworkUris, "qa-library-douban", "Douban Top Rated", "movies", ""),
                LibraryView(artworkUris, "qa-library-netflix", "Netflix", "tvshows", ""),
                LibraryView(artworkUris, "qa-library-anime", "Anime", "tvshows", ""),
                LibraryView(artworkUris, "qa-library-docs", "Documentaries", "movies", "")
            };

            var libraryPreviews = new Dictionary<string, IReadOnlyList<EmbyMediaItem>>(StringComparer.Ordinal)
            {
                ["qa-library-movies"] = movies.Take(1).Concat(new[] { noArtworkMovie }).Concat(movies.Skip(1)).Concat(moviePreviewExtras).ToList(),
                ["qa-library-tv"] = shows.Take(5).ToList(),
                ["qa-library-douban"] = movies.Skip(1).Take(5).ToList(),
                ["qa-library-netflix"] = shows.Skip(1).Take(4).ToList(),
                ["qa-library-anime"] = shows.Reverse().Take(4).ToList(),
                ["qa-library-docs"] = documentaries.Take(3).ToList()
            };

            var configuredRows = new List<DevelopmentHomeMediaRow>
            {
                Row(artworkUris, "Hot Movies", "movies", "", "qa-section-hot-movies", "qa-section-hot-movies-parent", "", movies),
                Row(artworkUris, "Tonight Picks", "movies", "", "qa-section-tonight", "qa-section-tonight-parent", "", documentaries.Take(2).ToList()),
                Row(artworkUris, "Hot TV Series", "tvshows", "", "qa-section-hot-tv", "qa-section-hot-tv-parent", "", shows),
                Row(artworkUris, "Douban Top Rated", "movies", "", "qa-section-douban", "qa-section-douban-parent", "", movies.Skip(1).Concat(documentaries).Take(7).ToList()),
                Row(artworkUris, "Netflix", "tvshows", "", "qa-section-netflix", "qa-section-netflix-parent", "", shows.Skip(1).Take(4).ToList())
            };

            var popularRows = new List<DevelopmentHomeMediaRow>
            {
                Row(artworkUris, "Popular in Hot Movies", "movies", "qa-library-movies", "", "qa-popular-movies-parent", "", movies.Skip(1).Take(5).ToList()),
                Row(artworkUris, "Popular in Hot TV Series", "tvshows", "qa-library-tv", "", "qa-popular-tv-parent", "", shows.Take(5).ToList())
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

        private static EmbyMediaItem MediaItemWithoutArtwork(
            string id,
            string name,
            string type,
            int year,
            int runtimeMinutes)
        {
            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = type,
                ProductionYear = year,
                RunTimeTicks = runtimeMinutes * MinuteTicks,
                UserData = new EmbyUserData()
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
            if (!string.IsNullOrWhiteSpace(sectionId))
            {
                AddArtwork(artworkUris, sectionId, "Thumb", parentThumbAsset);
            }

            var parentItem = new EmbyMediaItem
            {
                Id = parentItemId,
                Name = title,
                Type = "Folder",
                ThumbImageTag = ArtworkTag,
                ThumbImageItemId = parentItemId
            };
            var section = new EmbyHomeSection
            {
                Id = sectionId,
                Name = title,
                CollectionType = collectionType,
                ThumbImageTag = string.IsNullOrWhiteSpace(sectionId) ? "" : ArtworkTag,
                ThumbImageItemId = string.IsNullOrWhiteSpace(sectionId) ? "" : sectionId,
                ParentItem = parentItem
            };

            return new DevelopmentHomeMediaRow(
                title,
                collectionType,
                parentId,
                sectionId,
                section,
                items);
        }

        private static void AddArtwork(
            IDictionary<string, string> artworkUris,
            string itemId,
            string imageType,
            string assetName)
        {
            _ = artworkUris;
            _ = itemId;
            _ = imageType;
            _ = assetName;
        }
    }
}
