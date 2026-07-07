using System;
using System.Collections.Generic;
using System.Globalization;

namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityRunComparison
    {
        public string CaseId { get; set; } = "";
        public string BaselineRunId { get; set; } = "";
        public string CandidateRunId { get; set; } = "";
        public string Result { get; set; } = "unchanged";
        public string Decision { get; set; } = "no-change";
        public string SuggestedNextAction { get; set; } = "";
        public PlaybackQualityComparabilityAssessment Comparability { get; set; } =
            new PlaybackQualityComparabilityAssessment();
        public PlaybackQualityComparisonConfidence Confidence { get; set; } =
            new PlaybackQualityComparisonConfidence();
        public PlaybackQualityComparisonOptimization Optimization { get; set; } =
            new PlaybackQualityComparisonOptimization();
        public PlaybackQualityComparisonCoverage Coverage { get; set; } =
            new PlaybackQualityComparisonCoverage();
        public PlaybackQualityComparisonEnvironment Environment { get; set; } =
            new PlaybackQualityComparisonEnvironment();
        public List<PlaybackQualitySignalDelta> Improvements { get; } = new List<PlaybackQualitySignalDelta>();
        public List<PlaybackQualitySignalDelta> Regressions { get; } = new List<PlaybackQualitySignalDelta>();
        public List<PlaybackQualitySignalDelta> PolicyChanges { get; } = new List<PlaybackQualitySignalDelta>();
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

    public sealed class PlaybackQualityComparisonConfidence
    {
        public string Level { get; set; } = "weak";
        public List<string> Reasons { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
    }

    public sealed class PlaybackQualityComparisonOptimization
    {
        public string Action { get; set; } = "collect-comparable-evidence";
        public string Risk { get; set; } = "high";
        public List<string> Reasons { get; } = new List<string>();
        public List<string> Blockers { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
        public List<string> FailureAreas { get; } = new List<string>();
    }

    public sealed class PlaybackQualityComparisonContext
    {
        public int StallComparisonCountThreshold { get; set; } = 2;
        public List<PlaybackQualityRunComparison> PreviousComparisons { get; } =
            new List<PlaybackQualityRunComparison>();
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

    public sealed class PlaybackQualityComparisonEnvironment
    {
        public string Status { get; set; } = "missing-evidence";
        public string BaselineCollectorVersion { get; set; } = "";
        public string CandidateCollectorVersion { get; set; } = "";
        public string BaselinePlayerCoreVersion { get; set; } = "";
        public string CandidatePlayerCoreVersion { get; set; } = "";
        public string BaselineSourceRevision { get; set; } = "";
        public string CandidateSourceRevision { get; set; } = "";
        public string BaselineBuildConfiguration { get; set; } = "";
        public string CandidateBuildConfiguration { get; set; } = "";
        public List<string> Signals { get; } = new List<string>();
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
        private const double DerivedSignalEpsilon = 0.0001;

        public static PlaybackQualityRunComparison Compare(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            return Compare(
                baseline,
                candidate,
                new PlaybackQualityComparisonContext());
        }

        public static PlaybackQualityRunComparison Compare(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate,
            PlaybackQualityComparisonContext context)
        {
            if (baseline == null)
            {
                throw new ArgumentNullException(nameof(baseline));
            }

            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var comparison = new PlaybackQualityRunComparison
            {
                BaselineRunId = baseline.RunId,
                CandidateRunId = candidate.RunId,
                Environment = AssessEnvironment(baseline, candidate)
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

                return FinalizeComparison(comparison, context);
            }

            if (baseline.Checks.Count == 0 || candidate.Checks.Count == 0)
            {
                comparison.Result = "insufficient-evidence";
                comparison.Limitations.Add("comparison requires baseline and candidate checks");
                return FinalizeComparison(comparison, context);
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
                return FinalizeComparison(comparison, context);
            }

            AddCandidateOnlyFailures(comparison, candidate, baselineByKey, matchedKeys);
            AddUnmatchedCandidateSignals(comparison, candidate, matchedKeys);
            AddFramePacingSeverityDeltas(comparison, baseline, candidate);

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

            return FinalizeComparison(comparison, context);
        }

        private static PlaybackQualityComparisonEnvironment AssessEnvironment(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            var environment = new PlaybackQualityComparisonEnvironment
            {
                BaselineCollectorVersion = baseline.Environment.CollectorVersion,
                CandidateCollectorVersion = candidate.Environment.CollectorVersion,
                BaselinePlayerCoreVersion = baseline.Environment.PlayerCoreVersion,
                CandidatePlayerCoreVersion = candidate.Environment.PlayerCoreVersion,
                BaselineSourceRevision = baseline.Environment.SourceRevision,
                CandidateSourceRevision = candidate.Environment.SourceRevision,
                BaselineBuildConfiguration = baseline.Environment.BuildConfiguration,
                CandidateBuildConfiguration = candidate.Environment.BuildConfiguration
            };

            AddEnvironmentSignalIfPresent(
                environment,
                "environment.collectorVersion",
                environment.BaselineCollectorVersion,
                environment.CandidateCollectorVersion);
            AddEnvironmentSignalIfPresent(
                environment,
                "environment.playerCoreVersion",
                environment.BaselinePlayerCoreVersion,
                environment.CandidatePlayerCoreVersion);
            AddEnvironmentSignalIfPresent(
                environment,
                "environment.sourceRevision",
                environment.BaselineSourceRevision,
                environment.CandidateSourceRevision);
            AddEnvironmentSignalIfPresent(
                environment,
                "environment.buildConfiguration",
                environment.BaselineBuildConfiguration,
                environment.CandidateBuildConfiguration);

            var baselineHasIdentity = HasEnvironmentIdentity(baseline.Environment);
            var candidateHasIdentity = HasEnvironmentIdentity(candidate.Environment);
            if (!baselineHasIdentity && !candidateHasIdentity)
            {
                environment.Status = "missing-evidence";
                return environment;
            }

            if (!baselineHasIdentity || !candidateHasIdentity)
            {
                environment.Status = "partial";
                return environment;
            }

            environment.Status =
                HasDifferentEnvironmentIdentity(environment)
                    ? "different-build"
                    : "same-build";
            return environment;
        }

        private static bool HasEnvironmentIdentity(PlaybackQualityEnvironment environment)
        {
            return !string.IsNullOrWhiteSpace(environment.SourceRevision) ||
                !string.IsNullOrWhiteSpace(environment.PlayerCoreVersion);
        }

        private static bool HasDifferentEnvironmentIdentity(
            PlaybackQualityComparisonEnvironment environment)
        {
            return IsDifferentNonEmpty(
                    environment.BaselineSourceRevision,
                    environment.CandidateSourceRevision) ||
                IsDifferentNonEmpty(
                    environment.BaselinePlayerCoreVersion,
                    environment.CandidatePlayerCoreVersion) ||
                IsDifferentNonEmpty(
                    environment.BaselineBuildConfiguration,
                    environment.CandidateBuildConfiguration) ||
                IsDifferentNonEmpty(
                    environment.BaselineCollectorVersion,
                    environment.CandidateCollectorVersion);
        }

        private static bool IsDifferentNonEmpty(string baselineValue, string candidateValue)
        {
            return !string.IsNullOrWhiteSpace(baselineValue) &&
                !string.IsNullOrWhiteSpace(candidateValue) &&
                !string.Equals(baselineValue, candidateValue, StringComparison.Ordinal);
        }

        private static void AddEnvironmentSignalIfPresent(
            PlaybackQualityComparisonEnvironment environment,
            string signal,
            string baselineValue,
            string candidateValue)
        {
            if (!string.IsNullOrWhiteSpace(baselineValue) ||
                !string.IsNullOrWhiteSpace(candidateValue))
            {
                AddUnique(environment.Signals, signal);
            }
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

        private static PlaybackQualityRunComparison FinalizeComparison(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityComparisonContext context)
        {
            ApplyConfidence(comparison);
            ApplyDecision(comparison);
            ApplyOptimization(comparison);
            ApplyStallGuard(comparison, context);
            return comparison;
        }

        private static void ApplyConfidence(PlaybackQualityRunComparison comparison)
        {
            if (comparison.Result == "insufficient-evidence")
            {
                comparison.Confidence.Level = "weak";
                if (comparison.Comparability.Status == "incompatible")
                {
                    AddUnique(comparison.Confidence.Reasons, "comparison inputs are incompatible");
                    foreach (var signal in comparison.Comparability.Signals)
                    {
                        AddUnique(comparison.Confidence.Signals, signal);
                    }

                    return;
                }

                if (comparison.Coverage.MatchedCheckCount == 0)
                {
                    AddUnique(comparison.Confidence.Reasons, "comparison has no matched check signals");
                    return;
                }

                AddUnique(comparison.Confidence.Reasons, "comparison result is insufficient evidence");
                return;
            }

            if (comparison.Environment.Status == "same-build")
            {
                comparison.Confidence.Level = "weak";
                AddUnique(comparison.Confidence.Reasons, "candidate build identity matches baseline");
                CopyValues(comparison.Environment.Signals, comparison.Confidence.Signals);
                return;
            }

            if (comparison.Environment.Status == "missing-evidence")
            {
                comparison.Confidence.Level = "weak";
                AddUnique(
                    comparison.Confidence.Reasons,
                    "comparison is missing baseline and candidate build identity");
                AddUnique(comparison.Confidence.Signals, "environment.identity");
                return;
            }

            if (comparison.Environment.Status == "partial")
            {
                comparison.Confidence.Level = "weak";
                AddUnique(
                    comparison.Confidence.Reasons,
                    "comparison is missing complete baseline and candidate build identity");
                AddUnique(comparison.Confidence.Signals, "environment.identity");
                CopyValues(comparison.Environment.Signals, comparison.Confidence.Signals);
                return;
            }

            if (HasUnmatchedEvidence(comparison))
            {
                comparison.Confidence.Level = "partial";
                AddUnique(comparison.Confidence.Reasons, "unmatched comparison signals are present");
                foreach (var signal in comparison.Coverage.UnmatchedBaselineSignals)
                {
                    AddUnique(comparison.Confidence.Signals, signal);
                }

                foreach (var signal in comparison.Coverage.UnmatchedCandidateSignals)
                {
                    AddUnique(comparison.Confidence.Signals, signal);
                }

                if (comparison.Confidence.Signals.Count == 0)
                {
                    AddUnique(comparison.Confidence.Reasons, "not all comparison checks had stable signal keys");
                }

                return;
            }

            comparison.Confidence.Level = "strong";
            AddUnique(comparison.Confidence.Reasons, "all comparison checks matched");
        }

        private static void ApplyOptimization(PlaybackQualityRunComparison comparison)
        {
            if (comparison.Confidence.Level == "weak")
            {
                comparison.Optimization.Action = "collect-comparable-evidence";
                comparison.Optimization.Risk = "high";
                AddUnique(
                    comparison.Optimization.Reasons,
                    "weak comparison confidence blocks playback Core optimization");
                CopyValues(comparison.Confidence.Reasons, comparison.Optimization.Blockers);
                CopyValues(comparison.Confidence.Signals, comparison.Optimization.Signals);
                return;
            }

            if (comparison.Confidence.Level == "partial")
            {
                ApplyPartialConfidenceOptimization(comparison);
                return;
            }

            ApplyStrongConfidenceOptimization(comparison);
        }

        private static void ApplyStallGuard(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityComparisonContext context)
        {
            if (comparison.Result != "unchanged" ||
                comparison.PersistingFailureAreas.Count == 0 ||
                context.StallComparisonCountThreshold <= 1)
            {
                return;
            }

            var unchangedCount = 1;
            for (var index = context.PreviousComparisons.Count - 1; index >= 0; index--)
            {
                var previous = context.PreviousComparisons[index];
                if (previous == null ||
                    previous.Result != "unchanged" ||
                    !SharesFailureArea(previous.PersistingFailureAreas, comparison.PersistingFailureAreas))
                {
                    break;
                }

                unchangedCount++;
                if (unchangedCount >= context.StallComparisonCountThreshold)
                {
                    MarkOptimizationStalled(comparison);
                    return;
                }
            }
        }

        private static bool SharesFailureArea(
            List<string> previousAreas,
            List<string> currentAreas)
        {
            foreach (var area in currentAreas)
            {
                if (previousAreas.Contains(area))
                {
                    return true;
                }
            }

            return false;
        }

        private static void MarkOptimizationStalled(PlaybackQualityRunComparison comparison)
        {
            comparison.Optimization.Action = "change-optimization-strategy";
            comparison.Optimization.Risk = "high";
            AddUnique(
                comparison.Optimization.Reasons,
                "repeated unchanged comparisons indicate optimization stall");
            AddUnique(comparison.Optimization.Blockers, "iteration.stalled");
            CopyValues(comparison.PersistingFailureAreas, comparison.Optimization.FailureAreas);
            CopyValues(comparison.Coverage.MatchedSignals, comparison.Optimization.Signals);
        }

        private static void ApplyPartialConfidenceOptimization(
            PlaybackQualityRunComparison comparison)
        {
            comparison.Optimization.Risk = "medium";
            CopyValues(comparison.Confidence.Signals, comparison.Optimization.Signals);

            switch (comparison.Decision)
            {
                case "reject-candidate":
                    comparison.Optimization.Action = "isolate-candidate-regression";
                    AddUnique(
                        comparison.Optimization.Reasons,
                        "partial comparison evidence found candidate regression");
                    AddRegressionSignals(comparison);
                    break;
                case "split-candidate":
                    comparison.Optimization.Action = "split-candidate";
                    AddUnique(
                        comparison.Optimization.Reasons,
                        "partial comparison evidence found mixed playback quality changes");
                    AddRegressionSignals(comparison);
                    break;
                case "collect-comparable-evidence":
                    comparison.Optimization.Action = "collect-comparable-evidence";
                    comparison.Optimization.Risk = "high";
                    AddUnique(
                        comparison.Optimization.Reasons,
                        "partial comparison evidence is insufficient for playback Core optimization");
                    break;
                default:
                    comparison.Optimization.Action = "review-unmatched-signals";
                    AddUnique(
                        comparison.Optimization.Reasons,
                        "partial comparison evidence requires unmatched signal review");
                    break;
            }
        }

        private static void ApplyStrongConfidenceOptimization(
            PlaybackQualityRunComparison comparison)
        {
            comparison.Optimization.Risk = "low";
            switch (comparison.Decision)
            {
                case "keep-candidate":
                    comparison.Optimization.Action = "accept-candidate";
                    AddUnique(
                        comparison.Optimization.Reasons,
                        "strong comparison evidence supports candidate");
                    break;
                case "reject-candidate":
                    comparison.Optimization.Action = "reject-candidate";
                    AddUnique(
                        comparison.Optimization.Reasons,
                        "strong comparison evidence rejects candidate");
                    AddRegressionSignals(comparison);
                    break;
                case "split-candidate":
                    comparison.Optimization.Action = "split-candidate";
                    comparison.Optimization.Risk = "medium";
                    AddUnique(
                        comparison.Optimization.Reasons,
                        "strong comparison evidence found mixed playback quality changes");
                    AddRegressionSignals(comparison);
                    break;
                case "collect-comparable-evidence":
                    comparison.Optimization.Action = "collect-comparable-evidence";
                    comparison.Optimization.Risk = "high";
                    AddUnique(
                        comparison.Optimization.Reasons,
                        "comparison still requires comparable playback quality evidence");
                    break;
                default:
                    comparison.Optimization.Action = "continue-next-triage-step";
                    AddUnique(
                        comparison.Optimization.Reasons,
                        "strong comparison evidence found no candidate quality change");
                    break;
            }
        }

        private static void AddRegressionSignals(PlaybackQualityRunComparison comparison)
        {
            foreach (var regression in comparison.Regressions)
            {
                AddUnique(comparison.Optimization.Signals, regression.Signal);
            }
        }

        private static void CopyValues(List<string> source, List<string> target)
        {
            foreach (var value in source)
            {
                AddUnique(target, value);
            }
        }

        private static bool HasUnmatchedEvidence(PlaybackQualityRunComparison comparison)
        {
            return comparison.Coverage.UnmatchedBaselineSignals.Count > 0 ||
                comparison.Coverage.UnmatchedCandidateSignals.Count > 0 ||
                comparison.Coverage.MatchedCheckCount < comparison.Coverage.BaselineCheckCount ||
                comparison.Coverage.MatchedCheckCount < comparison.Coverage.CandidateCheckCount;
        }

        private static void ApplyDecision(PlaybackQualityRunComparison comparison)
        {
            if ((comparison.Environment.Status == "same-build" ||
                    comparison.Environment.Status == "missing-evidence" ||
                    comparison.Environment.Status == "partial") &&
                comparison.Result != "insufficient-evidence")
            {
                comparison.Decision = "collect-comparable-evidence";
                comparison.SuggestedNextAction =
                    "Collect reports with distinct baseline and candidate build identity before deciding on playback Core changes.";
                return;
            }

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

        private static void AddFramePacingSeverityDeltas(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            if (!IsFramePacingComparison(comparison))
            {
                return;
            }

            var baselineAnalysis = PlaybackQualityReportAnalyzer.Analyze(baseline);
            var candidateAnalysis = PlaybackQualityReportAnalyzer.Analyze(candidate);

            CompareDerivedLowerIsBetter(
                comparison,
                "framePacing.renderIntervalP95FrameRatio",
                baselineAnalysis.FramePacing.RenderIntervalP95FrameRatio,
                candidateAnalysis.FramePacing.RenderIntervalP95FrameRatio,
                requirePositive: true);
            CompareDerivedLowerIsBetter(
                comparison,
                "framePacing.renderIntervalP99FrameRatio",
                baselineAnalysis.FramePacing.RenderIntervalP99FrameRatio,
                candidateAnalysis.FramePacing.RenderIntervalP99FrameRatio,
                requirePositive: true);
            CompareDerivedLowerIsBetter(
                comparison,
                "framePacing.maxFrameGapFrameRatio",
                baselineAnalysis.FramePacing.MaxFrameGapFrameRatio,
                candidateAnalysis.FramePacing.MaxFrameGapFrameRatio,
                requirePositive: true);
            CompareDerivedPolicyChange(
                comparison,
                "framePacing.lateFrameDropToleranceFrameRatio",
                baselineAnalysis.FramePacing.LateFrameDropToleranceFrameRatio,
                candidateAnalysis.FramePacing.LateFrameDropToleranceFrameRatio,
                requirePositive: true);
            if (HasObservedFrameCount(baseline) && HasObservedFrameCount(candidate))
            {
                CompareDerivedLowerIsBetter(
                    comparison,
                    "framePacing.droppedVideoFramePercent",
                    baselineAnalysis.FramePacing.DroppedVideoFramePercent,
                    candidateAnalysis.FramePacing.DroppedVideoFramePercent,
                    requirePositive: false);
            }
        }

        private static void CompareDerivedPolicyChange(
            PlaybackQualityRunComparison comparison,
            string signal,
            double baselineActual,
            double candidateActual,
            bool requirePositive)
        {
            if (requirePositive &&
                (baselineActual <= 0 || candidateActual <= 0))
            {
                return;
            }

            AddUnique(comparison.Coverage.MatchedSignals, signal);

            var numericDelta = candidateActual - baselineActual;
            if (Math.Abs(numericDelta) <= DerivedSignalEpsilon)
            {
                return;
            }

            comparison.PolicyChanges.Add(CreateDelta(
                CreateDerivedCheck(signal, baselineActual),
                CreateDerivedCheck(signal, candidateActual),
                numericDelta < 0 ? "decreased" : "increased",
                numericDelta));
        }

        private static bool IsFramePacingComparison(
            PlaybackQualityRunComparison comparison)
        {
            return comparison.PersistingFailureAreas.Contains("frame-pacing") ||
                comparison.ResolvedFailureAreas.Contains("frame-pacing") ||
                comparison.NewFailureAreas.Contains("frame-pacing");
        }

        private static bool HasObservedFrameCount(PlaybackQualityReport report)
        {
            return report.Timing.RenderedVideoFrames > 0 ||
                report.Timing.DroppedVideoFrames > 0;
        }

        private static void CompareDerivedLowerIsBetter(
            PlaybackQualityRunComparison comparison,
            string signal,
            double baselineActual,
            double candidateActual,
            bool requirePositive)
        {
            if (requirePositive &&
                (baselineActual <= 0 || candidateActual <= 0))
            {
                return;
            }

            AddUnique(comparison.Coverage.MatchedSignals, signal);

            var numericDelta = candidateActual - baselineActual;
            if (Math.Abs(numericDelta) <= DerivedSignalEpsilon)
            {
                return;
            }

            var baselineCheck = CreateDerivedCheck(signal, baselineActual);
            var candidateCheck = CreateDerivedCheck(signal, candidateActual);
            if (numericDelta < 0)
            {
                comparison.Improvements.Add(CreateDelta(
                    baselineCheck,
                    candidateCheck,
                    "decreased",
                    numericDelta));
            }
            else
            {
                comparison.Regressions.Add(CreateDelta(
                    baselineCheck,
                    candidateCheck,
                    "increased",
                    numericDelta));
            }
        }

        private static PlaybackQualityCheck CreateDerivedCheck(
            string signal,
            double actual)
        {
            return new PlaybackQualityCheck
            {
                Name = signal,
                Signal = signal,
                FailureArea = "frame-pacing",
                Status = "derived",
                Actual = actual.ToString("0.######", CultureInfo.InvariantCulture)
            };
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
