using System.Collections.Generic;
using System.Linq;

namespace NoiraPlayer.Core.Diagnostics
{
    public static class SettingsDiagnosticsFormatter
    {
        public static string FormatAccount(string userName, string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                return "Not signed in";
            }

            return string.IsNullOrWhiteSpace(userName)
                ? "Signed in on " + serverUrl
                : userName + " on " + serverUrl;
        }

        public static string FormatVersionSummary(string appVersion, string clientVersion)
        {
            var app = string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion;
            var client = string.IsNullOrWhiteSpace(clientVersion) ? "unknown" : clientVersion;
            return "App " + app + " / Emby client " + client;
        }

        public static string FormatStartupSummary(IEnumerable<string> logLines)
        {
            var lines = (logLines ?? Enumerable.Empty<string>())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (lines.Count == 0)
            {
                return "No startup diagnostics yet";
            }

            var lastLaunchStartIndex = lines.FindLastIndex(line => line.Contains("App.ctor start"));
            var latestLaunchLines = lastLaunchStartIndex >= 0
                ? lines.Skip(lastLaunchStartIndex).ToList()
                : lines;

            if (latestLaunchLines.Any(ContainsException))
            {
                return "Last launch recorded an exception";
            }

            return latestLaunchLines.Any(line => line.Contains("App.OnLaunched completed"))
                ? "Last launch completed"
                : "Startup diagnostics available";
        }

        private static bool ContainsException(string line)
        {
            return line.IndexOf("exception", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
