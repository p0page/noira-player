using System;
using System.Collections.Generic;
using System.Linq;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Diagnostics
{
    public sealed class DevelopmentPhotosFixtureSnapshot
    {
        public DevelopmentPhotosFixtureSnapshot(
            IReadOnlyList<EmbyMediaItem> items,
            IReadOnlyDictionary<string, string> artworkUris)
        {
            Items = items ?? Array.Empty<EmbyMediaItem>();
            ArtworkUris = artworkUris ?? new Dictionary<string, string>();
        }

        public IReadOnlyList<EmbyMediaItem> Items { get; }

        public IReadOnlyDictionary<string, string> ArtworkUris { get; }

        public IReadOnlyList<EmbyMediaItem> GetItemsForParent(string parentId)
        {
            var normalizedParentId = parentId ?? "";
            return Items
                .Where(item => string.Equals(item.ParentId ?? "", normalizedParentId, StringComparison.Ordinal))
                .ToList();
        }
    }

    public static class DevelopmentPhotosFixture
    {
        private const string ArtworkTag = "";

        public static DevelopmentPhotosFixtureSnapshot Create()
        {
            var artworkUris = new Dictionary<string, string>(StringComparer.Ordinal);
            var album = Folder(
                artworkUris,
                "fixture-photo-album-night-market",
                "Night Market",
                2026,
                12,
                "");

            var items = new[]
            {
                album,
                Photo(artworkUris, "fixture-photo-rooftop", "Rooftop After Rain", "", 2026, ""),
                Photo(artworkUris, "fixture-photo-window", "Window Seat", "", 2025, ""),
                Photo(artworkUris, "fixture-photo-lanterns", "Lantern Street", album.Id, 2026, ""),
                Photo(artworkUris, "fixture-photo-noodles", "Late Noodles", album.Id, 2026, ""),
                Photo(artworkUris, "fixture-photo-crossing", "Blue Crossing", album.Id, 2026, ""),
                Photo(artworkUris, "fixture-photo-last-train", "Last Train", album.Id, 2026, ""),
                Photo(artworkUris, "fixture-photo-station-arcade", "Station Arcade", album.Id, 2026, ""),
                Photo(artworkUris, "fixture-photo-rain-glass", "Rain Glass", album.Id, 2026, ""),
                Photo(artworkUris, "fixture-photo-rooftop-market", "Rooftop Market", album.Id, 2026, ""),
                Photo(artworkUris, "fixture-photo-midnight-stall", "Midnight Stall", album.Id, 2026, ""),
                Photo(artworkUris, "fixture-photo-neon-crosswalk", "Neon Crosswalk", album.Id, 2026, ""),
                Photo(artworkUris, "fixture-photo-platform-lights", "Platform Lights", album.Id, 2026, ""),
                Photo(artworkUris, "fixture-photo-market-awning", "Market Awning", album.Id, 2026, ""),
                Photo(artworkUris, "fixture-photo-closing-time", "Closing Time", album.Id, 2026, "")
            };

            return new DevelopmentPhotosFixtureSnapshot(items, artworkUris);
        }

        public static string ArtworkKey(string itemId, string imageType)
        {
            return (itemId ?? "") + "|" + (imageType ?? "");
        }

        private static EmbyMediaItem Folder(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            int year,
            int childCount,
            string primaryAsset)
        {
            AddArtwork(artworkUris, id, "Primary", primaryAsset);

            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = "Folder",
                Overview = "A photo album fixture used to validate nested couch navigation without a live photo library.",
                ProductionYear = year,
                ChildCount = childCount,
                PrimaryImageTag = ArtworkTag,
                PrimaryImageItemId = id,
                UserData = new EmbyUserData()
            };
        }

        private static EmbyMediaItem Photo(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            string parentId,
            int year,
            string primaryAsset)
        {
            AddArtwork(artworkUris, id, "Primary", primaryAsset);

            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = "Photo",
                Overview = "A local photo fixture used for positive viewer validation.",
                ParentId = parentId ?? "",
                ProductionYear = year,
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
            _ = artworkUris;
            _ = itemId;
            _ = imageType;
            _ = assetName;
        }
    }
}
