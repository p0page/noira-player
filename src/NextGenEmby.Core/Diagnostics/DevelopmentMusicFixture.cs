using System;
using System.Collections.Generic;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Diagnostics
{
    public static class DevelopmentMusicFixture
    {
        private const string ArtworkTag = "qa";
        private const long MinuteTicks = TimeSpan.TicksPerMinute;

        public static DevelopmentMusicFixtureSnapshot Create()
        {
            var artworkUris = new Dictionary<string, string>(StringComparer.Ordinal);
            var albums = new[]
            {
                Album(artworkUris, "qa-album-nocturne", "Nocturne Signals", 2026, 10, "qa-poster-11.png"),
                Album(artworkUris, "qa-album-city", "City Lights Archive", 2025, 12, "qa-poster-12.png"),
                Album(artworkUris, "qa-album-lobby", "Neon Lobby Themes", 2024, 8, "qa-poster-13.png")
            };
            var songs = new[]
            {
                Song(artworkUris, "qa-song-opening", "Opening Credits", albums[0], 1, 0, 3, "qa-poster-11.png"),
                Song(artworkUris, "qa-song-glass", "Glass Elevator", albums[0], 2, 0, 4, "qa-poster-11.png"),
                Song(artworkUris, "qa-song-static", "Soft Static", albums[0], 3, 0, 2, "qa-poster-11.png"),
                Song(artworkUris, "qa-song-late", "Late Train Window", albums[1], 1, 0, 5, "qa-poster-12.png"),
                Song(artworkUris, "qa-song-rooftop", "Rooftop Weather", albums[1], 2, 0, 4, "qa-poster-12.png"),
                Song(artworkUris, "qa-song-lobby", "Lobby Theme", albums[2], 1, 0, 3, "qa-poster-13.png")
            };

            return new DevelopmentMusicFixtureSnapshot(albums, songs, artworkUris);
        }

        public static string ArtworkKey(string itemId, string imageType)
        {
            return (itemId ?? "") + "|" + (imageType ?? "");
        }

        private static EmbyMediaItem Album(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            int year,
            int trackCount,
            string primaryAsset)
        {
            AddArtwork(artworkUris, id, "Primary", primaryAsset);

            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = "MusicAlbum",
                Overview = "A browse-only album fixture used to validate couch navigation without a live music library.",
                ProductionYear = year,
                ChildCount = trackCount,
                PrimaryImageTag = ArtworkTag,
                PrimaryImageItemId = id,
                UserData = new EmbyUserData()
            };
        }

        private static EmbyMediaItem Song(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            EmbyMediaItem album,
            int indexNumber,
            int runtimeHours,
            int runtimeMinutes,
            string primaryAsset)
        {
            AddArtwork(artworkUris, id, "Primary", primaryAsset);

            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = "Audio",
                Overview = "Audio playback remains browse-only in this build; this item validates song focus, preview, and the unsupported layer.",
                ParentId = album.Id,
                ProductionYear = album.ProductionYear,
                ParentIndexNumber = 1,
                IndexNumber = indexNumber,
                RunTimeTicks = runtimeHours * TimeSpan.TicksPerHour + runtimeMinutes * MinuteTicks + 22 * TimeSpan.TicksPerSecond,
                PrimaryImageTag = ArtworkTag,
                PrimaryImageItemId = id,
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
