using System;
using System.Security.Cryptography;
using System.Text;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualitySourceFingerprint
    {
        public static string Compute(string locator)
        {
            var bytes = Encoding.UTF8.GetBytes(locator ?? "");
            return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
    }
}
