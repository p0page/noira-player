using System;
using System.Collections.Generic;

namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityComparisonSuite
    {
        public int TotalComparisonCount { get; set; }
        public int ImprovedCount { get; set; }
        public int RegressedCount { get; set; }
        public int MixedCount { get; set; }
        public int UnchangedCount { get; set; }
        public int InsufficientEvidenceCount { get; set; }
        public int StrongConfidenceCount { get; set; }
        public int PartialConfidenceCount { get; set; }
        public int WeakConfidenceCount { get; set; }
        public string Action { get; set; } = "collect-comparable-evidence";
        public string Risk { get; set; } = "high";
        public List<string> Reasons { get; } = new List<string>();
        public List<string> Blockers { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
        public List<string> FailureAreas { get; } = new List<string>();
        public List<string> TargetFailureAreas { get; } = new List<string>();
        public List<PlaybackQualityComparisonCaseSummary> Cases { get; } =
            new List<PlaybackQualityComparisonCaseSummary>();
        public List<PlaybackQualityRunComparison> Comparisons { get; } =
            new List<PlaybackQualityRunComparison>();
    }

    public sealed class PlaybackQualityComparisonCaseSummary
    {
        public string CaseId { get; set; } = "";
        public string BaselineRunId { get; set; } = "";
        public string CandidateRunId { get; set; } = "";
        public string Result { get; set; } = "";
        public string Decision { get; set; } = "";
        public string Action { get; set; } = "";
        public string Risk { get; set; } = "";
        public string Confidence { get; set; } = "";
        public List<string> Signals { get; } = new List<string>();
        public List<string> FailureAreas { get; } = new List<string>();
        public List<string> Blockers { get; } = new List<string>();
    }

    public static class PlaybackQualityComparisonSuiteAggregator
    {
        public static PlaybackQualityComparisonSuite Summarize(
            IEnumerable<PlaybackQualityRunComparison> comparisons)
        {
            if (comparisons == null)
            {
                throw new ArgumentNullException(nameof(comparisons));
            }

            var suite = new PlaybackQualityComparisonSuite();
            foreach (var comparison in comparisons)
            {
                if (comparison == null)
                {
                    continue;
                }

                suite.Comparisons.Add(comparison);
                suite.Cases.Add(CreateCaseSummary(comparison));
                CountComparison(suite, comparison);
                AddComparisonEvidence(suite, comparison);
            }

            suite.TotalComparisonCount = suite.Comparisons.Count;
            ApplySuiteAction(suite);
            ApplyTargetFailureAreas(suite);
            return suite;
        }

        private static PlaybackQualityComparisonCaseSummary CreateCaseSummary(
            PlaybackQualityRunComparison comparison)
        {
            var summary = new PlaybackQualityComparisonCaseSummary
            {
                CaseId = comparison.CaseId,
                BaselineRunId = comparison.BaselineRunId,
                CandidateRunId = comparison.CandidateRunId,
                Result = comparison.Result,
                Decision = comparison.Decision,
                Action = comparison.Optimization.Action,
                Risk = comparison.Optimization.Risk,
                Confidence = comparison.Confidence.Level
            };

            foreach (var signal in comparison.Optimization.Signals)
            {
                AddUnique(summary.Signals, signal);
            }

            foreach (var signal in comparison.Confidence.Signals)
            {
                AddUnique(summary.Signals, signal);
            }

            foreach (var improvement in comparison.Improvements)
            {
                AddUnique(summary.Signals, improvement.Signal);
                AddUnique(summary.FailureAreas, improvement.FailureArea);
            }

            foreach (var regression in comparison.Regressions)
            {
                AddUnique(summary.Signals, regression.Signal);
                AddUnique(summary.FailureAreas, regression.FailureArea);
            }

            foreach (var area in comparison.Optimization.FailureAreas)
            {
                AddUnique(summary.FailureAreas, area);
            }

            foreach (var area in comparison.PersistingFailureAreas)
            {
                AddUnique(summary.FailureAreas, area);
            }

            if (comparison.PersistingFailureAreas.Count > 0)
            {
                foreach (var signal in comparison.Coverage.MatchedSignals)
                {
                    AddUnique(summary.Signals, signal);
                }
            }

            foreach (var blocker in comparison.Optimization.Blockers)
            {
                AddUnique(summary.Blockers, blocker);
            }

            return summary;
        }

        private static void CountComparison(
            PlaybackQualityComparisonSuite suite,
            PlaybackQualityRunComparison comparison)
        {
            switch (comparison.Result)
            {
                case "improved":
                    suite.ImprovedCount++;
                    break;
                case "regressed":
                    suite.RegressedCount++;
                    break;
                case "mixed":
                    suite.MixedCount++;
                    break;
                case "insufficient-evidence":
                    suite.InsufficientEvidenceCount++;
                    break;
                default:
                    suite.UnchangedCount++;
                    break;
            }

            switch (comparison.Confidence.Level)
            {
                case "strong":
                    suite.StrongConfidenceCount++;
                    break;
                case "partial":
                    suite.PartialConfidenceCount++;
                    break;
                default:
                    suite.WeakConfidenceCount++;
                    break;
            }
        }

        private static void AddComparisonEvidence(
            PlaybackQualityComparisonSuite suite,
            PlaybackQualityRunComparison comparison)
        {
            foreach (var signal in comparison.Optimization.Signals)
            {
                AddUnique(suite.Signals, signal);
            }

            foreach (var signal in comparison.Confidence.Signals)
            {
                AddUnique(suite.Signals, signal);
            }

            foreach (var regression in comparison.Regressions)
            {
                AddUnique(suite.Signals, regression.Signal);
                AddUnique(suite.FailureAreas, regression.FailureArea);
            }

            foreach (var area in comparison.NewFailureAreas)
            {
                AddUnique(suite.FailureAreas, area);
            }

            foreach (var area in comparison.Optimization.FailureAreas)
            {
                AddUnique(suite.FailureAreas, area);
            }

            foreach (var area in comparison.PersistingFailureAreas)
            {
                AddUnique(suite.FailureAreas, area);
            }

            if (comparison.PersistingFailureAreas.Count > 0)
            {
                foreach (var signal in comparison.Coverage.MatchedSignals)
                {
                    AddUnique(suite.Signals, signal);
                }
            }
        }

        private static void ApplySuiteAction(PlaybackQualityComparisonSuite suite)
        {
            if (suite.TotalComparisonCount == 0)
            {
                suite.Action = "collect-comparable-evidence";
                suite.Risk = "high";
                AddUnique(suite.Blockers, "suite.empty");
                AddUnique(suite.Reasons, "suite has no playback quality comparisons");
                return;
            }

            if (HasOptimizationAction(suite, "reject-candidate") ||
                HasOptimizationAction(suite, "isolate-candidate-regression") ||
                suite.RegressedCount > 0)
            {
                suite.Action = "reject-candidate";
                suite.Risk = "high";
                AddUnique(suite.Blockers, "suite.regression");
                AddUnique(suite.Reasons, "suite contains at least one candidate regression");
                return;
            }

            if (HasOptimizationAction(suite, "split-candidate") || suite.MixedCount > 0)
            {
                suite.Action = "split-candidate";
                suite.Risk = "high";
                AddUnique(suite.Blockers, "suite.mixed-results");
                AddUnique(suite.Reasons, "suite contains mixed playback quality changes");
                return;
            }

            if (HasOptimizationAction(suite, "change-optimization-strategy"))
            {
                suite.Action = "change-optimization-strategy";
                suite.Risk = "high";
                AddUnique(suite.Blockers, "suite.stalled");
                AddUnique(suite.Reasons, "suite contains stalled playback optimization evidence");
                return;
            }

            if (suite.WeakConfidenceCount > 0 || suite.InsufficientEvidenceCount > 0)
            {
                suite.Action = "collect-comparable-evidence";
                suite.Risk = "high";
                AddUnique(suite.Blockers, "suite.weak-evidence");
                AddUnique(suite.Reasons, "suite contains weak or insufficient comparison evidence");
                return;
            }

            if (suite.PartialConfidenceCount > 0)
            {
                suite.Action = "review-unmatched-signals";
                suite.Risk = "medium";
                AddUnique(suite.Blockers, "suite.partial-evidence");
                AddUnique(suite.Reasons, "suite contains partial comparison evidence");
                return;
            }

            if (suite.ImprovedCount > 0)
            {
                suite.Action = "accept-candidate";
                suite.Risk = "low";
                AddUnique(suite.Reasons, "suite has strong improvement and no blocking comparisons");
                AddImprovementSignals(suite);
                return;
            }

            suite.Action = "continue-next-triage-step";
            suite.Risk = "low";
            AddUnique(suite.Reasons, "suite contains no blocking comparisons and no measured improvement");
        }

        private static bool HasOptimizationAction(
            PlaybackQualityComparisonSuite suite,
            string action)
        {
            foreach (var comparison in suite.Comparisons)
            {
                if (comparison.Optimization.Action == action)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddImprovementSignals(PlaybackQualityComparisonSuite suite)
        {
            foreach (var comparison in suite.Comparisons)
            {
                foreach (var improvement in comparison.Improvements)
                {
                    AddUnique(suite.Signals, improvement.Signal);
                    AddUnique(suite.FailureAreas, improvement.FailureArea);
                }
            }
        }

        private static void ApplyTargetFailureAreas(PlaybackQualityComparisonSuite suite)
        {
            if (suite.Action == "accept-candidate")
            {
                return;
            }

            var target = GetHighestPriorityFailureArea(suite.FailureAreas);
            if (!string.IsNullOrWhiteSpace(target))
            {
                AddUnique(suite.TargetFailureAreas, target);
            }
        }

        private static string GetHighestPriorityFailureArea(List<string> failureAreas)
        {
            var priorityAreas = new[]
            {
                "unsupported-source",
                "color-pipeline",
                "startup",
                "buffering",
                "av-sync",
                "frame-pacing",
                "unknown"
            };

            foreach (var area in priorityAreas)
            {
                if (failureAreas.Contains(area))
                {
                    return area;
                }
            }

            return failureAreas.Count == 0 ? "" : failureAreas[0];
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
