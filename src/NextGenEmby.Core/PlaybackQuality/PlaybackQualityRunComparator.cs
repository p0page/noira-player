using System;
using System.Collections.Generic;
using System.Globalization;

namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityRunComparison
    {
        public string Result { get; set; } = "unchanged";
        public List<PlaybackQualitySignalDelta> Improvements { get; } = new List<PlaybackQualitySignalDelta>();
        public List<PlaybackQualitySignalDelta> Regressions { get; } = new List<PlaybackQualitySignalDelta>();
        public List<string> ResolvedFailureAreas { get; } = new List<string>();
        public List<string> NewFailureAreas { get; } = new List<string>();
        public List<string> PersistingFailureAreas { get; } = new List<string>();
        public List<string> Limitations { get; } = new List<string>();
    }

    public sealed class PlaybackQualitySignalDelta
    {
        public string Signal { get; set; } = "";
        public string FailureArea { get; set; } = "";
        public string Direction { get; set; } = "";
        public string BaselineStatus { get; set; } = "";
        public string CandidateStatus { get; set; } = "";
        public string BaselineActual { get; set; } = "";
        public string CandidateActual { get; set; } = "";
        public double NumericDelta { get; set; }
    }

    public static class PlaybackQualityRunComparator
    {
        public static PlaybackQualityRunComparison Compare(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            if (baseline == null)
            {
                throw new ArgumentNullException(nameof(baseline));
            }

            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var comparison = new PlaybackQualityRunComparison();
            if (baseline.Checks.Count == 0 || candidate.Checks.Count == 0)
            {
                comparison.Result = "insufficient-evidence";
                comparison.Limitations.Add("comparison requires baseline and candidate checks");
                return comparison;
            }

            var candidateByKey = CreateCheckMap(candidate);
            foreach (var baselineCheck in baseline.Checks)
            {
                var key = GetCheckKey(baselineCheck);
                if (string.IsNullOrWhiteSpace(key) || !candidateByKey.ContainsKey(key))
                {
                    continue;
                }

                CompareCheck(comparison, baselineCheck, candidateByKey[key]);
                TrackFailureArea(comparison, baselineCheck, candidateByKey[key]);
            }

            if (comparison.Improvements.Count > 0 && comparison.Regressions.Count > 0)
            {
                comparison.Result = "mixed";
            }
            else if (comparison.Improvements.Count > 0)
            {
                comparison.Result = "improved";
            }
            else if (comparison.Regressions.Count > 0)
            {
                comparison.Result = "regressed";
            }

            return comparison;
        }

        private static Dictionary<string, PlaybackQualityCheck> CreateCheckMap(
            PlaybackQualityReport report)
        {
            var map = new Dictionary<string, PlaybackQualityCheck>();
            foreach (var check in report.Checks)
            {
                var key = GetCheckKey(check);
                if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
                {
                    map.Add(key, check);
                }
            }

            return map;
        }

        private static void CompareCheck(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityCheck baseline,
            PlaybackQualityCheck candidate)
        {
            if (baseline.Status == "fail" && candidate.Status == "pass")
            {
                comparison.Improvements.Add(CreateDelta(baseline, candidate, "resolved", 0));
                return;
            }

            if (baseline.Status == "pass" && candidate.Status == "fail")
            {
                comparison.Regressions.Add(CreateDelta(baseline, candidate, "new-failure", 0));
                return;
            }

            if (baseline.Status != "fail" || candidate.Status != "fail")
            {
                return;
            }

            if (!TryParseDouble(baseline.Actual, out var baselineActual) ||
                !TryParseDouble(candidate.Actual, out var candidateActual))
            {
                return;
            }

            var numericDelta = candidateActual - baselineActual;
            var higherIsBetter = IsHigherBetterSignal(GetCheckKey(candidate));
            if ((!higherIsBetter && numericDelta < 0) ||
                (higherIsBetter && numericDelta > 0))
            {
                comparison.Improvements.Add(CreateDelta(
                    baseline,
                    candidate,
                    numericDelta < 0 ? "decreased" : "increased",
                    numericDelta));
            }
            else if ((!higherIsBetter && numericDelta > 0) ||
                (higherIsBetter && numericDelta < 0))
            {
                comparison.Regressions.Add(CreateDelta(
                    baseline,
                    candidate,
                    numericDelta < 0 ? "decreased" : "increased",
                    numericDelta));
            }
        }

        private static bool IsHigherBetterSignal(string signal)
        {
            return signal == "timing.renderedVideoFrames";
        }

        private static void TrackFailureArea(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityCheck baseline,
            PlaybackQualityCheck candidate)
        {
            if (baseline.Status == "fail" && candidate.Status == "pass")
            {
                AddUnique(comparison.ResolvedFailureAreas, baseline.FailureArea);
                return;
            }

            if (baseline.Status == "pass" && candidate.Status == "fail")
            {
                AddUnique(comparison.NewFailureAreas, candidate.FailureArea);
                return;
            }

            if (baseline.Status == "fail" && candidate.Status == "fail")
            {
                AddUnique(comparison.PersistingFailureAreas, candidate.FailureArea);
            }
        }

        private static PlaybackQualitySignalDelta CreateDelta(
            PlaybackQualityCheck baseline,
            PlaybackQualityCheck candidate,
            string direction,
            double numericDelta)
        {
            return new PlaybackQualitySignalDelta
            {
                Signal = string.IsNullOrWhiteSpace(candidate.Signal) ? baseline.Signal : candidate.Signal,
                FailureArea = string.IsNullOrWhiteSpace(candidate.FailureArea)
                    ? baseline.FailureArea
                    : candidate.FailureArea,
                Direction = direction,
                BaselineStatus = baseline.Status,
                CandidateStatus = candidate.Status,
                BaselineActual = baseline.Actual,
                CandidateActual = candidate.Actual,
                NumericDelta = numericDelta
            };
        }

        private static string GetCheckKey(PlaybackQualityCheck check)
        {
            return string.IsNullOrWhiteSpace(check.Signal) ? check.Name : check.Signal;
        }

        private static bool TryParseDouble(string value, out double result)
        {
            return double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }
    }
}
