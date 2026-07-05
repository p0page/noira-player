using System;
using System.Text.Json;

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
            parsed.StartPositionTicks = Math.Max(0, parsed.StartPositionTicks);

            if (!IsSupportedRoute(parsed.Route))
            {
                error = "dev-command.json has an unsupported route.";
                return false;
            }

            if (RequiresItemId(parsed.Route) && string.IsNullOrWhiteSpace(parsed.ItemId))
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
                case "details":
                case "playback":
                    return true;

                default:
                    return false;
            }
        }

        private static bool RequiresItemId(string route)
        {
            return route == "details" || route == "playback";
        }
    }
}
