using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityRunComparison
    {
        public int SchemaVersion { get; set; } = 1;
        public string EvaluationVersion { get; set; } =
            PlaybackQualityRunResult.CurrentEvaluationVersion;
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
        public List<string> CodeTargets { get; } = new List<string>();
        public List<PlaybackQualitySuiteNextAction> NextActions { get; } =
            new List<PlaybackQualitySuiteNextAction>();
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
        private const double MinimumFramePacingExpectedErrorDeltaMs = 2.0;
        private const double MinimumAudioAheadWaitOversleepDeltaMs = 2.0;
        private const double MinimumAcceptableFrameRatio = 0.75;
        private const double MaximumAcceptableFrameRatio = 1.5;

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
            return Compare(
                baseline,
                candidate,
                baselinePresentSignals: null,
                candidatePresentSignals: null,
                context);
        }

        public static PlaybackQualityRunComparison Compare(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate,
            IEnumerable<string>? baselinePresentSignals,
            IEnumerable<string>? candidatePresentSignals,
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
            var hasBaselineSignalPresence = baselinePresentSignals != null;
            var hasCandidateSignalPresence = candidatePresentSignals != null;
            var baselineSignals = CreateComparisonSignalSet(baseline, baselinePresentSignals);
            var candidateSignals = CreateComparisonSignalSet(candidate, candidatePresentSignals);
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

            var candidateByKey = CreateCheckMap(
                candidate,
                candidateSignals,
                hasCandidateSignalPresence);
            var baselineByKey = CreateCheckMap(
                baseline,
                baselineSignals,
                hasBaselineSignalPresence);
            var matchedKeys = new List<string>();
            var matchedChecks = 0;
            foreach (var baselineCheck in baseline.Checks)
            {
                if (!IsCheckSupportedBySignalPresence(
                    baselineCheck,
                    baselineSignals,
                    hasBaselineSignalPresence))
                {
                    continue;
                }

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

            AddUnmatchedBaselineSignals(
                comparison,
                baseline,
                matchedKeys,
                baselineSignals,
                hasBaselineSignalPresence);

            if (matchedChecks == 0)
            {
                comparison.Result = "insufficient-evidence";
                comparison.Limitations.Add("comparison requires at least one matching check signal");
                return FinalizeComparison(comparison, context);
            }

            AddCandidateOnlyFailures(
                comparison,
                candidate,
                baselineByKey,
                matchedKeys,
                baselineSignals,
                candidateSignals,
                hasBaselineSignalPresence,
                hasCandidateSignalPresence);
            AddUnmatchedCandidateSignals(
                comparison,
                candidate,
                matchedKeys,
                candidateSignals,
                hasCandidateSignalPresence);
            AddFramePacingSeverityDeltas(
                comparison,
                baseline,
                candidate,
                baselineSignals,
                candidateSignals,
                hasBaselineSignalPresence,
                hasCandidateSignalPresence);
            AddSeekTimelineEvidenceDeltas(comparison, baseline, candidate);
            AddRuntimePlaybackEvidenceSignals(
                comparison,
                baseline,
                candidate,
                baselineSignals,
                candidateSignals);
            AddAudioAheadWaitOversleepDeltas(comparison, baseline, candidate);
            AddTrackAndSubtitleEvidenceDeltas(comparison, baseline, candidate);
            AddInteractionScenarioOutcomeDelta(comparison, baseline, candidate);

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
            List<string> matchedKeys,
            HashSet<string> baselineSignals,
            bool hasBaselineSignalPresence)
        {
            foreach (var baselineCheck in baseline.Checks)
            {
                if (!IsCheckSupportedBySignalPresence(
                    baselineCheck,
                    baselineSignals,
                    hasBaselineSignalPresence))
                {
                    continue;
                }

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
            List<string> matchedKeys,
            HashSet<string> candidateSignals,
            bool hasCandidateSignalPresence)
        {
            foreach (var candidateCheck in candidate.Checks)
            {
                if (!IsCheckSupportedBySignalPresence(
                    candidateCheck,
                    candidateSignals,
                    hasCandidateSignalPresence))
                {
                    continue;
                }

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
            ValidateExecutionEvidence(assessment, baseline);
            ValidateExecutionEvidence(assessment, candidate);

            var baselineExecution = baseline.Execution ?? new PlaybackQualityExecutionEvidence();
            var candidateExecution = candidate.Execution ?? new PlaybackQualityExecutionEvidence();
            if (!string.Equals(
                    baselineExecution.EvidenceLevel,
                    candidateExecution.EvidenceLevel,
                    StringComparison.Ordinal))
            {
                AddIncompatibility(assessment, "execution.evidenceLevel");
            }

            if (!string.Equals(
                    baselineExecution.Runner,
                    candidateExecution.Runner,
                    StringComparison.Ordinal))
            {
                AddIncompatibility(assessment, "execution.runner");
            }

            if (!string.Equals(
                    baselineExecution.Scenario,
                    candidateExecution.Scenario,
                    StringComparison.Ordinal))
            {
                AddIncompatibility(assessment, "execution.scenario");
            }

            if (!string.Equals(
                    baselineExecution.SourceLocatorHash,
                    candidateExecution.SourceLocatorHash,
                    StringComparison.Ordinal))
            {
                AddIncompatibility(assessment, "execution.sourceLocatorHash");
            }

            if (baselineExecution.SourceOpened &&
                candidateExecution.SourceOpened &&
                !string.Equals(
                    baselineExecution.OpenedSourceHashKind,
                    candidateExecution.OpenedSourceHashKind,
                    StringComparison.Ordinal))
            {
                AddIncompatibility(assessment, "execution.openedSourceHashKind");
            }

            if (baselineExecution.SourceOpened &&
                candidateExecution.SourceOpened &&
                !string.Equals(
                    baselineExecution.OpenedSourceHash,
                    candidateExecution.OpenedSourceHash,
                    StringComparison.Ordinal))
            {
                AddIncompatibility(assessment, "execution.openedSourceHash");
            }

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
                "colorPipeline.expectationProfile",
                baseline.ColorPipeline.ExpectationProfile,
                candidate.ColorPipeline.ExpectationProfile);
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

        private static void ValidateExecutionEvidence(
            PlaybackQualityComparabilityAssessment assessment,
            PlaybackQualityReport report)
        {
            var execution = report.Execution ?? new PlaybackQualityExecutionEvidence();
            if (!PlaybackQualityEvidenceLevel.IsKnown(execution.EvidenceLevel) ||
                !PlaybackQualityEvidenceLevel.MeetsMinimum(
                    execution.EvidenceLevel,
                    PlaybackQualityEvidenceLevel.NativePlayback))
            {
                AddIncompatibility(assessment, "execution.evidenceLevel");
            }

            if (string.IsNullOrWhiteSpace(execution.AttemptId))
            {
                AddIncompatibility(assessment, "execution.attemptId");
            }

            if (string.IsNullOrWhiteSpace(execution.Runner))
            {
                AddIncompatibility(assessment, "execution.runner");
            }

            if (!PlaybackQualityExecutionScenario.IsKnown(execution.Scenario))
            {
                AddIncompatibility(assessment, "execution.scenario");
            }

            if (!PlaybackQualityExecutionStatus.IsKnown(execution.Status))
            {
                AddIncompatibility(assessment, "execution.status");
            }

            if (!IsSha256Fingerprint(execution.SourceLocatorHash))
            {
                AddIncompatibility(assessment, "execution.sourceLocatorHash");
            }

            if (!DateTimeOffset.TryParse(
                    execution.StartedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var startedAt) ||
                startedAt.Offset != TimeSpan.Zero)
            {
                AddIncompatibility(assessment, "execution.startedAtUtc");
            }

            if (!double.IsFinite(execution.DurationMs) || execution.DurationMs < 0)
            {
                AddIncompatibility(assessment, "execution.durationMs");
            }

            if (!execution.SourceOpenAttempted)
            {
                AddIncompatibility(assessment, "execution.sourceOpenAttempted");
            }

            if (execution.SourceOpened && !IsSha256Fingerprint(execution.OpenedSourceHash))
            {
                AddIncompatibility(assessment, "execution.openedSourceHash");
            }

            if (execution.SourceOpened &&
                !string.Equals(
                    execution.OpenedSourceHashKind,
                    PlaybackQualitySourceFingerprint.OpenedMediaSignatureKind,
                    StringComparison.Ordinal))
            {
                AddIncompatibility(assessment, "execution.openedSourceHashKind");
            }

            ValidateExecutionStatus(assessment, report.Result, execution.Status);
            if (report.Result == PlaybackQualityReportResult.Pass ||
                report.Result == PlaybackQualityReportResult.Fail)
            {
                RequireExecutionStage(assessment, execution.SourceOpened, "execution.sourceOpened");
                RequireExecutionStage(assessment, execution.NativeGraphOpened, "execution.nativeGraphOpened");
                RequireExecutionStage(assessment, execution.DemuxStarted, "execution.demuxStarted");
                RequireExecutionStage(assessment, execution.DecoderOpened, "execution.decoderOpened");
                RequireExecutionStage(assessment, execution.PlaybackSampleObserved, "execution.playbackSampleObserved");
            }
        }

        private static void ValidateExecutionStatus(
            PlaybackQualityComparabilityAssessment assessment,
            string result,
            string status)
        {
            var matches = result switch
            {
                PlaybackQualityReportResult.Pass or PlaybackQualityReportResult.Fail =>
                    status == PlaybackQualityExecutionStatus.Completed,
                PlaybackQualityReportResult.Error =>
                    status == PlaybackQualityExecutionStatus.Failed ||
                    status == PlaybackQualityExecutionStatus.TimedOut ||
                    status == PlaybackQualityExecutionStatus.Cancelled,
                PlaybackQualityReportResult.Unsupported =>
                    status == PlaybackQualityExecutionStatus.Unsupported,
                _ => false
            };
            if (!matches)
            {
                AddIncompatibility(assessment, "execution.status");
            }
        }

        private static void RequireExecutionStage(
            PlaybackQualityComparabilityAssessment assessment,
            bool observed,
            string signal)
        {
            if (!observed)
            {
                AddIncompatibility(assessment, signal);
            }
        }

        private static bool IsSha256Fingerprint(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length != 71 ||
                !value.StartsWith("sha256:", StringComparison.Ordinal))
            {
                return false;
            }

            for (var index = 7; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') ||
                    (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
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
            ApplyComparisonCodeTargets(comparison);
            AddComparisonNextAction(comparison);
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
                AddComparabilityBlocker(comparison);
                AddEnvironmentIdentityBlocker(comparison);
                AddCoverageBlocker(comparison);
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

        private static void AddComparabilityBlocker(
            PlaybackQualityRunComparison comparison)
        {
            if (comparison.Comparability.Status == "incompatible")
            {
                AddUnique(comparison.Optimization.Blockers, "comparison.incompatible-inputs");
            }
        }

        private static void AddCoverageBlocker(
            PlaybackQualityRunComparison comparison)
        {
            if (comparison.Coverage.BaselineCheckCount == 0 ||
                comparison.Coverage.CandidateCheckCount == 0)
            {
                AddUnique(comparison.Optimization.Blockers, "comparison.missing-checks");
                return;
            }

            if (comparison.Comparability.Status != "incompatible" &&
                comparison.Coverage.MatchedCheckCount == 0)
            {
                AddUnique(comparison.Optimization.Blockers, "comparison.no-matched-signals");
            }
        }

        private static void AddEnvironmentIdentityBlocker(
            PlaybackQualityRunComparison comparison)
        {
            if (comparison.Environment.Status == "same-build")
            {
                AddUnique(comparison.Optimization.Blockers, "comparison.environment-same-build");
                return;
            }

            if (comparison.Environment.Status == "missing-evidence" ||
                comparison.Environment.Status == "partial")
            {
                AddUnique(comparison.Optimization.Blockers, "comparison.environment-evidence-missing");
            }
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
            comparison.SuggestedNextAction =
                "Change optimization strategy for the stalled playback Core failure area before making more similar candidate edits.";
            AddUnique(
                comparison.Optimization.Reasons,
                "repeated unchanged comparisons indicate optimization stall");
            AddUnique(comparison.Optimization.Blockers, "iteration.stalled");
            CopyValues(comparison.PersistingFailureAreas, comparison.Optimization.FailureAreas);
            CopyValues(comparison.Coverage.MatchedSignals, comparison.Optimization.Signals);
        }

        private static void ApplyComparisonCodeTargets(PlaybackQualityRunComparison comparison)
        {
            PlaybackQualityCodeTargetCatalog.AddForFailureAreas(
                comparison.CodeTargets,
                comparison.Optimization.FailureAreas);
            PlaybackQualityCodeTargetCatalog.AddForFailureAreas(
                comparison.CodeTargets,
                comparison.NewFailureAreas);
            PlaybackQualityCodeTargetCatalog.AddForFailureAreas(
                comparison.CodeTargets,
                comparison.PersistingFailureAreas);
            PlaybackQualityCodeTargetCatalog.AddForFailureAreas(
                comparison.CodeTargets,
                comparison.ResolvedFailureAreas);
            AddDeltaCodeTargets(comparison, comparison.Improvements);
            AddDeltaCodeTargets(comparison, comparison.Regressions);
            AddDeltaCodeTargets(comparison, comparison.PolicyChanges);
            PlaybackQualityCodeTargetCatalog.AddForSignals(
                comparison.CodeTargets,
                comparison.Optimization.Signals);
            PlaybackQualityCodeTargetCatalog.AddForSignals(
                comparison.CodeTargets,
                comparison.Confidence.Signals);
            PlaybackQualityCodeTargetCatalog.AddForSignals(
                comparison.CodeTargets,
                comparison.Coverage.MatchedSignals);
            PlaybackQualityCodeTargetCatalog.AddForSignals(
                comparison.CodeTargets,
                comparison.Coverage.UnmatchedBaselineSignals);
            PlaybackQualityCodeTargetCatalog.AddForSignals(
                comparison.CodeTargets,
                comparison.Coverage.UnmatchedCandidateSignals);

            if (comparison.CodeTargets.Count == 0 &&
                comparison.Optimization.Action == "collect-comparable-evidence")
            {
                PlaybackQualityCodeTargetCatalog.AddForFailureArea(
                    comparison.CodeTargets,
                    "evidence-collection");
            }
        }

        private static void AddDeltaCodeTargets(
            PlaybackQualityRunComparison comparison,
            List<PlaybackQualitySignalDelta> deltas)
        {
            foreach (var delta in deltas)
            {
                PlaybackQualityCodeTargetCatalog.AddForFailureArea(
                    comparison.CodeTargets,
                    delta.FailureArea);
                PlaybackQualityCodeTargetCatalog.AddForSignal(
                    comparison.CodeTargets,
                    delta.Signal);
            }
        }

        private static void AddComparisonNextAction(PlaybackQualityRunComparison comparison)
        {
            var action = new PlaybackQualitySuiteNextAction
            {
                Rank = 1,
                Action = comparison.Optimization.Action,
                Risk = comparison.Optimization.Risk,
                FailureArea = GetComparisonTargetFailureArea(comparison)
            };

            AddUnique(
                action.CaseIds,
                string.IsNullOrWhiteSpace(comparison.CaseId)
                    ? string.IsNullOrWhiteSpace(comparison.CandidateRunId)
                        ? comparison.BaselineRunId
                        : comparison.CandidateRunId
                    : comparison.CaseId);
            CopyValues(comparison.Optimization.Signals, action.Signals);
            AddDeltaSignals(action.Signals, comparison.Improvements);
            AddDeltaSignals(action.Signals, comparison.Regressions);
            AddDeltaSignals(action.Signals, comparison.PolicyChanges);
            CopyValues(comparison.Optimization.Blockers, action.Blockers);
            CopyValues(comparison.Optimization.Reasons, action.Reasons);
            AddUnique(action.Reasons, comparison.SuggestedNextAction);
            CopyValues(comparison.CodeTargets, action.CodeTargets);
            comparison.NextActions.Add(action);
        }

        private static void AddDeltaSignals(
            List<string> signals,
            List<PlaybackQualitySignalDelta> deltas)
        {
            foreach (var delta in deltas)
            {
                AddUnique(signals, delta.Signal);
            }
        }

        private static string GetComparisonTargetFailureArea(
            PlaybackQualityRunComparison comparison)
        {
            var priorityAreas = new[]
            {
                "unsupported-source",
                "color-pipeline",
                "startup",
                "buffering",
                "timeline",
                "av-sync",
                "frame-pacing",
                "unknown"
            };

            foreach (var area in priorityAreas)
            {
                if (comparison.Optimization.FailureAreas.Contains(area) ||
                    comparison.NewFailureAreas.Contains(area) ||
                    comparison.PersistingFailureAreas.Contains(area) ||
                    comparison.ResolvedFailureAreas.Contains(area) ||
                    HasDeltaFailureArea(comparison.Improvements, area) ||
                    HasDeltaFailureArea(comparison.Regressions, area) ||
                    HasDeltaFailureArea(comparison.PolicyChanges, area))
                {
                    return area;
                }
            }

            return "";
        }

        private static bool HasDeltaFailureArea(
            List<PlaybackQualitySignalDelta> deltas,
            string failureArea)
        {
            foreach (var delta in deltas)
            {
                if (delta.FailureArea == failureArea)
                {
                    return true;
                }
            }

            return false;
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
            List<string> matchedKeys,
            HashSet<string> baselineSignals,
            HashSet<string> candidateSignals,
            bool hasBaselineSignalPresence,
            bool hasCandidateSignalPresence)
        {
            foreach (var candidateCheck in candidate.Checks)
            {
                if (!IsCheckSupportedBySignalPresence(
                    candidateCheck,
                    candidateSignals,
                    hasCandidateSignalPresence))
                {
                    continue;
                }

                var key = GetCheckKey(candidateCheck);
                if (string.IsNullOrWhiteSpace(key) ||
                    baselineByKey.ContainsKey(key) ||
                    matchedKeys.Contains(key) ||
                    candidateCheck.Status != "fail" ||
                    (hasBaselineSignalPresence &&
                        PlaybackQualitySignalCatalog.IsReportSignal(key) &&
                        !baselineSignals.Contains(key)))
                {
                    continue;
                }

                comparison.Regressions.Add(CreateCandidateOnlyDelta(candidateCheck));
                AddUnique(comparison.NewFailureAreas, candidateCheck.FailureArea);
            }
        }

        private static Dictionary<string, PlaybackQualityCheck> CreateCheckMap(
            PlaybackQualityReport report,
            HashSet<string> presentSignals,
            bool hasSignalPresence)
        {
            var map = new Dictionary<string, PlaybackQualityCheck>();
            foreach (var check in report.Checks)
            {
                if (!IsCheckSupportedBySignalPresence(
                    check,
                    presentSignals,
                    hasSignalPresence))
                {
                    continue;
                }

                var key = GetCheckKey(check);
                if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
                {
                    map.Add(key, check);
                }
            }

            return map;
        }

        private static bool IsCheckSupportedBySignalPresence(
            PlaybackQualityCheck check,
            HashSet<string> presentSignals,
            bool hasSignalPresence)
        {
            var signal = GetCheckKey(check);
            return !hasSignalPresence ||
                !PlaybackQualitySignalCatalog.IsReportSignal(signal) ||
                presentSignals.Contains(signal);
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

        private static void AddTrackAndSubtitleEvidenceDeltas(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            if (!HasTrackComparisonEvidence(baseline) &&
                !HasTrackComparisonEvidence(candidate))
            {
                return;
            }

            CompareEvidenceValue(
                comparison,
                "tracks.videoTrackCount",
                "tracks",
                baseline.Tracks.VideoTrackCount.ToString(CultureInfo.InvariantCulture),
                candidate.Tracks.VideoTrackCount.ToString(CultureInfo.InvariantCulture));
            CompareEvidenceValue(
                comparison,
                "tracks.audioTrackCount",
                "tracks",
                baseline.Tracks.AudioTrackCount.ToString(CultureInfo.InvariantCulture),
                candidate.Tracks.AudioTrackCount.ToString(CultureInfo.InvariantCulture));
            CompareEvidenceValue(
                comparison,
                "tracks.subtitleTrackCount",
                "subtitles",
                baseline.Tracks.SubtitleTrackCount.ToString(CultureInfo.InvariantCulture),
                candidate.Tracks.SubtitleTrackCount.ToString(CultureInfo.InvariantCulture));
            CompareEvidenceValue(
                comparison,
                "tracks.selectedVideoStreamIndex",
                "tracks",
                FormatNullableInt(baseline.Tracks.SelectedVideoStreamIndex),
                FormatNullableInt(candidate.Tracks.SelectedVideoStreamIndex));
            CompareEvidenceValue(
                comparison,
                "tracks.selectedAudioStreamIndex",
                "tracks",
                FormatNullableInt(baseline.Tracks.SelectedAudioStreamIndex),
                FormatNullableInt(candidate.Tracks.SelectedAudioStreamIndex));
            CompareEvidenceValue(
                comparison,
                "tracks.selectedSubtitleStreamIndex",
                "subtitles",
                FormatNullableInt(baseline.Tracks.SelectedSubtitleStreamIndex),
                FormatNullableInt(candidate.Tracks.SelectedSubtitleStreamIndex));
            CompareEvidenceValue(
                comparison,
                "tracks.isSubtitleDisabled",
                "subtitles",
                FormatBool(baseline.Tracks.IsSubtitleDisabled),
                FormatBool(candidate.Tracks.IsSubtitleDisabled));

            CompareTrackListEvidence(comparison, "tracks.video.codec", "tracks", baseline.Tracks.Video, candidate.Tracks.Video, track => track.Codec);
            CompareTrackListEvidence(comparison, "tracks.video.isExternal", "tracks", baseline.Tracks.Video, candidate.Tracks.Video, track => FormatBool(track.IsExternal));
            CompareTrackListEvidence(comparison, "tracks.video.isDefault", "tracks", baseline.Tracks.Video, candidate.Tracks.Video, track => FormatNullableBool(track.IsDefault));
            CompareTrackListEvidence(comparison, "tracks.video.isForced", "tracks", baseline.Tracks.Video, candidate.Tracks.Video, track => FormatNullableBool(track.IsForced));
            CompareTrackListEvidence(comparison, "tracks.audio.codec", "tracks", baseline.Tracks.Audio, candidate.Tracks.Audio, track => track.Codec);
            CompareTrackListEvidence(comparison, "tracks.audio.language", "tracks", baseline.Tracks.Audio, candidate.Tracks.Audio, track => track.Language);
            CompareTrackListEvidence(comparison, "tracks.audio.channels", "tracks", baseline.Tracks.Audio, candidate.Tracks.Audio, track => track.Channels.ToString(CultureInfo.InvariantCulture));
            CompareTrackListEvidence(comparison, "tracks.audio.isExternal", "tracks", baseline.Tracks.Audio, candidate.Tracks.Audio, track => FormatBool(track.IsExternal));
            CompareTrackListEvidence(comparison, "tracks.audio.isDefault", "tracks", baseline.Tracks.Audio, candidate.Tracks.Audio, track => FormatNullableBool(track.IsDefault));
            CompareTrackListEvidence(comparison, "tracks.audio.isForced", "tracks", baseline.Tracks.Audio, candidate.Tracks.Audio, track => FormatNullableBool(track.IsForced));
            CompareTrackListEvidence(comparison, "tracks.subtitles.codec", "subtitles", baseline.Tracks.Subtitles, candidate.Tracks.Subtitles, track => track.Codec);
            CompareTrackListEvidence(comparison, "tracks.subtitles.language", "subtitles", baseline.Tracks.Subtitles, candidate.Tracks.Subtitles, track => track.Language);
            CompareTrackListEvidence(comparison, "tracks.subtitles.isExternal", "subtitles", baseline.Tracks.Subtitles, candidate.Tracks.Subtitles, track => FormatBool(track.IsExternal));
            CompareTrackListEvidence(comparison, "tracks.subtitles.isDefault", "subtitles", baseline.Tracks.Subtitles, candidate.Tracks.Subtitles, track => FormatNullableBool(track.IsDefault));
            CompareTrackListEvidence(comparison, "tracks.subtitles.isForced", "subtitles", baseline.Tracks.Subtitles, candidate.Tracks.Subtitles, track => FormatNullableBool(track.IsForced));
        }

        private static void AddSeekTimelineEvidenceDeltas(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            CompareOptionalTimelineEvidence(
                comparison,
                "source.containerStartTimeTicks",
                baseline.Source.ContainerStartTimeTicks,
                candidate.Source.ContainerStartTimeTicks);
            CompareOptionalTimelineEvidence(
                comparison,
                "source.videoStreamStartTimeTicks",
                baseline.Source.VideoStreamStartTimeTicks,
                candidate.Source.VideoStreamStartTimeTicks);
            CompareOptionalTimelineEvidence(
                comparison,
                "source.durationTicks",
                baseline.Source.DurationTicks > 0 ? baseline.Source.DurationTicks : null,
                candidate.Source.DurationTicks > 0 ? candidate.Source.DurationTicks : null);
            CompareOptionalTimelineContext(
                comparison,
                "position.seekTargetPositionTicks",
                baseline.Position.SeekTargetPositionTicks.HasValue,
                candidate.Position.SeekTargetPositionTicks.HasValue);
            CompareOptionalTimelineContext(
                comparison,
                "position.seekDemuxTargetTicks",
                baseline.Position.SeekDemuxTargetTicks.HasValue,
                candidate.Position.SeekDemuxTargetTicks.HasValue);
            CompareOptionalTimelineContext(
                comparison,
                "position.firstPresentedPositionTicks",
                baseline.Position.FirstPresentedPositionTicks.HasValue,
                candidate.Position.FirstPresentedPositionTicks.HasValue);
            CompareOptionalTimelineContext(
                comparison,
                "position.postSeekPositionTicks",
                baseline.Position.PostSeekPositionTicks.HasValue,
                candidate.Position.PostSeekPositionTicks.HasValue);
            CompareOptionalTimelineContext(
                comparison,
                "position.postSeekAdvanced",
                baseline.Position.PostSeekAdvanced.HasValue,
                candidate.Position.PostSeekAdvanced.HasValue);
            CompareLowerIsBetterTimelineMetric(
                comparison,
                "position.seekOperationDurationMs",
                baseline.Position.SeekOperationDurationMs,
                candidate.Position.SeekOperationDurationMs);
            CompareLowerIsBetterTimelineMetric(
                comparison,
                "position.seekRecoveryDurationMs",
                baseline.Position.SeekRecoveryDurationMs,
                candidate.Position.SeekRecoveryDurationMs);
            CompareOptionalTimelineContext(
                comparison,
                "position.seekPacketCacheEnabled",
                baseline.Position.SeekPacketCacheEnabled.HasValue,
                candidate.Position.SeekPacketCacheEnabled.HasValue);
            CompareOptionalTimelineContext(
                comparison,
                "position.seekPacketCacheHit",
                baseline.Position.SeekPacketCacheHit.HasValue,
                candidate.Position.SeekPacketCacheHit.HasValue);
            CompareOptionalTimelineContext(
                comparison,
                "position.seekPacketCachePacketCount",
                baseline.Position.SeekPacketCachePacketCount.HasValue,
                candidate.Position.SeekPacketCachePacketCount.HasValue);
            CompareOptionalTimelineContext(
                comparison,
                "position.seekPacketCacheBytes",
                baseline.Position.SeekPacketCacheBytes.HasValue,
                candidate.Position.SeekPacketCacheBytes.HasValue);
            CompareOptionalTimelineContext(
                comparison,
                "position.seekPacketCacheWindowDurationTicks",
                baseline.Position.SeekPacketCacheWindowDurationTicks.HasValue,
                candidate.Position.SeekPacketCacheWindowDurationTicks.HasValue);
            CompareOptionalTimelineContext(
                comparison,
                "position.seekFallbackReason",
                !string.IsNullOrWhiteSpace(baseline.Position.SeekFallbackReason),
                !string.IsNullOrWhiteSpace(candidate.Position.SeekFallbackReason));

            var baselineError = ResolveSeekPositionErrorMs(baseline.Position);
            var candidateError = ResolveSeekPositionErrorMs(candidate.Position);
            if (!baselineError.HasValue || !candidateError.HasValue)
            {
                return;
            }

            AddUnique(comparison.Coverage.MatchedSignals, "position.seekPositionErrorMs");
            var numericDelta = candidateError.Value - baselineError.Value;
            if (Math.Abs(numericDelta) <= DerivedSignalEpsilon)
            {
                return;
            }

            var baselineCheck = CreateTimelineCheck("position.seekPositionErrorMs", baselineError.Value);
            var candidateCheck = CreateTimelineCheck("position.seekPositionErrorMs", candidateError.Value);
            if (numericDelta < 0)
            {
                comparison.Improvements.Add(CreateDelta(
                    baselineCheck,
                    candidateCheck,
                    "decreased",
                    numericDelta));
                AddUnique(comparison.Optimization.FailureAreas, "timeline");
            }
            else
            {
                comparison.Regressions.Add(CreateDelta(
                    baselineCheck,
                    candidateCheck,
                    "increased",
                    numericDelta));
                AddUnique(comparison.NewFailureAreas, "timeline");
            }
        }

        private static void CompareOptionalTimelineEvidence(
            PlaybackQualityRunComparison comparison,
            string signal,
            long? baseline,
            long? candidate)
        {
            CompareOptionalTimelineEvidence(
                comparison,
                signal,
                baseline.HasValue,
                candidate.HasValue,
                baseline?.ToString(CultureInfo.InvariantCulture) ?? "",
                candidate?.ToString(CultureInfo.InvariantCulture) ?? "");
        }

        private static void CompareOptionalTimelineContext(
            PlaybackQualityRunComparison comparison,
            string signal,
            bool hasBaseline,
            bool hasCandidate)
        {
            if (hasBaseline && hasCandidate)
            {
                AddUnique(comparison.Coverage.MatchedSignals, signal);
            }
            else if (hasBaseline)
            {
                AddUnique(comparison.Coverage.UnmatchedBaselineSignals, signal);
            }
            else if (hasCandidate)
            {
                AddUnique(comparison.Coverage.UnmatchedCandidateSignals, signal);
            }
        }

        private static void CompareLowerIsBetterTimelineMetric(
            PlaybackQualityRunComparison comparison,
            string signal,
            double? baseline,
            double? candidate)
        {
            CompareOptionalTimelineContext(
                comparison,
                signal,
                baseline.HasValue,
                candidate.HasValue);
            if (!baseline.HasValue || !candidate.HasValue ||
                comparison.Improvements.Any(delta => delta.Signal == signal) ||
                comparison.Regressions.Any(delta => delta.Signal == signal) ||
                comparison.PolicyChanges.Any(delta => delta.Signal == signal))
            {
                return;
            }

            var delta = candidate.Value - baseline.Value;
            if (Math.Abs(delta) <= DerivedSignalEpsilon)
            {
                return;
            }

            var baselineCheck = CreateTimelineCheck(signal, baseline.Value);
            var candidateCheck = CreateTimelineCheck(signal, candidate.Value);
            if (delta < 0)
            {
                comparison.Improvements.Add(CreateDelta(
                    baselineCheck,
                    candidateCheck,
                    "decreased",
                    delta));
                AddUnique(comparison.Optimization.FailureAreas, "timeline");
            }
            else
            {
                comparison.Regressions.Add(CreateDelta(
                    baselineCheck,
                    candidateCheck,
                    "increased",
                    delta));
                AddUnique(comparison.NewFailureAreas, "timeline");
            }
        }

        private static void CompareOptionalTimelineEvidence(
            PlaybackQualityRunComparison comparison,
            string signal,
            bool? baseline,
            bool? candidate)
        {
            CompareOptionalTimelineEvidence(
                comparison,
                signal,
                baseline.HasValue,
                candidate.HasValue,
                baseline?.ToString() ?? "",
                candidate?.ToString() ?? "");
        }

        private static void CompareOptionalTimelineEvidence(
            PlaybackQualityRunComparison comparison,
            string signal,
            double? baseline,
            double? candidate)
        {
            CompareOptionalTimelineEvidence(
                comparison,
                signal,
                baseline.HasValue,
                candidate.HasValue,
                baseline?.ToString("0.000", CultureInfo.InvariantCulture) ?? "",
                candidate?.ToString("0.000", CultureInfo.InvariantCulture) ?? "");
        }

        private static void CompareOptionalTimelineEvidence(
            PlaybackQualityRunComparison comparison,
            string signal,
            bool hasBaseline,
            bool hasCandidate,
            string baseline,
            string candidate)
        {
            if (hasBaseline && hasCandidate)
            {
                CompareEvidenceValue(comparison, signal, "timeline", baseline, candidate);
            }
            else if (hasBaseline)
            {
                AddUnique(comparison.Coverage.UnmatchedBaselineSignals, signal);
            }
            else if (hasCandidate)
            {
                AddUnique(comparison.Coverage.UnmatchedCandidateSignals, signal);
            }
        }

        private static double? ResolveSeekPositionErrorMs(PlaybackQualityPosition position)
        {
            if (position.SeekPositionErrorMs.HasValue)
            {
                return Math.Abs(position.SeekPositionErrorMs.Value);
            }

            if (position.SeekTargetPositionTicks.HasValue &&
                position.ActualPositionTicks.HasValue)
            {
                return Math.Abs(
                    position.ActualPositionTicks.Value -
                    position.SeekTargetPositionTicks.Value) / 10000.0;
            }

            return null;
        }

        private static PlaybackQualityCheck CreateTimelineCheck(
            string signal,
            double actual)
        {
            return new PlaybackQualityCheck
            {
                Name = signal,
                Signal = signal,
                FailureArea = "timeline",
                Status = "observed",
                Actual = actual.ToString("0.000", CultureInfo.InvariantCulture)
            };
        }

        private static void AddRuntimePlaybackEvidenceSignals(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate,
            HashSet<string> baselineSignals,
            HashSet<string> candidateSignals)
        {
            var hasComparableEpisodeSemantics =
                HasComparableAudioAheadWaitEpisodeSemantics(baseline, candidate);
            foreach (var descriptor in PlaybackQualitySignalCatalog.ReportSignals)
            {
                var signal = descriptor.Signal;
                if (!IsRuntimePlaybackSignal(signal) ||
                    (!hasComparableEpisodeSemantics && IsEpisodeSemanticSignal(signal)))
                {
                    continue;
                }

                AddSignalPresenceCoverage(
                    comparison,
                    signal,
                    baselineSignals.Contains(signal),
                    candidateSignals.Contains(signal));
            }
        }

        private static HashSet<string> CreateComparisonSignalSet(
            PlaybackQualityReport report,
            IEnumerable<string>? presentSignals)
        {
            return new HashSet<string>(
                presentSignals ?? PlaybackQualityReportAnalyzer.Analyze(report).EvidenceSignals,
                StringComparer.Ordinal);
        }

        private static bool IsRuntimePlaybackSignal(string signal)
        {
            return signal.StartsWith("timing.", StringComparison.Ordinal) ||
                signal.StartsWith("sync.", StringComparison.Ordinal) ||
                signal.StartsWith("buffers.", StringComparison.Ordinal) ||
                signal.StartsWith("readRecovery.", StringComparison.Ordinal) ||
                signal.StartsWith("runtimeMetrics.", StringComparison.Ordinal);
        }

        private static bool IsEpisodeSemanticSignal(string signal)
        {
            return signal.StartsWith("timing.audioAheadWaitTargetMs", StringComparison.Ordinal) ||
                signal.StartsWith("timing.audioAheadWaitOversleepMs", StringComparison.Ordinal);
        }

        private static void AddSignalPresenceCoverage(
            PlaybackQualityRunComparison comparison,
            string signal,
            bool baselinePresent,
            bool candidatePresent)
        {
            if (baselinePresent && candidatePresent)
            {
                AddUnique(comparison.Coverage.MatchedSignals, signal);
            }
            else if (baselinePresent)
            {
                AddUnique(comparison.Coverage.UnmatchedBaselineSignals, signal);
            }
            else if (candidatePresent)
            {
                AddUnique(comparison.Coverage.UnmatchedCandidateSignals, signal);
            }
        }

        private static void AddAudioAheadWaitOversleepDeltas(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            if (!HasComparableAudioAheadWaitEpisodeSemantics(baseline, candidate))
            {
                if (!HasAudioAheadWaitEpisodeMetricEvidence(baseline) &&
                    !HasAudioAheadWaitEpisodeMetricEvidence(candidate))
                {
                    return;
                }

                var baselineSemantics = NormalizeAudioAheadWaitOversleepSemantics(
                    baseline.Timing.AudioAheadWaitOversleepSemantics);
                var candidateSemantics = NormalizeAudioAheadWaitOversleepSemantics(
                    candidate.Timing.AudioAheadWaitOversleepSemantics);
                if (HasAudioAheadWaitTargetEvidence(baseline) ||
                    HasAudioAheadWaitTargetEvidence(candidate))
                {
                    AddUnique(
                        comparison.Coverage.UnmatchedBaselineSignals,
                        "timing.audioAheadWaitTargetMs@" + baselineSemantics);
                    AddUnique(
                        comparison.Coverage.UnmatchedCandidateSignals,
                        "timing.audioAheadWaitTargetMs@" + candidateSemantics);
                }

                if (HasAudioAheadWaitOversleepEvidence(baseline) ||
                    HasAudioAheadWaitOversleepEvidence(candidate))
                {
                    AddUnique(
                        comparison.Coverage.UnmatchedBaselineSignals,
                        "timing.audioAheadWaitOversleepMs@" + baselineSemantics);
                    AddUnique(
                        comparison.Coverage.UnmatchedCandidateSignals,
                        "timing.audioAheadWaitOversleepMs@" + candidateSemantics);
                }

                AddUnique(
                    comparison.Limitations,
                    "audio-ahead episode metric semantics differ; target and oversleep deltas were not compared");
                return;
            }

            if (!AudioAheadFinalDeltaCorroboratesOversleepDelta(baseline, candidate))
            {
                return;
            }

            var p95Delta = CompareAudioAheadWaitOversleep(
                comparison,
                "timing.audioAheadWaitOversleepMsP95",
                baseline.Timing.AudioAheadWaitOversleepMsP95,
                candidate.Timing.AudioAheadWaitOversleepMsP95);

            if (!p95Delta.HasValue)
            {
                return;
            }

            var requiredDirection = p95Delta.Value < 0 ? -1 : 1;
            CompareAudioAheadWaitOversleep(
                comparison,
                "timing.audioAheadWaitOversleepMsP99",
                baseline.Timing.AudioAheadWaitOversleepMsP99,
                candidate.Timing.AudioAheadWaitOversleepMsP99,
                requiredDirection);
        }

        private static bool HasComparableAudioAheadWaitEpisodeSemantics(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            return string.Equals(
                NormalizeAudioAheadWaitOversleepSemantics(
                    baseline.Timing.AudioAheadWaitOversleepSemantics),
                NormalizeAudioAheadWaitOversleepSemantics(
                    candidate.Timing.AudioAheadWaitOversleepSemantics),
                StringComparison.Ordinal);
        }

        private static bool HasAudioAheadWaitEpisodeMetricEvidence(PlaybackQualityReport report)
        {
            return HasAudioAheadWaitTargetEvidence(report) ||
                HasAudioAheadWaitOversleepEvidence(report);
        }

        private static bool HasAudioAheadWaitTargetEvidence(PlaybackQualityReport report)
        {
            return report.Timing.AudioAheadWaitTargetMsP50 > 0 ||
                report.Timing.AudioAheadWaitTargetMsP95 > 0 ||
                report.Timing.AudioAheadWaitTargetMsP99 > 0 ||
                report.Timing.AudioAheadWaitTargetMsMax > 0;
        }

        private static bool HasAudioAheadWaitOversleepEvidence(PlaybackQualityReport report)
        {
            return report.Timing.AudioAheadWaitOversleepMsP50 > 0 ||
                report.Timing.AudioAheadWaitOversleepMsP95 > 0 ||
                report.Timing.AudioAheadWaitOversleepMsP99 > 0 ||
                report.Timing.AudioAheadWaitOversleepMsMax > 0;
        }

        private static string NormalizeAudioAheadWaitOversleepSemantics(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unspecified" : value.Trim();
        }

        private static bool AudioAheadFinalDeltaCorroboratesOversleepDelta(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            var oversleepDelta = candidate.Timing.AudioAheadWaitOversleepMsP95 -
                baseline.Timing.AudioAheadWaitOversleepMsP95;
            if (Math.Abs(oversleepDelta) < MinimumAudioAheadWaitOversleepDeltaMs)
            {
                return true;
            }

            var hasFinalDeltaEvidence =
                baseline.Timing.AudioAheadWaitFinalDeltaAbsMsP95 > 0 &&
                candidate.Timing.AudioAheadWaitFinalDeltaAbsMsP95 > 0;
            if (!hasFinalDeltaEvidence)
            {
                return true;
            }

            var requiredDirection = oversleepDelta < 0 ? -1 : 1;
            return AudioAheadFinalDeltaMovesMaterially(
                    baseline.Timing.AudioAheadWaitFinalDeltaAbsMsP95,
                    candidate.Timing.AudioAheadWaitFinalDeltaAbsMsP95,
                    requiredDirection) ||
                AudioAheadFinalDeltaMovesMaterially(
                    baseline.Timing.AudioAheadWaitFinalDeltaAbsMsP99,
                    candidate.Timing.AudioAheadWaitFinalDeltaAbsMsP99,
                    requiredDirection);
        }

        private static bool AudioAheadFinalDeltaMovesMaterially(
            double baselineActual,
            double candidateActual,
            int requiredDirection)
        {
            if (baselineActual <= 0 || candidateActual <= 0)
            {
                return false;
            }

            var numericDelta = candidateActual - baselineActual;
            if (Math.Abs(numericDelta) < MinimumAudioAheadWaitOversleepDeltaMs)
            {
                return false;
            }

            var direction = numericDelta < 0 ? -1 : 1;
            return direction == requiredDirection;
        }

        private static double? CompareAudioAheadWaitOversleep(
            PlaybackQualityRunComparison comparison,
            string signal,
            double baselineActual,
            double candidateActual,
            int? requiredDirection = null)
        {
            if (baselineActual <= 0 || candidateActual <= 0)
            {
                return null;
            }

            AddUnique(comparison.Coverage.MatchedSignals, signal);

            var numericDelta = candidateActual - baselineActual;
            if (Math.Abs(numericDelta) < MinimumAudioAheadWaitOversleepDeltaMs)
            {
                return null;
            }

            var direction = numericDelta < 0 ? -1 : 1;
            if (requiredDirection.HasValue && direction != requiredDirection.Value)
            {
                return null;
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
                AddUnique(comparison.Optimization.FailureAreas, "frame-pacing");
            }
            else
            {
                comparison.Regressions.Add(CreateDelta(
                    baselineCheck,
                    candidateCheck,
                    "increased",
                    numericDelta));
                AddUnique(comparison.NewFailureAreas, "frame-pacing");
            }

            return numericDelta;
        }

        private static bool HasTrackComparisonEvidence(PlaybackQualityReport report)
        {
            return report.Tracks.VideoTrackCount > 0 ||
                report.Tracks.AudioTrackCount > 0 ||
                report.Tracks.SubtitleTrackCount > 0 ||
                report.Tracks.Video.Count > 0 ||
                report.Tracks.Audio.Count > 0 ||
                report.Tracks.Subtitles.Count > 0 ||
                report.Tracks.SelectedVideoStreamIndex.HasValue ||
                report.Tracks.SelectedAudioStreamIndex.HasValue ||
                report.Tracks.SelectedSubtitleStreamIndex.HasValue;
        }

        private static void CompareTrackListEvidence(
            PlaybackQualityRunComparison comparison,
            string signal,
            string failureArea,
            IReadOnlyList<PlaybackQualityTrack> baseline,
            IReadOnlyList<PlaybackQualityTrack> candidate,
            Func<PlaybackQualityTrack, string> selectValue)
        {
            if (baseline.Count == 0 && candidate.Count == 0)
            {
                return;
            }

            CompareEvidenceValue(
                comparison,
                signal,
                failureArea,
                JoinTrackValues(baseline, selectValue),
                JoinTrackValues(candidate, selectValue));
        }

        private static void CompareEvidenceValue(
            PlaybackQualityRunComparison comparison,
            string signal,
            string failureArea,
            string baselineActual,
            string candidateActual)
        {
            AddUnique(comparison.Coverage.MatchedSignals, signal);
            if (string.Equals(baselineActual, candidateActual, StringComparison.Ordinal))
            {
                return;
            }

            comparison.Regressions.Add(CreateDelta(
                CreateEvidenceCheck(signal, failureArea, baselineActual),
                CreateEvidenceCheck(signal, failureArea, candidateActual),
                "changed",
                0));
            AddUnique(comparison.NewFailureAreas, failureArea);
        }

        private static PlaybackQualityCheck CreateEvidenceCheck(
            string signal,
            string failureArea,
            string actual)
        {
            return new PlaybackQualityCheck
            {
                Name = signal,
                Signal = signal,
                FailureArea = failureArea,
                Status = "observed",
                Actual = actual
            };
        }

        private static string JoinTrackValues(
            IReadOnlyList<PlaybackQualityTrack> tracks,
            Func<PlaybackQualityTrack, string> selectValue)
        {
            var values = new List<string>();
            foreach (var track in tracks)
            {
                values.Add(Normalize(selectValue(track)));
            }

            return string.Join("|", values.ToArray());
        }

        private static string FormatNullableInt(int? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "";
        }

        private static string FormatBool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string FormatNullableBool(bool? value)
        {
            return value.HasValue ? FormatBool(value.Value) : "";
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }

        private static void AddFramePacingSeverityDeltas(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate,
            HashSet<string> baselineSignals,
            HashSet<string> candidateSignals,
            bool hasBaselineSignalPresence,
            bool hasCandidateSignalPresence)
        {
            if (!string.Equals(
                    baseline.Execution?.Scenario,
                    PlaybackQualityExecutionScenario.Playback,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (!IsFramePacingComparison(comparison) &&
                !HasComparableExpectedFramePacingEvidence(baseline, candidate))
            {
                return;
            }

            var baselineAnalysis = PlaybackQualityReportAnalyzer.Analyze(baseline);
            var candidateAnalysis = PlaybackQualityReportAnalyzer.Analyze(candidate);

            if (HasComparableDerivedInputs(
                comparison,
                "framePacing.renderIntervalP95FrameRatio",
                baselineSignals,
                candidateSignals,
                hasBaselineSignalPresence,
                hasCandidateSignalPresence,
                "timing.expectedFrameDurationMs",
                "timing.renderIntervalMsP95"))
            {
                CompareDerivedFrameRatio(
                    comparison,
                    "framePacing.renderIntervalP95FrameRatio",
                    baselineAnalysis.FramePacing.RenderIntervalP95FrameRatio,
                    candidateAnalysis.FramePacing.RenderIntervalP95FrameRatio,
                    requirePositive: true);
            }

            if (HasComparableDerivedInputs(
                comparison,
                "framePacing.renderIntervalP99FrameRatio",
                baselineSignals,
                candidateSignals,
                hasBaselineSignalPresence,
                hasCandidateSignalPresence,
                "timing.expectedFrameDurationMs",
                "timing.renderIntervalMsP99"))
            {
                CompareDerivedFrameRatio(
                    comparison,
                    "framePacing.renderIntervalP99FrameRatio",
                    baselineAnalysis.FramePacing.RenderIntervalP99FrameRatio,
                    candidateAnalysis.FramePacing.RenderIntervalP99FrameRatio,
                    requirePositive: true);
            }

            if (HasComparableDerivedInputs(
                comparison,
                "framePacing.maxFrameGapFrameRatio",
                baselineSignals,
                candidateSignals,
                hasBaselineSignalPresence,
                hasCandidateSignalPresence,
                "timing.expectedFrameDurationMs",
                "timing.maxFrameGapMs"))
            {
                CompareDerivedFrameRatio(
                    comparison,
                    "framePacing.maxFrameGapFrameRatio",
                    baselineAnalysis.FramePacing.MaxFrameGapFrameRatio,
                    candidateAnalysis.FramePacing.MaxFrameGapFrameRatio,
                    requirePositive: true);
            }

            if (HasComparableDerivedInputs(
                comparison,
                "framePacing.renderIntervalP95ExpectedErrorMs",
                baselineSignals,
                candidateSignals,
                hasBaselineSignalPresence,
                hasCandidateSignalPresence,
                "timing.expectedFrameDurationMs",
                "timing.renderIntervalMsP95"))
            {
                CompareFramePacingExpectedError(
                    comparison,
                    "framePacing.renderIntervalP95ExpectedErrorMs",
                    baseline.Timing.RenderIntervalMsP95,
                    candidate.Timing.RenderIntervalMsP95,
                    baseline.Timing.ExpectedFrameDurationMs,
                    candidate.Timing.ExpectedFrameDurationMs);
            }

            if (HasComparableDerivedInputs(
                comparison,
                "framePacing.renderIntervalP99ExpectedErrorMs",
                baselineSignals,
                candidateSignals,
                hasBaselineSignalPresence,
                hasCandidateSignalPresence,
                "timing.expectedFrameDurationMs",
                "timing.renderIntervalMsP99"))
            {
                CompareFramePacingExpectedError(
                    comparison,
                    "framePacing.renderIntervalP99ExpectedErrorMs",
                    baseline.Timing.RenderIntervalMsP99,
                    candidate.Timing.RenderIntervalMsP99,
                    baseline.Timing.ExpectedFrameDurationMs,
                    candidate.Timing.ExpectedFrameDurationMs);
            }

            if (HasComparableDerivedInputs(
                comparison,
                "framePacing.maxFrameGapExpectedErrorMs",
                baselineSignals,
                candidateSignals,
                hasBaselineSignalPresence,
                hasCandidateSignalPresence,
                "timing.expectedFrameDurationMs",
                "timing.maxFrameGapMs"))
            {
                CompareFramePacingExpectedError(
                    comparison,
                    "framePacing.maxFrameGapExpectedErrorMs",
                    baseline.Timing.MaxFrameGapMs,
                    candidate.Timing.MaxFrameGapMs,
                    baseline.Timing.ExpectedFrameDurationMs,
                    candidate.Timing.ExpectedFrameDurationMs);
            }

            if (HasComparableDerivedInputs(
                comparison,
                "framePacing.lateFrameDropToleranceFrameRatio",
                baselineSignals,
                candidateSignals,
                hasBaselineSignalPresence,
                hasCandidateSignalPresence,
                "timing.expectedFrameDurationMs",
                "timing.lateFrameDropToleranceMs"))
            {
                CompareDerivedPolicyChange(
                    comparison,
                    "framePacing.lateFrameDropToleranceFrameRatio",
                    baselineAnalysis.FramePacing.LateFrameDropToleranceFrameRatio,
                    candidateAnalysis.FramePacing.LateFrameDropToleranceFrameRatio,
                    requirePositive: true);
            }

            if (HasComparableDerivedInputs(
                    comparison,
                    "framePacing.droppedVideoFramePercent",
                    baselineSignals,
                    candidateSignals,
                    hasBaselineSignalPresence,
                    hasCandidateSignalPresence,
                    "timing.renderedVideoFrames",
                    "timing.droppedVideoFrames") &&
                HasObservedFrameCount(baseline) &&
                HasObservedFrameCount(candidate))
            {
                CompareDerivedLowerIsBetter(
                    comparison,
                    "framePacing.droppedVideoFramePercent",
                    baselineAnalysis.FramePacing.DroppedVideoFramePercent,
                    candidateAnalysis.FramePacing.DroppedVideoFramePercent,
                    requirePositive: false);
            }
        }

        private static void AddInteractionScenarioOutcomeDelta(
            PlaybackQualityRunComparison comparison,
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            var scenario = baseline.Execution?.Scenario ?? "";
            var failureArea = scenario switch
            {
                PlaybackQualityExecutionScenario.AudioSwitch => "tracks",
                PlaybackQualityExecutionScenario.SubtitleSwitch => "subtitles",
                _ => ""
            };
            if (string.IsNullOrWhiteSpace(failureArea))
            {
                return;
            }

            var baselineEvent = baseline.Lifecycle.Events.LastOrDefault(item =>
                string.Equals(item.Operation, scenario, StringComparison.Ordinal));
            var candidateEvent = candidate.Lifecycle.Events.LastOrDefault(item =>
                string.Equals(item.Operation, scenario, StringComparison.Ordinal));
            if (baselineEvent == null || candidateEvent == null)
            {
                return;
            }

            var signal = "lifecycle." + scenario;
            AddUnique(comparison.Coverage.MatchedSignals, signal);
            if (string.Equals(baselineEvent.Status, candidateEvent.Status, StringComparison.Ordinal))
            {
                return;
            }

            var baselineCheck = CreateEvidenceCheck(signal, failureArea, baselineEvent.Status);
            var candidateCheck = CreateEvidenceCheck(signal, failureArea, candidateEvent.Status);
            if (baselineEvent.Status == "failed" && candidateEvent.Status == "completed")
            {
                comparison.Improvements.Add(CreateDelta(
                    baselineCheck,
                    candidateCheck,
                    "resolved",
                    0));
                AddUnique(comparison.ResolvedFailureAreas, failureArea);
                AddUnique(comparison.Optimization.FailureAreas, failureArea);
            }
            else if (baselineEvent.Status == "completed" && candidateEvent.Status == "failed")
            {
                comparison.Regressions.Add(CreateDelta(
                    baselineCheck,
                    candidateCheck,
                    "regressed",
                    0));
                AddUnique(comparison.NewFailureAreas, failureArea);
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

        private static bool HasComparableExpectedFramePacingEvidence(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            return baseline.Timing.ExpectedFrameDurationMs > 0 &&
                candidate.Timing.ExpectedFrameDurationMs > 0 &&
                Math.Abs(
                    baseline.Timing.ExpectedFrameDurationMs -
                    candidate.Timing.ExpectedFrameDurationMs) <= DerivedSignalEpsilon &&
                ((baseline.Timing.RenderIntervalMsP95 > 0 && candidate.Timing.RenderIntervalMsP95 > 0) ||
                    (baseline.Timing.RenderIntervalMsP99 > 0 && candidate.Timing.RenderIntervalMsP99 > 0) ||
                    (baseline.Timing.MaxFrameGapMs > 0 && candidate.Timing.MaxFrameGapMs > 0));
        }

        private static bool HasObservedFrameCount(PlaybackQualityReport report)
        {
            return report.Timing.RenderedVideoFrames > 0 ||
                report.Timing.DroppedVideoFrames > 0;
        }

        private static void CompareDerivedFrameRatio(
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
            var baselineAcceptable = IsAcceptableFrameRatio(baselineActual);
            var candidateAcceptable = IsAcceptableFrameRatio(candidateActual);
            if (!baselineAcceptable && candidateAcceptable)
            {
                comparison.Improvements.Add(CreateDelta(
                    CreateDerivedCheck(signal, baselineActual),
                    CreateDerivedCheck(signal, candidateActual),
                    numericDelta < 0 ? "decreased" : "increased",
                    numericDelta));
                return;
            }

            if (baselineAcceptable && !candidateAcceptable)
            {
                comparison.Regressions.Add(CreateDelta(
                    CreateDerivedCheck(signal, baselineActual),
                    CreateDerivedCheck(signal, candidateActual),
                    numericDelta < 0 ? "decreased" : "increased",
                    numericDelta));
                return;
            }

            if (baselineAcceptable && candidateAcceptable)
            {
                return;
            }

            var baselineDistance = FrameRatioDistanceFromAcceptableRange(baselineActual);
            var candidateDistance = FrameRatioDistanceFromAcceptableRange(candidateActual);
            if (Math.Abs(candidateDistance - baselineDistance) <= DerivedSignalEpsilon)
            {
                return;
            }

            if (candidateDistance < baselineDistance)
            {
                comparison.Improvements.Add(CreateDelta(
                    CreateDerivedCheck(signal, baselineActual),
                    CreateDerivedCheck(signal, candidateActual),
                    numericDelta < 0 ? "decreased" : "increased",
                    numericDelta));
            }
            else
            {
                comparison.Regressions.Add(CreateDelta(
                    CreateDerivedCheck(signal, baselineActual),
                    CreateDerivedCheck(signal, candidateActual),
                    numericDelta < 0 ? "decreased" : "increased",
                    numericDelta));
            }
        }

        private static bool HasComparableDerivedInputs(
            PlaybackQualityRunComparison comparison,
            string derivedSignal,
            HashSet<string> baselineSignals,
            HashSet<string> candidateSignals,
            bool hasBaselineSignalPresence,
            bool hasCandidateSignalPresence,
            params string[] requiredSignals)
        {
            var baselinePresent = !hasBaselineSignalPresence ||
                ContainsAllSignals(baselineSignals, requiredSignals);
            var candidatePresent = !hasCandidateSignalPresence ||
                ContainsAllSignals(candidateSignals, requiredSignals);
            if (baselinePresent && !candidatePresent)
            {
                AddUnique(comparison.Coverage.UnmatchedBaselineSignals, derivedSignal);
            }
            else if (!baselinePresent && candidatePresent)
            {
                AddUnique(comparison.Coverage.UnmatchedCandidateSignals, derivedSignal);
            }

            return baselinePresent && candidatePresent;
        }

        private static bool ContainsAllSignals(
            HashSet<string> presentSignals,
            IReadOnlyList<string> requiredSignals)
        {
            foreach (var signal in requiredSignals)
            {
                if (!presentSignals.Contains(signal))
                {
                    return false;
                }
            }

            return true;
        }

        private static void CompareFramePacingExpectedError(
            PlaybackQualityRunComparison comparison,
            string signal,
            double baselineActual,
            double candidateActual,
            double baselineExpectedFrameDurationMs,
            double candidateExpectedFrameDurationMs)
        {
            if (baselineActual <= 0 ||
                candidateActual <= 0 ||
                baselineExpectedFrameDurationMs <= 0 ||
                candidateExpectedFrameDurationMs <= 0 ||
                Math.Abs(baselineExpectedFrameDurationMs - candidateExpectedFrameDurationMs) > DerivedSignalEpsilon)
            {
                return;
            }

            AddUnique(comparison.Coverage.MatchedSignals, signal);

            var baselineError = Math.Abs(baselineActual - baselineExpectedFrameDurationMs);
            var candidateError = Math.Abs(candidateActual - candidateExpectedFrameDurationMs);
            var numericDelta = candidateError - baselineError;
            if (Math.Abs(numericDelta) < MinimumFramePacingExpectedErrorDeltaMs)
            {
                return;
            }

            var baselineCheck = CreateDerivedCheck(signal, baselineError);
            var candidateCheck = CreateDerivedCheck(signal, candidateError);
            if (numericDelta < 0)
            {
                comparison.Improvements.Add(CreateDelta(
                    baselineCheck,
                    candidateCheck,
                    "decreased",
                    numericDelta));
                AddUnique(comparison.Optimization.FailureAreas, "frame-pacing");
            }
            else
            {
                comparison.Regressions.Add(CreateDelta(
                    baselineCheck,
                    candidateCheck,
                    "increased",
                    numericDelta));
                AddUnique(comparison.NewFailureAreas, "frame-pacing");
            }
        }

        private static bool IsAcceptableFrameRatio(double value)
        {
            return value >= MinimumAcceptableFrameRatio &&
                value <= MaximumAcceptableFrameRatio;
        }

        private static double FrameRatioDistanceFromAcceptableRange(double value)
        {
            if (value < MinimumAcceptableFrameRatio)
            {
                return MinimumAcceptableFrameRatio - value;
            }

            if (value > MaximumAcceptableFrameRatio)
            {
                return value - MaximumAcceptableFrameRatio;
            }

            return 0;
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
