using System;
using System.Collections.Generic;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Diagnostics
{
    public static class DevelopmentLibraryOrganizationFixture
    {
        private const string ArtworkTag = "qa";
        private const long MinuteTicks = TimeSpan.TicksPerMinute;

        public static DevelopmentLibraryOrganizationFixtureSnapshot Create()
        {
            var artworkUris = new Dictionary<string, string>(StringComparer.Ordinal);
            var items = new[]
            {
                Container(artworkUris, "fixture-collection-signal", "Signal Archives", "BoxSet", 4, "qa-wide-06.png"),
                Container(artworkUris, "fixture-collection-city", "City Nights", "BoxSet", 3, "qa-wide-07.png"),
                Container(artworkUris, "fixture-playlist-weekend", "Weekend Queue", "Playlist", 5, "qa-wide-08.png"),
                Container(artworkUris, "fixture-playlist-documentary", "Documentary Stack", "Playlist", 3, "qa-wide-12.png"),
                MediaItem(artworkUris, "fixture-org-aurora", "Aurora Protocol", "Movie", "fixture-collection-signal", 2026, 118, "qa-poster-01.png", "qa-wide-01.png"),
                MediaItem(artworkUris, "fixture-org-midnight", "Midnight Signal", "Movie", "fixture-collection-signal", 2025, 104, "qa-poster-02.png", "qa-wide-02.png"),
                MediaItem(artworkUris, "fixture-org-afterimage", "Afterimage", "Movie", "fixture-collection-signal", 2026, 111, "qa-poster-04.png", "qa-wide-04.png"),
                MediaItem(artworkUris, "fixture-org-orbit", "Quiet Orbit", "Movie", "fixture-collection-signal", 2023, 126, "qa-poster-05.png", "qa-wide-05.png"),
                MediaItem(artworkUris, "fixture-org-harbor", "Harbor Run", "Movie", "fixture-collection-city", 2024, 96, "qa-poster-03.png", "qa-wide-03.png"),
                MediaItem(artworkUris, "fixture-org-summit", "Summit Line", "Movie", "fixture-collection-city", 2022, 92, "qa-poster-06.png", "qa-wide-06.png"),
                MediaItem(artworkUris, "fixture-org-city-night", "City at Night", "Movie", "fixture-collection-city", 2023, 73, "qa-poster-13.png", "qa-wide-13.png"),
                MediaItem(artworkUris, "fixture-org-northline-e4", "Northline S1:E4", "Episode", "fixture-playlist-weekend", 2026, 49, "qa-poster-10.png", "qa-wide-10.png", resumeMinutes: 12),
                MediaItem(artworkUris, "fixture-org-roomtone-e1", "Room Tone S2:E1", "Episode", "fixture-playlist-weekend", 2025, 51, "qa-poster-11.png", "qa-wide-11.png"),
                MediaItem(artworkUris, "fixture-org-ocean", "Ocean Archive", "Movie", "fixture-playlist-weekend", 2024, 88, "qa-poster-12.png", "qa-wide-12.png"),
                MediaItem(artworkUris, "fixture-org-sound", "Sound Room", "Movie", "fixture-playlist-weekend", 2022, 81, "qa-poster-14.png", "qa-wide-14.png"),
                MediaItem(artworkUris, "fixture-org-roomtone", "Room Tone", "Series", "fixture-playlist-weekend", 2025, 52, "qa-poster-08.png", "qa-wide-08.png"),
                MediaItem(artworkUris, "fixture-org-doc-ocean", "Ocean Archive", "Movie", "fixture-playlist-documentary", 2024, 88, "qa-poster-12.png", "qa-wide-12.png"),
                MediaItem(artworkUris, "fixture-org-doc-city", "City at Night", "Movie", "fixture-playlist-documentary", 2023, 73, "qa-poster-13.png", "qa-wide-13.png"),
                MediaItem(artworkUris, "fixture-org-doc-sound", "Sound Room", "Movie", "fixture-playlist-documentary", 2022, 81, "qa-poster-14.png", "qa-wide-14.png")
            };

            return new DevelopmentLibraryOrganizationFixtureSnapshot(items, artworkUris);
        }

        public static string ArtworkKey(string itemId, string imageType)
        {
            return (itemId ?? "") + "|" + (imageType ?? "");
        }

        private static EmbyMediaItem Container(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            string type,
            int childCount,
            string thumbAsset)
        {
            AddArtwork(artworkUris, id, "Thumb", thumbAsset);

            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = type,
                ParentId = "",
                ChildCount = childCount,
                ThumbImageTag = ArtworkTag,
                ThumbImageItemId = id,
                UserData = new EmbyUserData()
            };
        }

        private static EmbyMediaItem MediaItem(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            string type,
            string parentId,
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
                ParentId = parentId ?? "",
                ProductionYear = year,
                RunTimeTicks = runtimeMinutes * MinuteTicks,
                PrimaryImageTag = ArtworkTag,
                PrimaryImageItemId = id,
                BackdropImageTag = ArtworkTag,
                BackdropImageItemId = id,
                ThumbImageTag = ArtworkTag,
                ThumbImageItemId = id,
                GenreItems = new[]
                {
                    Reference("fixture-genre-sci-fi", "Sci-Fi")
                },
                StudioItems = new[]
                {
                    Reference("fixture-studio-terminus", "Terminus Pictures")
                },
                TagItems = new[]
                {
                    Reference("", "Douban Top")
                },
                UserData = new EmbyUserData
                {
                    PlaybackPositionTicks = Math.Max(0, resumeMinutes) * MinuteTicks,
                    PlayedPercentage = resumeMinutes <= 0 || runtimeMinutes <= 0
                        ? null
                        : (double)resumeMinutes / runtimeMinutes * 100d
                }
            };
        }

        private static EmbyItemReference Reference(string id, string name)
        {
            return new EmbyItemReference
            {
                Id = id ?? "",
                Name = name ?? ""
            };
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
