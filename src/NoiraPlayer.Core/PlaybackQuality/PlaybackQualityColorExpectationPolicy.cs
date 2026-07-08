namespace NoiraPlayer.Core.PlaybackQuality
{
    internal static class PlaybackQualityColorExpectationPolicy
    {
        public static bool RequiresSurfaceEvidence(PlaybackQualityExpected expected)
        {
            return expected != null &&
                (!string.IsNullOrWhiteSpace(expected.HdrOutput) ||
                !string.IsNullOrWhiteSpace(expected.DxgiOutput));
        }

        public static bool RequiresTenBitSwapChain(PlaybackQualityExpected expected)
        {
            return expected != null &&
                (IsHdr10LikeOutput(expected.HdrOutput) ||
                IsHdr10LikeColorSpace(expected.DxgiOutput));
        }

        private static bool IsHdr10LikeOutput(string value)
        {
            return string.Equals(value, "Hdr10", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Hdr", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Hlg", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHdr10LikeColorSpace(string value)
        {
            return value.IndexOf("G2084", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("P2020", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
