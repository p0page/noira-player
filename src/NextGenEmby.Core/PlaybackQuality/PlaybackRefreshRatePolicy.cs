using System;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackRefreshRatePolicy
    {
        public const double MatchTolerance = 0.15;
        public const double NoMatchWeight = 1000000.0;

        private static readonly double[] SupportedRatios = { 1.0, 2.0, 2.5 };

        public static bool HasUsableVideoFrameRate(double videoFrameRate)
        {
            return IsFinite(videoFrameRate) &&
                videoFrameRate >= 5.0 &&
                videoFrameRate <= 120.0;
        }

        public static bool MatchesVideoFrameRate(double displayRefreshRate, double videoFrameRate)
        {
            return RefreshWeight(displayRefreshRate, videoFrameRate) < NoMatchWeight;
        }

        public static bool IsBetterRefreshRateForVideo(
            double candidateRefreshRate,
            double selectedRefreshRate,
            double videoFrameRate)
        {
            return RefreshWeight(candidateRefreshRate, videoFrameRate) <
                RefreshWeight(selectedRefreshRate, videoFrameRate);
        }

        public static double RefreshWeight(double displayRefreshRate, double videoFrameRate)
        {
            if (!HasUsableVideoFrameRate(videoFrameRate) ||
                !IsFinite(displayRefreshRate) ||
                displayRefreshRate <= 0.0)
            {
                return NoMatchWeight;
            }

            var bestWeight = NoMatchWeight;
            foreach (var ratio in SupportedRatios)
            {
                var targetRefreshRate = videoFrameRate * ratio;
                var difference = Math.Abs(displayRefreshRate - targetRefreshRate);
                if (difference > MatchTolerance)
                {
                    continue;
                }

                var weight = difference / videoFrameRate;
                if (displayRefreshRate <= 30.0 && !IsCinemaLowRefresh(displayRefreshRate))
                {
                    weight += 1.0;
                }

                if (displayRefreshRate > 60.0 && ratio > 1.0)
                {
                    weight += ratio / 10000.0;
                }

                if (weight < bestWeight)
                {
                    bestWeight = weight;
                }
            }

            return bestWeight;
        }

        private static bool IsCinemaLowRefresh(double displayRefreshRate)
        {
            return Math.Abs(displayRefreshRate - 24.0) <= MatchTolerance ||
                Math.Abs(displayRefreshRate - (24.0 / 1.001)) <= MatchTolerance;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
