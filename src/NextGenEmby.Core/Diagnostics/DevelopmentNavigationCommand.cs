using System;
using System.Globalization;
using System.Text.Json;
using NextGenEmby.Core.PlaybackQuality;

namespace NextGenEmby.Core.Diagnostics
{
    public sealed class DevelopmentNavigationCommand
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public string Route { get; set; } = "";
        public string ItemId { get; set; } = "";
        public string ItemName { get; set; } = "";
        public long StartPositionTicks { get; set; }
        public string MediaSourceId { get; set; } = "";
        public bool ForceSdrOutput { get; set; }
        public string StreamUrl { get; set; } = "";
        public bool AutoStart { get; set; }
        public string RunId { get; set; } = "";
        public int DurationSeconds { get; set; } = 10;
        public PlaybackQualityExpected? Expected { get; set; }

        public static bool TryParseJson(
            string json,
            out DevelopmentNavigationCommand? command,
            out string error)
        {
            command = null;
            error = "";

            DevelopmentNavigationCommand? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<DevelopmentNavigationCommand>(json, JsonOptions);
            }
            catch (JsonException)
            {
                error = "dev-command.json must contain valid JSON.";
                return false;
            }

            if (parsed == null)
            {
                error = "dev-command.json must contain a JSON object.";
                return false;
            }

            parsed.Route = NormalizeRoute(parsed.Route);
            parsed.ItemId = Normalize(parsed.ItemId);
            parsed.ItemName = Normalize(parsed.ItemName);
            parsed.MediaSourceId = Normalize(parsed.MediaSourceId);
            parsed.StreamUrl = Normalize(parsed.StreamUrl);
            parsed.RunId = Normalize(parsed.RunId);
            parsed.StartPositionTicks = Math.Max(0, parsed.StartPositionTicks);

            if (string.IsNullOrWhiteSpace(parsed.RunId))
            {
                parsed.RunId = parsed.Route + "-" +
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            }

            if (parsed.DurationSeconds < 10)
            {
                parsed.DurationSeconds = 10;
            }
            else if (parsed.DurationSeconds > 600)
            {
                parsed.DurationSeconds = 600;
            }

            if (!IsSupportedRoute(parsed.Route))
            {
                error = "dev-command.json has an unsupported route.";
                return false;
            }

            if (parsed.Route == "quality-run")
            {
                if (string.IsNullOrWhiteSpace(parsed.ItemId) &&
                    string.IsNullOrWhiteSpace(parsed.StreamUrl))
                {
                    error = "dev-command.json route requires itemId or streamUrl.";
                    return false;
                }
            }
            else if (RequiresItemId(parsed.Route) && string.IsNullOrWhiteSpace(parsed.ItemId))
            {
                error = "dev-command.json route requires itemId.";
                return false;
            }

            command = parsed;
            return true;
        }

        private static string Normalize(string value)
        {
            return value == null ? "" : value.Trim();
        }

        private static string NormalizeRoute(string route)
        {
            return Normalize(route).ToLowerInvariant();
        }

        private static bool IsSupportedRoute(string route)
        {
            switch (route)
            {
                case "home":
                case "movies":
                case "tv":
                case "search":
                case "settings":
                case "livetv":
                case "livetv-unsupported":
                case "music":
                case "music-unsupported":
                case "photos":
                case "playlists":
                case "favorites":
                case "unwatched":
                case "details":
                case "photo":
                case "playback":
                case "manual-playback":
                case "quality-run":
                    return true;

                default:
                    return false;
            }
        }

        private static bool RequiresItemId(string route)
        {
            return route == "details" ||
                route == "photo" ||
                route == "playback";
        }
    }
}
