using System;
using System.Collections.Generic;
using NoiraPlayer.Core.Emby;

namespace NoiraPlayer.Core.Diagnostics
{
    public static class DevelopmentMusicFixture
    {
        private const string ArtworkTag = "";
        private const long MinuteTicks = TimeSpan.TicksPerMinute;

        public static DevelopmentMusicFixtureSnapshot Create()
        {
            var artworkUris = new Dictionary<string, string>(StringComparer.Ordinal);
            var artists = new[]
            {
                Artist(artworkUris, "qa-artist-kairos", "Kairos Collective", 2026, 10, ""),
                Artist(artworkUris, "qa-artist-mira", "Mira Vale", 2025, 12, ""),
                Artist(artworkUris, "qa-artist-signal", "Signal Room", 2024, 8, "")
            };
            var albums = new[]
            {
                Album(artworkUris, "qa-album-nocturne", "Nocturne Signals", artists[0], 2026, 10, ""),
                Album(artworkUris, "qa-album-city", "City Lights Archive", artists[1], 2025, 12, ""),
                Album(artworkUris, "qa-album-lobby", "Neon Lobby Themes", artists[2], 2024, 8, "")
            };
            var songs = new[]
            {
                Song(artworkUris, "qa-song-opening", "Opening Credits", albums[0], artists[0], 1, 0, 3, ""),
                Song(artworkUris, "qa-song-glass", "Glass Elevator", albums[0], artists[0], 2, 0, 4, ""),
                Song(artworkUris, "qa-song-static", "Soft Static", albums[0], artists[0], 3, 0, 2, ""),
                Song(artworkUris, "qa-song-late", "Late Train Window", albums[1], artists[1], 1, 0, 5, ""),
                Song(artworkUris, "qa-song-rooftop", "Rooftop Weather", albums[1], artists[1], 2, 0, 4, ""),
                Song(artworkUris, "qa-song-lobby", "Lobby Theme", albums[2], artists[2], 1, 0, 3, "")
            };

            return new DevelopmentMusicFixtureSnapshot(artists, albums, songs, artworkUris);
        }

        public static string ArtworkKey(string itemId, string imageType)
        {
            return (itemId ?? "") + "|" + (imageType ?? "");
        }

        private static EmbyMediaItem Artist(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            int year,
            int releaseCount,
            string primaryAsset)
        {
            AddArtwork(artworkUris, id, "Primary", primaryAsset);

            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = "MusicArtist",
                Overview = "A browse-only artist fixture used to validate artist, album, and song navigation.",
                ProductionYear = year,
                ChildCount = releaseCount,
                PrimaryImageTag = ArtworkTag,
                PrimaryImageItemId = id,
                UserData = new EmbyUserData()
            };
        }

        private static EmbyMediaItem Album(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            EmbyMediaItem artist,
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
                AlbumArtist = artist.Name,
                Artists = new[] { artist.Name },
                ArtistItems = new[] { Reference(artist) },
                AlbumArtists = new[] { Reference(artist) },
                UserData = new EmbyUserData()
            };
        }

        private static EmbyMediaItem Song(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            EmbyMediaItem album,
            EmbyMediaItem artist,
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
                AlbumArtist = artist.Name,
                Artists = new[] { artist.Name },
                ArtistItems = new[] { Reference(artist) },
                AlbumArtists = new[] { Reference(artist) },
                UserData = new EmbyUserData()
            };
        }

        private static EmbyItemReference Reference(EmbyMediaItem item)
        {
            return new EmbyItemReference
            {
                Id = item.Id,
                Name = item.Name
            };
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
