using System;
using System.Collections.Generic;
using System.Linq;
using NoiraPlayer.Core.Emby;

namespace NoiraPlayer.Core.Diagnostics
{
    public sealed class DevelopmentLibraryOrganizationFixtureSnapshot
    {
        public DevelopmentLibraryOrganizationFixtureSnapshot(
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
}
