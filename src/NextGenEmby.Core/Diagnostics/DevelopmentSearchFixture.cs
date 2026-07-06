using System;
using System.Collections.Generic;
using System.Linq;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Diagnostics
{
    public static class DevelopmentSearchFixture
    {
        private static readonly IReadOnlyList<EmbyMediaItem> Items = new[]
        {
            CreateItem("fixture-movie-aurora", "Aurora Protocol", "Movie", 2026, 24000000000),
            CreateItem("fixture-series-polar", "Polar Archive", "Series", 2025, 0, childCount: 18),
            CreateItem("fixture-episode-signal", "Signal Room", "Episode", 2026, 27000000000, parentIndexNumber: 1, indexNumber: 3),
            CreateItem("fixture-video-gallery", "Behind the Glass", "Video", 2024, 9000000000),
            CreateItem("fixture-music-video", "Neon Skyline Session", "MusicVideo", 2025, 12000000000),
            CreateItem("fixture-collection-night", "Night City Collection", "BoxSet", 2026, 0, childCount: 7),
            CreateItem("fixture-playlist-weekend", "Weekend Queue", "Playlist", 2026, 0, childCount: 12),
            CreateItem("fixture-person-maya", "Maya Chen", "Person", null, 0),
            CreateItem("fixture-album-nocturne", "Nocturne Signals", "MusicAlbum", 2026, 0, childCount: 10),
            CreateItem("fixture-audio-opening", "Opening Credits", "Audio", 2026, 1800000000),
            CreateItem("fixture-photo-lobby", "Neon Lobby Still", "Photo", 2026, 0),
            CreateItem("fixture-channel-news24", "News 24", "TvChannel", null, 0)
        };

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

        private static EmbyMediaItem CreateItem(
            string id,
            string name,
            string type,
            int? year,
            long runtimeTicks,
            int? parentIndexNumber = null,
            int? indexNumber = null,
            int? childCount = null)
        {
            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = type,
                ProductionYear = year,
                RunTimeTicks = runtimeTicks > 0 ? runtimeTicks : (long?)null,
                ParentIndexNumber = parentIndexNumber,
                IndexNumber = indexNumber,
                ChildCount = childCount,
                UserData = new EmbyUserData()
            };
        }
    }
}
