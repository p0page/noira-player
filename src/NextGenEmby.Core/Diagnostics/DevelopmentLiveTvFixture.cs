using System;
using System.Collections.Generic;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Diagnostics
{
    public static class DevelopmentLiveTvFixture
    {
        private const string ArtworkTag = "qa";

        public static DevelopmentLiveTvFixtureSnapshot Create()
        {
            var artworkUris = new Dictionary<string, string>(StringComparer.Ordinal);
            var baseDate = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
            var channels = new[]
            {
                Channel(
                    artworkUris,
                    "qa-live-news-24",
                    "News 24",
                    "101",
                    "Morning Briefing",
                    "Local Edition",
                    "Headlines, weather, and transit updates for the next hour.",
                    baseDate,
                    TimeSpan.FromMinutes(30),
                    "qa-wide-01.png",
                    isNews: true),
                Channel(
                    artworkUris,
                    "qa-live-cinema",
                    "Cinema One",
                    "202",
                    "Matinee Window",
                    "",
                    "A curated movie block used to validate long Live TV program descriptions.",
                    baseDate.AddMinutes(10),
                    TimeSpan.FromMinutes(110),
                    "qa-wide-02.png",
                    isMovie: true),
                Channel(
                    artworkUris,
                    "qa-live-sports",
                    "Match Center",
                    "303",
                    "Late Match",
                    "Quarter Final",
                    "Live sports coverage with pre-game context and halftime analysis.",
                    baseDate.AddMinutes(20),
                    TimeSpan.FromMinutes(95),
                    "qa-wide-03.png",
                    isSports: true),
                Channel(
                    artworkUris,
                    "qa-live-kids",
                    "Kids Studio",
                    "404",
                    "Saturday Workshop",
                    "Paper City",
                    "A family-safe workshop episode with simple craft segments.",
                    baseDate.AddMinutes(5),
                    TimeSpan.FromMinutes(25),
                    "qa-wide-04.png",
                    isKids: true)
            };

            return new DevelopmentLiveTvFixtureSnapshot(channels, artworkUris);
        }

        public static string ArtworkKey(string itemId, string imageType)
        {
            return (itemId ?? "") + "|" + (imageType ?? "");
        }

        private static EmbyLiveTvChannel Channel(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            string number,
            string programName,
            string episodeTitle,
            string overview,
            DateTimeOffset start,
            TimeSpan duration,
            string primaryAsset,
            bool isMovie = false,
            bool isSports = false,
            bool isNews = false,
            bool isKids = false)
        {
            AddArtwork(artworkUris, id, "Primary", primaryAsset);

            return new EmbyLiveTvChannel
            {
                Id = id,
                Name = name,
                Number = number,
                ChannelType = "TV",
                PrimaryImageTag = ArtworkTag,
                CurrentProgram = new EmbyLiveTvProgram
                {
                    Id = id + "-program",
                    Name = programName,
                    EpisodeTitle = episodeTitle,
                    Overview = overview,
                    OfficialRating = isKids ? "TV-G" : "TV-PG",
                    StartDate = start,
                    EndDate = start.Add(duration),
                    RunTimeTicks = duration.Ticks,
                    IsMovie = isMovie,
                    IsSports = isSports,
                    IsNews = isNews,
                    IsKids = isKids,
                    IsSeries = !isMovie && !isSports && !isNews,
                    ChannelId = id
                }
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
