using System;
using System.Text.RegularExpressions;

namespace NoiraPlayer.Core.Playback
{
    public static class HdrPlaybackProfileClassifier
    {
        private static readonly Regex DolbyVisionProfileRegex = new Regex(
            @"\b(?:profile|p)\s*(?<profile>5|7|8)(?:\.(?<compat>\d))?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DolbyVisionCodecRegex = new Regex(
            @"\bdv(?:he|h1)\.0?(?<profile>5|7|8)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static HdrPlaybackProfile Classify(
            string videoRange,
            string colorPrimaries,
            string colorTransfer,
            string colorSpace,
            string codec,
            string displayTitle,
            string mediaSourceName = "")
        {
            var profile = new HdrPlaybackProfile
            {
                VideoRange = videoRange ?? "",
                ColorPrimaries = colorPrimaries ?? "",
                ColorTransfer = colorTransfer ?? "",
                ColorSpace = colorSpace ?? "",
                Codec = codec ?? "",
                DisplayTitle = displayTitle ?? "",
                MediaSourceName = mediaSourceName ?? ""
            };

            var combined = JoinMetadata(profile);
            var hasDolbyVision = ContainsDolbyVision(combined);
            var hasHdr10 = ContainsAny(combined, "hdr10", "hdr10+") || IsPqBt2020(profile);
            var hasHlg = ContainsAny(combined, "hlg", "arib-std-b67");
            var hasBt2020 = ContainsAny(profile.ColorPrimaries, "bt2020") &&
                ContainsAny(profile.ColorSpace, "bt2020");

            if (hasDolbyVision)
            {
                ApplyDolbyVisionFallback(profile, combined, hasHdr10, hasHlg);
                return profile;
            }

            if (hasHlg)
            {
                profile.Kind = HdrPlaybackKind.Hlg;
                return profile;
            }

            if (hasHdr10)
            {
                profile.Kind = HdrPlaybackKind.Hdr10;
                return profile;
            }

            if (ContainsAny(combined, "hdr") || hasBt2020)
            {
                profile.Kind = HdrPlaybackKind.UnknownHdr;
                return profile;
            }

            profile.Kind = HdrPlaybackKind.Sdr;
            return profile;
        }

        private static void ApplyDolbyVisionFallback(
            HdrPlaybackProfile profile,
            string combined,
            bool hasHdr10,
            bool hasHlg)
        {
            profile.IsDolbyVision = true;

            var dvProfile = TryReadDolbyVisionProfile(combined);
            profile.DolbyVisionProfile = dvProfile;
            profile.DolbyVisionCompatibilityId = TryReadDolbyVisionCompatibilityId(combined, dvProfile);

            if (profile.DolbyVisionCompatibilityId == 1)
            {
                hasHdr10 = true;
            }

            if (profile.DolbyVisionCompatibilityId == 4)
            {
                hasHlg = true;
            }

            if (dvProfile == 5 && !hasHdr10 && !hasHlg)
            {
                profile.Kind = HdrPlaybackKind.DolbyVisionUnsupported;
                return;
            }

            if (hasHdr10 || dvProfile == 7)
            {
                profile.Kind = HdrPlaybackKind.DolbyVisionWithHdr10Fallback;
                profile.HasHdr10BaseLayer = true;
                return;
            }

            if (hasHlg)
            {
                profile.Kind = HdrPlaybackKind.DolbyVisionWithHlgFallback;
                profile.HasHlgBaseLayer = true;
                return;
            }

            profile.Kind = HdrPlaybackKind.UnknownHdr;
        }

        private static bool IsPqBt2020(HdrPlaybackProfile profile)
        {
            return ContainsAny(profile.ColorTransfer, "smpte2084", "pq") &&
                ContainsAny(profile.ColorPrimaries, "bt2020") &&
                ContainsAny(profile.ColorSpace, "bt2020");
        }

        private static int? TryReadDolbyVisionProfile(string combined)
        {
            var match = DolbyVisionProfileRegex.Match(combined);
            if (match.Success)
            {
                return ParseInt(match.Groups["profile"].Value);
            }

            match = DolbyVisionCodecRegex.Match(combined);
            return match.Success ? ParseInt(match.Groups["profile"].Value) : null;
        }

        private static int? TryReadDolbyVisionCompatibilityId(string combined, int? dvProfile)
        {
            var profileMatch = DolbyVisionProfileRegex.Match(combined);
            if (profileMatch.Success)
            {
                var compat = ParseInt(profileMatch.Groups["compat"].Value);
                if (compat.HasValue)
                {
                    return compat.Value;
                }
            }

            if (Regex.IsMatch(combined, @"(?:compat|compatibility)\s*(?:id)?\s*[:=]?\s*1", RegexOptions.IgnoreCase))
            {
                return 1;
            }

            if (Regex.IsMatch(combined, @"(?:compat|compatibility)\s*(?:id)?\s*[:=]?\s*4", RegexOptions.IgnoreCase))
            {
                return 4;
            }

            if (dvProfile == 8 && ContainsAny(combined, "hdr10"))
            {
                return 1;
            }

            if (dvProfile == 8 && ContainsAny(combined, "hlg"))
            {
                return 4;
            }

            return null;
        }

        private static int? ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : (int?)null;
        }

        private static bool ContainsDolbyVision(string combined)
        {
            return Regex.IsMatch(
                combined,
                @"dolby\s*vision|\bdovi\b|\bdv\b|\bdvhe\.0?[578]\b|\bdvh1\.0?[578]\b|\bdv\s*(?:p|profile)?\s*[578]\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string JoinMetadata(HdrPlaybackProfile profile)
        {
            return string.Join(
                " ",
                new[]
                {
                    profile.VideoRange,
                    profile.ColorPrimaries,
                    profile.ColorTransfer,
                    profile.ColorSpace,
                    profile.Codec
                });
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (var needle in needles)
            {
                if (value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
