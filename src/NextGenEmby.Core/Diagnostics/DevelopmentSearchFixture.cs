using System;
using System.Collections.Generic;
using System.Linq;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Diagnostics
{
    public static class DevelopmentSearchFixture
    {
        private const string ArtworkTag = "qa";
        private static readonly IReadOnlyDictionary<string, string> ArtworkUris;
        private static readonly IReadOnlyList<EmbyMediaItem> Items;

        static DevelopmentSearchFixture()
        {
            var artworkUris = new Dictionary<string, string>(StringComparer.Ordinal);
            Items = new[]
            {
                CreateItem(artworkUris, "fixture-movie-aurora", "Aurora Protocol", "Movie", 2026, 24000000000, "qa-poster-01.png"),
                CreateItemWithoutArtwork("fixture-movie-no-artwork", "No Poster Signal", "Movie", 2021, 64200000000),
                CreateItem(artworkUris, "fixture-series-polar", "Polar Archive", "Series", 2025, 0, "qa-poster-07.png", childCount: 18),
                CreateItem(artworkUris, "fixture-episode-signal", "Signal Room", "Episode", 2026, 27000000000, "qa-poster-10.png", parentIndexNumber: 1, indexNumber: 3),
                CreateItem(artworkUris, "fixture-video-gallery", "Behind the Glass", "Video", 2024, 9000000000, "qa-poster-04.png"),
                CreateItem(artworkUris, "fixture-music-video", "Neon Skyline Session", "MusicVideo", 2025, 12000000000, "qa-poster-05.png"),
                CreateItem(artworkUris, "fixture-collection-night", "Night City Collection", "BoxSet", 2026, 0, "qa-poster-06.png", childCount: 7),
                CreateItem(artworkUris, "fixture-playlist-weekend", "Weekend Queue", "Playlist", 2026, 0, "qa-poster-08.png", childCount: 12),
                CreateItem(artworkUris, "fixture-person-maya", "Maya Chen", "Person", null, 0, "qa-poster-09.png"),
                CreateItem(artworkUris, "fixture-album-nocturne", "Nocturne Signals", "MusicAlbum", 2026, 0, "qa-poster-11.png", childCount: 10),
                CreateItem(artworkUris, "fixture-audio-opening", "Opening Credits", "Audio", 2026, 1800000000, "qa-poster-12.png"),
                CreateItem(artworkUris, "fixture-photo-lobby", "Neon Lobby Still", "Photo", 2026, 0, "qa-poster-13.png"),
                CreateItem(artworkUris, "fixture-channel-news24", "News 24", "TvChannel", null, 0, "qa-poster-14.png")
            };
            ArtworkUris = artworkUris;
        }

        public static IReadOnlyList<EmbyMediaItem> CreateItemsForScope(string? scopeKey)
        {
            var scope = EmbySearchScopePolicy.GetScope(scopeKey);
            if (!scope.RequireItemTypeMatch)
            {
                return Items;
            }

            var allowedTypes = new HashSet<string>(
                scope.IncludeItemTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
            return Items.Where(item => allowedTypes.Contains(item.Type)).ToList();
        }

        public static IReadOnlyDictionary<string, string> CreateArtworkUris()
        {
            return ArtworkUris;
        }

        public static string ArtworkKey(string itemId, string imageType)
        {
            return (itemId ?? "") + "|" + (imageType ?? "");
        }

        private static EmbyMediaItem CreateItem(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            string type,
            int? year,
            long runtimeTicks,
            string primaryAsset,
            int? parentIndexNumber = null,
            int? indexNumber = null,
            int? childCount = null)
        {
            AddArtwork(artworkUris, id, "Primary", primaryAsset);

            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = type,
                ProductionYear = year,
                RunTimeTicks = runtimeTicks > 0 ? runtimeTicks : (long?)null,
                PrimaryImageTag = ArtworkTag,
                PrimaryImageItemId = id,
                ParentIndexNumber = parentIndexNumber,
                IndexNumber = indexNumber,
                ChildCount = childCount,
                UserData = new EmbyUserData()
            };
        }

        private static EmbyMediaItem CreateItemWithoutArtwork(
            string id,
            string name,
            string type,
            int? year,
            long runtimeTicks)
        {
            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = type,
                ProductionYear = year,
                RunTimeTicks = runtimeTicks > 0 ? runtimeTicks : (long?)null,
                UserData = new EmbyUserData()
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
