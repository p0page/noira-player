using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualitySourceFingerprint
    {
        public static string Compute(string locator)
        {
            var bytes = Encoding.UTF8.GetBytes(locator ?? "");
            return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        public static string ComputeOpenedSource(string locator)
        {
            if (!Uri.TryCreate(locator, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return Compute(locator);
            }

            var stableQuery = uri.Query
                .TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !string.Equals(
                    Uri.UnescapeDataString(part.Split('=', 2)[0]),
                    "PlaySessionId",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(part => part, StringComparer.Ordinal)
                .ToArray();
            var identity = uri.GetLeftPart(UriPartial.Path);
            if (stableQuery.Length > 0)
            {
                identity += "?" + string.Join("&", stableQuery);
            }

            return Compute(identity);
        }
    }
}
