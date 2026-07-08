using System;
using System.Collections.Generic;
using System.Linq;
using NoiraPlayer.Core.Emby;

namespace NoiraPlayer.Core.Diagnostics
{
    public enum DevelopmentRealDetailsSampleMode
    {
        FirstSupported,
        BrightestArtwork
    }

    public static class DevelopmentRealDetailsSampleSelector
    {
        public static EmbyMediaItem? SelectFirstSupported(IReadOnlyList<EmbyMediaItem>? items)
        {
            return (items ?? Array.Empty<EmbyMediaItem>())
                .FirstOrDefault(HasDetailsHeroArtwork);
        }

        public static EmbyMediaItem? SelectBrightestSupported(
            IReadOnlyList<EmbyMediaItem>? items,
            IReadOnlyDictionary<string, double>? brightnessScores)
        {
            var supportedItems = (items ?? Array.Empty<EmbyMediaItem>())
                .Where(HasDetailsHeroArtwork)
                .ToList();
            if (supportedItems.Count == 0)
            {
                return null;
            }

            var brightest = supportedItems
                .Select(item => new
                {
                    Item = item,
                    Score = ResolveBrightnessScore(item, brightnessScores)
                })
                .Where(entry => entry.Score.HasValue)
                .OrderByDescending(entry => entry.Score!.Value)
                .FirstOrDefault();

            return brightest?.Item ?? supportedItems[0];
        }

        private static bool HasDetailsHeroArtwork(EmbyMediaItem item)
        {
            return EmbyArtworkPolicy.SelectHeroArtwork(item, 1920) != null;
        }

        private static double? ResolveBrightnessScore(
            EmbyMediaItem item,
            IReadOnlyDictionary<string, double>? brightnessScores)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || brightnessScores == null)
            {
                return null;
            }

            double score;
            return brightnessScores.TryGetValue(item.Id, out score) ? score : (double?)null;
        }
    }
}
