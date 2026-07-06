using System;
using System.Collections.Generic;
using System.Linq;

namespace NextGenEmby.Core.Input
{
    public static class MediaDetailsVersionSelectionPolicy
    {
        public static MediaDetailsVersionSelectionDecision Select(
            IReadOnlyList<string> availableMediaSourceIds,
            string requestedMediaSourceId,
            string requestedLabel,
            string currentMediaSourceId = "")
        {
            var requested = Normalize(requestedMediaSourceId);
            if (!ContainsSource(availableMediaSourceIds, requested))
            {
                return new MediaDetailsVersionSelectionDecision
                {
                    SelectedMediaSourceId = ResolvePlaybackSource(availableMediaSourceIds, currentMediaSourceId),
                    StatusMessage = "Version unavailable.",
                    StartPlayback = false
                };
            }

            return new MediaDetailsVersionSelectionDecision
            {
                SelectedMediaSourceId = requested,
                StatusMessage = "Version selected: " + CreateLabel(requestedLabel, requested) + ". Press Play to start.",
                StartPlayback = false
            };
        }

        public static string ResolvePlaybackSource(
            IReadOnlyList<string> availableMediaSourceIds,
            string selectedMediaSourceId)
        {
            var selected = Normalize(selectedMediaSourceId);
            if (ContainsSource(availableMediaSourceIds, selected))
            {
                return selected;
            }

            return availableMediaSourceIds == null
                ? ""
                : availableMediaSourceIds.Select(Normalize).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? "";
        }

        private static bool ContainsSource(IReadOnlyList<string> availableMediaSourceIds, string mediaSourceId)
        {
            return availableMediaSourceIds != null &&
                !string.IsNullOrWhiteSpace(mediaSourceId) &&
                availableMediaSourceIds.Any(candidate => string.Equals(
                    Normalize(candidate),
                    mediaSourceId,
                    StringComparison.Ordinal));
        }

        private static string CreateLabel(string requestedLabel, string fallback)
        {
            return string.IsNullOrWhiteSpace(requestedLabel)
                ? fallback
                : requestedLabel.Trim();
        }

        private static string Normalize(string value)
        {
            return value == null ? "" : value.Trim();
        }
    }
}
