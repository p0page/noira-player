using System;
using System.Collections.Generic;

namespace NoiraPlayer.Core.Emby
{
    public static class EmbyLibraryItemTypePolicy
    {
        public static IReadOnlyList<EmbyMediaItem> KeepIncludedItemTypes(
            IReadOnlyList<EmbyMediaItem> items,
            string includeItemTypes)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<EmbyMediaItem>();
            }

            var includedTypes = ParseIncludedTypes(includeItemTypes);
            if (includedTypes.Count == 0)
            {
                return items;
            }

            var filtered = new List<EmbyMediaItem>();
            foreach (var item in items)
            {
                if (item != null && includedTypes.Contains(item.Type ?? ""))
                {
                    filtered.Add(item);
                }
            }

            return filtered;
        }

        private static HashSet<string> ParseIncludedTypes(string includeItemTypes)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(includeItemTypes))
            {
                return result;
            }

            var parts = includeItemTypes.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    result.Add(trimmed);
                }
            }

            return result;
        }
    }
}
