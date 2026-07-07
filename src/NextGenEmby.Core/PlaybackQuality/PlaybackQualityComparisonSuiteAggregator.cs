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
        public int PolicyChangeCount { get; set; }
        public int StrongConfidenceCount { get; set; }
        public int PartialConfidenceCount { get; set; }
        public int WeakConfidenceCount { get; set; }
        public string Action { get; set; } = "collect-comparable-evidence";
        public string Risk { get; set; } = "high";
        public PlaybackQualityComparisonSuiteEnvironment Environment { get; set; } =
            new PlaybackQualityComparisonSuiteEnvironment();
        public List<string> Reasons { get; } = new List<string>();
        public List<string> Blockers { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
        public List<PlaybackQualitySuiteSignalSummary> SignalSummaries { get; } =
            new List<PlaybackQualitySuiteSignalSummary>();
        public List<string> FailureAreas { get; } = new List<string>();
        public List<string> TargetFailureAreas { get; } = new List<string>();
        public List<string> TargetCaseIds { get; } = new List<string>();
        public List<string> CodeTargets { get; } = new List<string>();
        public List<PlaybackQualitySuiteNextAction> NextActions { get; } =
            new List<PlaybackQualitySuiteNextAction>();
        public List<PlaybackQualityComparisonCaseSummary> Cases { get; } =
            new List<PlaybackQualityComparisonCaseSummary>();
        public List<PlaybackQualityRunComparison> Comparisons { get; } =
            new List<PlaybackQualityRunComparison>();
    }

    public sealed class PlaybackQualityComparisonSuiteEnvironment
    {
        public int MissingEvidenceCount { get; set; }
        public int PartialCount { get; set; }
        public int SameBuildCount { get; set; }
        public int DifferentBuildCount { get; set; }
        public List<string> MissingEvidenceCaseIds { get; } = new List<string>();
        public List<string> PartialCaseIds { get; } = new List<string>();
        public List<string> SameBuildCaseIds { get; } = new List<string>();
        public List<string> DifferentBuildCaseIds { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
    }

    public sealed class PlaybackQualitySuiteNextAction
    {
        public int Rank { get; set; }
        public string Action { get; set; } = "";
        public string Risk { get; set; } = "";
        public string FailureArea { get; set; } = "";
        public List<string> CaseIds { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
        public List<string> Reasons { get; } = new List<string>();
        public List<string> Blockers { get; } = new List<string>();
        public List<string> CodeTargets { get; } = new List<string>();
    }

    public sealed class PlaybackQualitySuiteSignalSummary
    {
        public string Signal { get; set; } = "";
        public string FailureArea { get; set; } = "";
        public string Outcome { get; set; } = "unchanged";
        public int ImprovementCount { get; set; }
        public int RegressionCount { get; set; }
        public int PolicyChangeCount { get; set; }
        public List<string> CaseIds { get; } = new List<string>();
        public List<string> Directions { get; } = new List<string>();
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
        public string EnvironmentStatus { get; set; } = "";
        public string SuggestedNextAction { get; set; } = "";
        public int PolicyChangeCount { get; set; }
        public List<string> Reasons { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
        public List<string> EnvironmentSignals { get; } = new List<string>();
        public List<string> FailureAreas { get; } = new List<string>();
        public List<string> Blockers { get; } = new List<string>();
        public List<string> CodeTargets { get; } = new List<string>();
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
                AddEnvironmentEvidence(suite, comparison);
                AddComparisonEvidence(suite, comparison);
                AddSignalSummaries(suite, comparison);
            }

            suite.TotalComparisonCount = suite.Comparisons.Count;
            ApplySuiteAction(suite);
            ApplyTargetFailureAreas(suite);
            AddNextActions(suite);
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
                Confidence = comparison.Confidence.Level,
                EnvironmentStatus = comparison.Environment.Status,
                SuggestedNextAction = comparison.SuggestedNextAction
            };

            foreach (var reason in comparison.Optimization.Reasons)
            {
                AddUnique(summary.Reasons, reason);
            }

            foreach (var reason in comparison.Confidence.Reasons)
            {
                AddUnique(summary.Reasons, reason);
            }

            foreach (var reason in comparison.Comparability.Reasons)
            {
                AddUnique(summary.Reasons, reason);
            }

            foreach (var signal in comparison.Optimization.Signals)
            {
                AddUnique(summary.Signals, signal);
            }

            foreach (var signal in comparison.Confidence.Signals)
            {
                AddUnique(summary.Signals, signal);
            }

            foreach (var signal in comparison.Environment.Signals)
            {
                AddUnique(summary.EnvironmentSignals, signal);
                AddUnique(summary.Signals, signal);
            }

            if (comparison.Environment.Status == "missing-evidence" ||
                comparison.Environment.Status == "partial")
            {
                AddUnique(summary.EnvironmentSignals, "environment.identity");
                AddUnique(summary.Signals, "environment.identity");
                AddUnique(summary.Blockers, "environment.evidence-missing");
                AddUnique(
                    summary.Reasons,
                    "comparison is missing complete baseline and candidate build identity");
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

            foreach (var policyChange in comparison.PolicyChanges)
            {
                summary.PolicyChangeCount++;
                AddUnique(summary.Signals, policyChange.Signal);
                AddUnique(summary.FailureAreas, policyChange.FailureArea);
                AddUnique(
                    summary.Reasons,
                    "candidate changed Core policy signal without quality delta");
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

            AddCodeTargetsForFailureAreas(summary.CodeTargets, summary.FailureAreas);
            if (RequiresEvidenceCollection(summary))
            {
                AddCodeTargetsForFailureArea(summary.CodeTargets, "evidence-collection");
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

            suite.PolicyChangeCount += comparison.PolicyChanges.Count;
        }

        private static void AddEnvironmentEvidence(
            PlaybackQualityComparisonSuite suite,
            PlaybackQualityRunComparison comparison)
        {
            AddUnique(suite.Environment.Signals, "environment.identity");
            AddUnique(suite.Signals, "environment.identity");

            foreach (var signal in comparison.Environment.Signals)
            {
                AddUnique(suite.Environment.Signals, signal);
            }

            switch (comparison.Environment.Status)
            {
                case "different-build":
                    suite.Environment.DifferentBuildCount++;
                    AddUnique(suite.Environment.DifferentBuildCaseIds, comparison.CaseId);
                    break;
                case "same-build":
                    suite.Environment.SameBuildCount++;
                    AddUnique(suite.Environment.SameBuildCaseIds, comparison.CaseId);
                    AddSameBuildEnvironmentBlocker(suite);
                    break;
                case "partial":
                    suite.Environment.PartialCount++;
                    AddUnique(suite.Environment.PartialCaseIds, comparison.CaseId);
                    AddEnvironmentEvidenceBlocker(suite);
                    break;
                default:
                    suite.Environment.MissingEvidenceCount++;
                    AddUnique(suite.Environment.MissingEvidenceCaseIds, comparison.CaseId);
                    AddEnvironmentEvidenceBlocker(suite);
                    break;
            }
        }

        private static void AddEnvironmentEvidenceBlocker(
            PlaybackQualityComparisonSuite suite)
        {
            AddUnique(suite.Blockers, "suite.environment-evidence-missing");
            AddUnique(
                suite.Reasons,
                "suite contains comparisons without complete baseline and candidate build identity");
        }

        private static void AddSameBuildEnvironmentBlocker(
            PlaybackQualityComparisonSuite suite)
        {
            AddUnique(suite.Blockers, "suite.environment-same-build");
            AddUnique(
                suite.Reasons,
                "suite contains comparisons where candidate build identity matches baseline");
        }

        private static void AddComparisonEvidence(
            PlaybackQualityComparisonSuite suite,
            PlaybackQualityRunComparison comparison)
        {
            foreach (var blocker in comparison.Optimization.Blockers)
            {
                if (blocker.StartsWith("comparison.", StringComparison.Ordinal))
                {
                    AddUnique(suite.Blockers, blocker);
                }
            }

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

            foreach (var policyChange in comparison.PolicyChanges)
            {
                AddUnique(suite.Signals, policyChange.Signal);
                AddUnique(suite.FailureAreas, policyChange.FailureArea);
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

        private static void AddSignalSummaries(
            PlaybackQualityComparisonSuite suite,
            PlaybackQualityRunComparison comparison)
        {
            foreach (var improvement in comparison.Improvements)
            {
                AddSignalSummaryDelta(suite, comparison, improvement, isImprovement: true);
            }

            foreach (var regression in comparison.Regressions)
            {
                AddSignalSummaryDelta(suite, comparison, regression, isImprovement: false);
            }

            foreach (var policyChange in comparison.PolicyChanges)
            {
                AddSignalSummaryPolicyChange(suite, comparison, policyChange);
            }
        }

        private static void AddSignalSummaryDelta(
            PlaybackQualityComparisonSuite suite,
            PlaybackQualityRunComparison comparison,
            PlaybackQualitySignalDelta delta,
            bool isImprovement)
        {
            if (string.IsNullOrWhiteSpace(delta.Signal))
            {
                return;
            }

            var summary = FindSignalSummary(
                suite.SignalSummaries,
                delta.Signal,
                delta.FailureArea);
            if (summary == null)
            {
                summary = new PlaybackQualitySuiteSignalSummary
                {
                    Signal = delta.Signal,
                    FailureArea = delta.FailureArea
                };
                suite.SignalSummaries.Add(summary);
            }

            if (isImprovement)
            {
                summary.ImprovementCount++;
            }
            else
            {
                summary.RegressionCount++;
            }

            AddUnique(summary.CaseIds, comparison.CaseId);
            AddUnique(summary.Directions, delta.Direction);
            UpdateSignalSummaryOutcome(summary);
        }

        private static void AddSignalSummaryPolicyChange(
            PlaybackQualityComparisonSuite suite,
            PlaybackQualityRunComparison comparison,
            PlaybackQualitySignalDelta delta)
        {
            if (string.IsNullOrWhiteSpace(delta.Signal))
            {
                return;
            }

            var summary = FindSignalSummary(
                suite.SignalSummaries,
                delta.Signal,
                delta.FailureArea);
            if (summary == null)
            {
                summary = new PlaybackQualitySuiteSignalSummary
                {
                    Signal = delta.Signal,
                    FailureArea = delta.FailureArea
                };
                suite.SignalSummaries.Add(summary);
            }

            summary.PolicyChangeCount++;
            AddUnique(summary.CaseIds, comparison.CaseId);
            AddUnique(summary.Directions, delta.Direction);
            UpdateSignalSummaryOutcome(summary);
        }

        private static PlaybackQualitySuiteSignalSummary? FindSignalSummary(
            List<PlaybackQualitySuiteSignalSummary> summaries,
            string signal,
            string failureArea)
        {
            foreach (var summary in summaries)
            {
                if (summary.Signal == signal && summary.FailureArea == failureArea)
                {
                    return summary;
                }
            }

            return null;
        }

        private static void UpdateSignalSummaryOutcome(
            PlaybackQualitySuiteSignalSummary summary)
        {
            if (summary.ImprovementCount > 0 && summary.RegressionCount > 0)
            {
                summary.Outcome = "mixed";
            }
            else if (summary.ImprovementCount > 0)
            {
                summary.Outcome = "improved";
            }
            else if (summary.RegressionCount > 0)
            {
                summary.Outcome = "regressed";
            }
            else if (summary.PolicyChangeCount > 0)
            {
                summary.Outcome = "policy-changed";
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

            if (suite.Environment.MissingEvidenceCount > 0 ||
                suite.Environment.PartialCount > 0)
            {
                suite.Action = "collect-comparable-evidence";
                suite.Risk = "high";
                AddUnique(suite.Blockers, "suite.environment-evidence-missing");
                AddUnique(
                    suite.Reasons,
                    "suite contains comparisons without complete baseline and candidate build identity");
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
                AddCodeTargetsForFailureArea(suite.CodeTargets, target);
                foreach (var summary in suite.Cases)
                {
                    if (summary.FailureAreas.Contains(target))
                    {
                        AddUnique(suite.TargetCaseIds, summary.CaseId);
                    }
                }
            }

            if (suite.TargetCaseIds.Count == 0)
            {
                AddEvidenceTargetCaseIds(suite);
            }

            if (RequiresEvidenceCollection(suite))
            {
                AddCodeTargetsForFailureArea(suite.CodeTargets, "evidence-collection");
            }
        }

        private static void AddNextActions(PlaybackQualityComparisonSuite suite)
        {
            var action = new PlaybackQualitySuiteNextAction
            {
                Rank = 1,
                Action = suite.Action,
                Risk = suite.Risk,
                FailureArea = suite.TargetFailureAreas.Count == 0
                    ? ""
                    : suite.TargetFailureAreas[0]
            };

            CopyValues(suite.TargetCaseIds, action.CaseIds);
            if (action.CaseIds.Count == 0)
            {
                AddCaseIdsForSuiteAction(suite, action);
            }

            CopyValues(suite.Signals, action.Signals);
            CopyValues(suite.Reasons, action.Reasons);
            CopyValues(suite.Blockers, action.Blockers);
            CopyValues(suite.CodeTargets, action.CodeTargets);
            suite.NextActions.Add(action);
        }

        private static void AddCaseIdsForSuiteAction(
            PlaybackQualityComparisonSuite suite,
            PlaybackQualitySuiteNextAction action)
        {
            foreach (var summary in suite.Cases)
            {
                if (summary.Action == suite.Action ||
                    (summary.Result == "improved" && suite.Action == "accept-candidate") ||
                    (summary.Result == "regressed" && suite.Action == "reject-candidate") ||
                    (summary.Result == "mixed" && suite.Action == "split-candidate"))
                {
                    AddUnique(action.CaseIds, summary.CaseId);
                }
            }
        }

        private static void AddEvidenceTargetCaseIds(PlaybackQualityComparisonSuite suite)
        {
            foreach (var summary in suite.Cases)
            {
                if (summary.Result == "insufficient-evidence" ||
                    summary.Confidence == "weak" ||
                    summary.Action == "collect-comparable-evidence" ||
                    summary.Action == "review-unmatched-signals" ||
                    summary.Blockers.Count > 0)
                {
                    AddUnique(suite.TargetCaseIds, summary.CaseId);
                }
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

        private static bool RequiresEvidenceCollection(PlaybackQualityComparisonSuite suite)
        {
            return suite.Action == "collect-comparable-evidence" ||
                suite.Action == "review-unmatched-signals" ||
                suite.Blockers.Contains("suite.weak-evidence") ||
                suite.Blockers.Contains("suite.partial-evidence");
        }

        private static bool RequiresEvidenceCollection(PlaybackQualityComparisonCaseSummary summary)
        {
            return summary.Action == "collect-comparable-evidence" ||
                summary.Action == "review-unmatched-signals" ||
                summary.Confidence == "weak" ||
                summary.Blockers.Count > 0;
        }

        private static void AddCodeTargetsForFailureAreas(
            List<string> codeTargets,
            List<string> failureAreas)
        {
            foreach (var area in failureAreas)
            {
                AddCodeTargetsForFailureArea(codeTargets, area);
            }
        }

        private static void AddCodeTargetsForFailureArea(List<string> codeTargets, string area)
        {
            PlaybackQualityCodeTargetCatalog.AddForFailureArea(codeTargets, area);
        }

        private static void CopyValues(List<string> source, List<string> target)
        {
            foreach (var value in source)
            {
                AddUnique(target, value);
            }
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
