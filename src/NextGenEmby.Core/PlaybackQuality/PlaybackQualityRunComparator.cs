using System;
using System.Collections.Generic;
using System.Globalization;

namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityRunComparison
    {
        public string BaselineRunId { get; set; } = "";
        public string CandidateRunId { get; set; } = "";
        public string Result { get; set; } = "unchanged";
        public string Decision { get; set; } = "no-change";
        public string SuggestedNextAction { get; set; } = "";
        public PlaybackQualityComparabilityAssessment Comparability { get; set; } =
            new PlaybackQualityComparabilityAssessment();
        public PlaybackQualityComparisonCoverage Coverage { get; set; } =
            new PlaybackQualityComparisonCoverage();
        public List<PlaybackQualitySignalDelta> Improvements { get; } = new List<PlaybackQualitySignalDelta>();
        public List<PlaybackQualitySignalDelta> Regressions { get; } = new List<PlaybackQualitySignalDelta>();
        public List<string> ResolvedFailureAreas { get; } = new List<string>();
        public List<string> NewFailureAreas { get; } = new List<string>();
        public List<string> PersistingFailureAreas { get; } = new List<string>();
        public List<string> Limitations { get; } = new List<string>();
    }

    public sealed class PlaybackQualityComparabilityAssessment
    {
        public string Status { get; set; } = "comparable";
        public List<string> Reasons { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
    }

    public sealed class PlaybackQualityComparisonCoverage
    {
        public int BaselineCheckCount { get; set; }
        public int CandidateCheckCount { get; set; }
        public int MatchedCheckCount { get; set; }
        public List<string> MatchedSignals { get; } = new List<string>();
        public List<string> UnmatchedBaselineSignals { get; } = new List<string>();
        public List<string> UnmatchedCandidateSignals { get; } = new List<string>();
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

            var comparison = new PlaybackQualityRunComparison
            {
                BaselineRunId = baseline.RunId,
                CandidateRunId = candidate.RunId
            };
            comparison.Coverage.BaselineCheckCount = baseline.Checks.Count;
            comparison.Coverage.CandidateCheckCount = candidate.Checks.Count;
            comparison.Comparability = AssessComparability(baseline, candidate);
            if (comparison.Comparability.Status == "incompatible")
            {
                comparison.Result = "insufficient-evidence";
                foreach (var signal in comparison.Comparability.Signals)
                {
                    comparison.Limitations.Add("comparison requires matching " + signal);
                }

                ApplyDecision(comparison);
                return comparison;
            }

            if (baseline.Checks.Count == 0 || candidate.Checks.Count == 0)
            {
                comparison.Result = "insufficient-evidence";
                comparison.Limitations.Add("comparison requires baseline and candidate checks");
                ApplyDecision(comparison);
                return comparison;
            }

            var candidateByKey = CreateCheckMap(candidate);
            var baselineByKey = CreateCheckMap(baseline);
            var matchedKeys = new List<string>();
            var matchedChecks = 0;
            foreach (var baselineCheck in baseline.Checks)
            {
                var key = GetCheckKey(baselineCheck);
                if (string.IsNullOrWhiteSpace(key) || !candidateByKey.ContainsKey(key))
                {
                    continue;
                }

                matchedChecks++;
                AddUnique(matchedKeys, key);
                comparison.Coverage.MatchedCheckCount++;
                AddUnique(comparison.Coverage.MatchedSignals, key);
                CompareCheck(comparison, baselineCheck, candidateByKey[key]);
                TrackFailureArea(comparison, baselineCheck, candidateByKey[key]);
            }

            AddUnmatchedBaselineSignals(comparison, baseline, matchedKeys);

            if (matchedChecks == 0)
            {
                comparison.Result = "insufficient-evidence";
                comparison.Limitations.Add("comparison requires at least one matching check signal");
                ApplyDecision(comparison);
                return comparison;
            }

            AddCandidateOnlyFailures(comparison, candidate, baselineByKey, matchedKeys);
            AddUnmatchedCandidateSignals(comparison, candidate, matchedKeys);

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

            ApplyDecision(comparison);
            return comparison;
        }

        private static void AddUnmatchedBaselineSignals(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityReport baseline,
            List<string> matchedKeys)
        {
            foreach (var baselineCheck in baseline.Checks)
            {
                var key = GetCheckKey(baselineCheck);
                if (!string.IsNullOrWhiteSpace(key) && !matchedKeys.Contains(key))
                {
                    AddUnique(comparison.Coverage.UnmatchedBaselineSignals, key);
                }
            }
        }

        private static void AddUnmatchedCandidateSignals(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityReport candidate,
            List<string> matchedKeys)
        {
            foreach (var candidateCheck in candidate.Checks)
            {
                var key = GetCheckKey(candidateCheck);
                if (!string.IsNullOrWhiteSpace(key) && !matchedKeys.Contains(key))
                {
                    AddUnique(comparison.Coverage.UnmatchedCandidateSignals, key);
                }
            }
        }

        private static PlaybackQualityComparabilityAssessment AssessComparability(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            var assessment = new PlaybackQualityComparabilityAssessment();
            AddMismatchIfBothPresent(
                assessment,
                "source.itemId",
                baseline.Source.ItemId,
                candidate.Source.ItemId);
            AddMismatchIfBothPresent(
                assessment,
                "source.mediaSourceId",
                baseline.Source.MediaSourceId,
                candidate.Source.MediaSourceId);
            AddMismatchIfBothPresent(
                assessment,
                "source.hdrKind",
                baseline.Source.HdrKind,
                candidate.Source.HdrKind);
            AddMismatchIfBothPresent(
                assessment,
                "metricVersion",
                baseline.MetricVersion,
                candidate.MetricVersion);

            if (baseline.Source.FrameRate > 0 &&
                candidate.Source.FrameRate > 0 &&
                Math.Abs(baseline.Source.FrameRate - candidate.Source.FrameRate) > 0.01)
            {
                AddIncompatibility(assessment, "source.frameRate");
            }

            return assessment;
        }

        private static void AddMismatchIfBothPresent(
            PlaybackQualityComparabilityAssessment assessment,
            string signal,
            string baselineValue,
            string candidateValue)
        {
            if (!string.IsNullOrWhiteSpace(baselineValue) &&
                !string.IsNullOrWhiteSpace(candidateValue) &&
                !string.Equals(baselineValue, candidateValue, StringComparison.Ordinal))
            {
                AddIncompatibility(assessment, signal);
            }
        }

        private static void AddIncompatibility(
            PlaybackQualityComparabilityAssessment assessment,
            string signal)
        {
            assessment.Status = "incompatible";
            AddUnique(assessment.Reasons, signal + " mismatch");
            AddUnique(assessment.Signals, signal);
        }

        private static void ApplyDecision(PlaybackQualityRunComparison comparison)
        {
            switch (comparison.Result)
            {
                case "improved":
                    comparison.Decision = "keep-candidate";
                    comparison.SuggestedNextAction =
                        "Keep candidate playback Core change and continue investigating persisting failure areas.";
                    break;
                case "regressed":
                    comparison.Decision = "reject-candidate";
                    comparison.SuggestedNextAction =
                        "Reject or revert candidate playback Core change before further optimization.";
                    break;
                case "mixed":
                    comparison.Decision = "split-candidate";
                    comparison.SuggestedNextAction =
                        "Split candidate change or isolate regressions before keeping playback Core changes.";
                    break;
                case "insufficient-evidence":
                    comparison.Decision = "collect-comparable-evidence";
                    comparison.SuggestedNextAction =
                        "Collect comparable baseline and candidate checks before deciding on playback Core changes.";
                    break;
                default:
                    comparison.Decision = "no-change";
                    comparison.SuggestedNextAction =
                        "No comparable playback quality change was detected; continue with the next triage step.";
                    break;
            }
        }

        private static void AddCandidateOnlyFailures(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityReport candidate,
            Dictionary<string, PlaybackQualityCheck> baselineByKey,
            List<string> matchedKeys)
        {
            foreach (var candidateCheck in candidate.Checks)
            {
                var key = GetCheckKey(candidateCheck);
                if (string.IsNullOrWhiteSpace(key) ||
                    baselineByKey.ContainsKey(key) ||
                    matchedKeys.Contains(key) ||
                    candidateCheck.Status != "fail")
                {
                    continue;
                }

                comparison.Regressions.Add(CreateCandidateOnlyDelta(candidateCheck));
                AddUnique(comparison.NewFailureAreas, candidateCheck.FailureArea);
            }
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

        private static PlaybackQualitySignalDelta CreateCandidateOnlyDelta(
            PlaybackQualityCheck candidate)
        {
            return new PlaybackQualitySignalDelta
            {
                Signal = candidate.Signal,
                FailureArea = candidate.FailureArea,
                Direction = "candidate-only-failure",
                CandidateStatus = candidate.Status,
                CandidateActual = candidate.Actual
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
