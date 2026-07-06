using System;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackRefreshRatePolicy
    {
        public const double MatchTolerance = 0.15;
        public const double NoMatchWeight = 1000000.0;
        public const double ClockSpeedAdjustmentEpsilonPercent = 0.01;

        private static readonly double[] SupportedRatios = { 1.0, 2.0, 2.5, 3.0, 4.0, 5.0 };

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

        public static PlaybackQualityCadenceAssessment AssessCadence(
            double displayRefreshRate,
            double videoFrameRate)
        {
            var assessment = new PlaybackQualityCadenceAssessment
            {
                SourceFrameRate = videoFrameRate,
                DisplayRefreshRateHz = displayRefreshRate,
                ToleranceHz = MatchTolerance
            };

            if (!HasUsableVideoFrameRate(videoFrameRate))
            {
                assessment.Status = "unsupported-source";
                assessment.Reason = "Source frame rate is not usable for cadence assessment.";
                return assessment;
            }

            if (!IsFinite(displayRefreshRate) || displayRefreshRate <= 0.0)
            {
                assessment.Status = "missing-evidence";
                assessment.Reason = "Display refresh rate is missing for cadence assessment.";
                return assessment;
            }

            var bestDelta = double.MaxValue;
            foreach (var ratio in SupportedRatios)
            {
                var targetRefreshRate = videoFrameRate * ratio;
                var difference = Math.Abs(displayRefreshRate - targetRefreshRate);
                if (difference < bestDelta)
                {
                    bestDelta = difference;
                    assessment.BestMultiplier = ratio;
                    assessment.BestTargetRefreshRateHz = targetRefreshRate;
                    assessment.RefreshDeltaHz = difference;
                }
            }

            if (assessment.RefreshDeltaHz <= MatchTolerance)
            {
                ApplyClockSpeedAdjustment(assessment);
                assessment.Status = "matched";
                assessment.Reason = assessment.IsClockSpeedAdjustmentRequired
                    ? "Display refresh rate matches source cadence with clock speed adjustment."
                    : "Display refresh rate matches source cadence.";
            }
            else
            {
                assessment.Status = "mismatch";
                assessment.Reason = "Display refresh rate is outside cadence tolerance.";
            }

            return assessment;
        }

        private static void ApplyClockSpeedAdjustment(
            PlaybackQualityCadenceAssessment assessment)
        {
            if (assessment.BestTargetRefreshRateHz <= 0.0)
            {
                return;
            }

            assessment.ClockSpeedMultiplier =
                assessment.DisplayRefreshRateHz / assessment.BestTargetRefreshRateHz;
            assessment.ClockSpeedAdjustmentPercent =
                (assessment.ClockSpeedMultiplier - 1.0) * 100.0;
            assessment.IsClockSpeedAdjustmentRequired =
                Math.Abs(assessment.ClockSpeedAdjustmentPercent) >
                ClockSpeedAdjustmentEpsilonPercent;
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
