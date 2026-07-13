using System;
using System.Collections.Generic;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityReferenceReportSetValidation
    {
        public int SchemaVersion { get; set; } = 1;
        public string EvaluationVersion { get; set; } =
            PlaybackQualityRunResult.CurrentEvaluationVersion;

        public bool StructureValid { get; set; } = true;

        public bool ExecutionValid { get; set; } = true;

        public bool IsValid => StructureValid && ExecutionValid && Errors.Count == 0;

        public int ExpectedCaseCount { get; set; }

        public int ReportCount { get; set; }

        public int MatchedCaseCount { get; set; }

        public PlaybackQualityExecutionCoverage ExecutionCoverage { get; set; } =
            new PlaybackQualityExecutionCoverage();

        public List<PlaybackQualityReferenceReportCaseStatus> Cases { get; } =
            new List<PlaybackQualityReferenceReportCaseStatus>();

        public List<PlaybackQualityReferenceReportSetError> Errors { get; } =
            new List<PlaybackQualityReferenceReportSetError>();
    }

    public sealed class PlaybackQualityReferenceReportCaseStatus
    {
        public string CaseId { get; set; } = "";

        public string Category { get; set; } = "stable";

        public string Severity { get; set; } = "medium";

        public string Stability { get; set; } = "stable";

        public string ReportRunId { get; set; } = "";

        public string Status { get; set; } = "";

        public List<string> Signals { get; } = new List<string>();
    }

    public sealed class PlaybackQualityReferenceReportSetError
    {
        public string Code { get; set; } = "";

        public string CaseId { get; set; } = "";

        public string ReportRunId { get; set; } = "";

        public string Signal { get; set; } = "";

        public string Expected { get; set; } = "";

        public string Actual { get; set; } = "";

        public string Message { get; set; } = "";

        public string FailureArea { get; set; } = "";

        public string FailureClass { get; set; } = "";

        public string SuggestedNextAction { get; set; } = "";

        public List<string> CodeTargets { get; } = new List<string>();
    }

    public sealed class PlaybackQualityReferenceReportSetEntry
    {
        public PlaybackQualityReferenceReportSetEntry()
        {
        }

        public PlaybackQualityReferenceReportSetEntry(PlaybackQualityReport report)
        {
            Report = report;
        }

        public PlaybackQualityReport Report { get; set; } = new PlaybackQualityReport();

        public bool HasSignalPresenceEvidence { get; set; }

        public List<string> PresentSignals { get; } = new List<string>();
    }

    public static class PlaybackQualityReferenceReportSetValidator
    {
        private const double FrameRateTolerance = 0.01;
        private static readonly string[] RequiredSkipSignals =
        {
            "skip.code",
            "skip.reason",
            "skip.operation",
            "skip.failureClass",
            "skip.failureArea",
            "skip.isExpected",
            "skip.isRetriable",
            "lifecycle.skip"
        };

        public static PlaybackQualityReferenceReportSetValidation Validate(
            PlaybackQualityReferenceManifest manifest,
            IEnumerable<PlaybackQualityReport> reports)
        {
            var entries = new List<PlaybackQualityReferenceReportSetEntry>();
            if (reports != null)
            {
                foreach (var report in reports)
                {
                    if (report != null)
                    {
                        entries.Add(new PlaybackQualityReferenceReportSetEntry(report));
                    }
                }
            }

            return Validate(manifest, entries);
        }

        public static PlaybackQualityReferenceReportSetValidation Validate(
            PlaybackQualityReferenceManifest manifest,
            IEnumerable<PlaybackQualityReferenceReportSetEntry> entries)
        {
            var validation = new PlaybackQualityReferenceReportSetValidation();
            if (manifest == null)
            {
                AddError(
                    validation,
                    "manifest.missing",
                    "",
                    "",
                    "manifest",
                    "",
                    "",
                    "Playback quality reference manifest is missing.");
                return validation;
            }

            var entryList = new List<PlaybackQualityReferenceReportSetEntry>();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry?.Report != null)
                    {
                        entryList.Add(entry);
                    }
                }
            }

            validation.ExpectedCaseCount = manifest.Cases.Count;
            validation.ExecutionCoverage.DeclaredCaseCount = manifest.Cases.Count;
            validation.ReportCount = entryList.Count;
            var reportsByRunId = BuildReportMap(validation, entryList);
            ValidateManifestCases(validation, manifest, reportsByRunId);
            AddExtraReports(validation, manifest, entryList);
            return validation;
        }

        private static Dictionary<string, List<PlaybackQualityReferenceReportSetEntry>> BuildReportMap(
            PlaybackQualityReferenceReportSetValidation validation,
            List<PlaybackQualityReferenceReportSetEntry> entries)
        {
            var reportsByRunId = new Dictionary<string, List<PlaybackQualityReferenceReportSetEntry>>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var report = entry.Report;
                var runId = report.RunId ?? "";
                if (!reportsByRunId.TryGetValue(runId, out var runReports))
                {
                    runReports = new List<PlaybackQualityReferenceReportSetEntry>();
                    reportsByRunId.Add(runId, runReports);
                }

                runReports.Add(entry);
            }

            foreach (var item in reportsByRunId)
            {
                if (item.Value.Count > 1)
                {
                    AddError(
                        validation,
                        "report.duplicate-run-id",
                        item.Key,
                        item.Key,
                        "runId",
                        "unique",
                        item.Value.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "Playback quality report runId is duplicated.");
                }
            }

            return reportsByRunId;
        }

        private static void ValidateManifestCases(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceManifest manifest,
            Dictionary<string, List<PlaybackQualityReferenceReportSetEntry>> reportsByRunId)
        {
            foreach (var referenceCase in manifest.Cases)
            {
                var caseId = referenceCase.CaseId ?? "";
                if (!reportsByRunId.TryGetValue(caseId, out var reports) || reports.Count == 0)
                {
                    var category = NormalizeCaseCategory(referenceCase.Category);
                    if (string.Equals(category, "quarantine", StringComparison.Ordinal))
                    {
                        validation.ExecutionCoverage.QuarantineMissingCaseCount++;
                        validation.Cases.Add(new PlaybackQualityReferenceReportCaseStatus
                        {
                            CaseId = caseId,
                            Category = category,
                            Severity = NormalizeCaseSeverity(referenceCase.Severity),
                            Stability = NormalizeCaseStability(referenceCase.Stability),
                            Status = "quarantine-missing"
                        });
                        continue;
                    }

                    validation.ExecutionCoverage.MissingCaseCount++;

                    validation.Cases.Add(new PlaybackQualityReferenceReportCaseStatus
                    {
                        CaseId = caseId,
                        Category = category,
                        Severity = NormalizeCaseSeverity(referenceCase.Severity),
                        Stability = NormalizeCaseStability(referenceCase.Stability),
                        Status = "missing"
                    });
                    AddError(
                        validation,
                        "report.missing",
                        caseId,
                        "",
                        "runId",
                        caseId,
                        "",
                        "Playback quality report is missing for reference case.");
                    continue;
                }

                var entry = reports[0];
                var report = entry.Report;
                RecordExecutionCoverage(validation.ExecutionCoverage, report);
                var status = new PlaybackQualityReferenceReportCaseStatus
                {
                    CaseId = caseId,
                    Category = NormalizeCaseCategory(referenceCase.Category),
                    Severity = NormalizeCaseSeverity(referenceCase.Severity),
                    Stability = NormalizeCaseStability(referenceCase.Stability),
                    ReportRunId = report.RunId,
                    Status = "matched"
                };
                if (!IsErrorHandlingReport(referenceCase, report) &&
                    !IsSkipReport(report))
                {
                    ValidateSource(validation, status, referenceCase.Expected, report);
                }

                if (IsSkipReport(report))
                {
                    ValidateSkipSignals(validation, status, entry);
                }
                else
                {
                    ValidateRequiredSignals(validation, status, referenceCase, entry);
                    ValidateStartupTransportCallEvidence(
                        validation,
                        status,
                        referenceCase,
                        report);
                }

                ValidateReportResult(validation, status, report);
                ValidateReportEnvironment(validation, status, report);
                ValidateExecutionEvidence(validation, status, referenceCase, report);
                ValidateFailureClasses(validation, status, report);
                ValidateFailureAreas(validation, status, report);

                if (status.Signals.Count > 0)
                {
                    status.Status = "mismatch";
                }
                else
                {
                    validation.MatchedCaseCount++;
                }

                validation.Cases.Add(status);
            }
        }

        private static void ValidateReportResult(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            PlaybackQualityReport report)
        {
            if (PlaybackQualityReportResult.IsKnown(report.Result))
            {
                return;
            }

            AddUnique(status.Signals, "result");
            AddError(
                validation,
                "report.result.invalid",
                status.CaseId,
                status.ReportRunId,
                "result",
                string.Join(", ", PlaybackQualityReportResult.KnownResults),
                report.Result,
                "Playback quality report contains an unknown result.");
        }

        private static void ValidateReportEnvironment(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            PlaybackQualityReport report)
        {
            var environment = report.Environment ?? new PlaybackQualityEnvironment();
            ValidateRequiredEnvironmentSignal(
                validation,
                status,
                "environment.playerCoreVersion",
                environment.PlayerCoreVersion);
            ValidateRequiredEnvironmentSignal(
                validation,
                status,
                "environment.sourceRevision",
                environment.SourceRevision);
        }

        private static void ValidateExecutionEvidence(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            PlaybackQualityReferenceCase referenceCase,
            PlaybackQualityReport report)
        {
            var requiredLevel = referenceCase.ExecutionRequirement?.MinimumEvidenceLevel ??
                PlaybackQualityEvidenceLevel.NativePlayback;
            var execution = report.Execution ?? new PlaybackQualityExecutionEvidence();
            var actualLevel = execution.EvidenceLevel ?? "";
            if (!PlaybackQualityEvidenceLevel.MeetsMinimum(actualLevel, requiredLevel))
            {
                AddExecutionError(
                    validation,
                    status,
                    "report.execution.evidence-level.insufficient",
                    "execution.evidenceLevel",
                    requiredLevel,
                    string.IsNullOrWhiteSpace(actualLevel) ? "missing" : actualLevel,
                    "Playback quality report does not meet the reference case execution evidence requirement.");
                return;
            }

            if (string.IsNullOrWhiteSpace(execution.AttemptId))
            {
                AddExecutionError(validation, status, "report.execution.attempt-id.missing", "execution.attemptId", "present", "missing", "Playback quality report is missing execution attempt identity.");
            }

            if (string.IsNullOrWhiteSpace(execution.Runner))
            {
                AddExecutionError(validation, status, "report.execution.runner.missing", "execution.runner", "present", "missing", "Playback quality report is missing execution runner identity.");
            }

            var requiredScenario = referenceCase.ExecutionRequirement?.Scenario ?? "";
            if (!string.Equals(execution.Scenario, requiredScenario, StringComparison.Ordinal))
            {
                AddExecutionError(
                    validation,
                    status,
                    "report.execution.scenario.mismatch",
                    "execution.scenario",
                    requiredScenario,
                    string.IsNullOrWhiteSpace(execution.Scenario) ? "missing" : execution.Scenario,
                    "Playback quality report execution scenario does not match the reference case.");
            }

            if (!PlaybackQualityExecutionStatus.IsKnown(execution.Status))
            {
                AddExecutionError(validation, status, "report.execution.status.invalid", "execution.status", "known execution status", string.IsNullOrWhiteSpace(execution.Status) ? "missing" : execution.Status, "Playback quality report contains an unknown execution status.");
            }

            ValidateExecutionStatusForResult(validation, status, referenceCase, report.Result, execution.Status);

            if (!DateTimeOffset.TryParse(
                    execution.StartedAtUtc,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var startedAt) ||
                startedAt.Offset != TimeSpan.Zero)
            {
                AddExecutionError(validation, status, "report.execution.started-at.invalid", "execution.startedAtUtc", "UTC timestamp", string.IsNullOrWhiteSpace(execution.StartedAtUtc) ? "missing" : execution.StartedAtUtc, "Playback quality execution start time is missing or is not a UTC timestamp.");
            }

            if (!double.IsFinite(execution.DurationMs) || execution.DurationMs < 0)
            {
                AddExecutionError(validation, status, "report.execution.duration.invalid", "execution.durationMs", "finite value greater than or equal to zero", execution.DurationMs.ToString(System.Globalization.CultureInfo.InvariantCulture), "Playback quality execution duration is invalid.");
            }

            if (RequiresCompletedPlaybackSample(report.Result) &&
                (!double.IsFinite(execution.RequestedSampleDurationMs) ||
                 execution.RequestedSampleDurationMs <= 0))
            {
                AddExecutionError(
                    validation,
                    status,
                    "report.execution.requested-sample-duration.invalid",
                    "execution.requestedSampleDurationMs",
                    "finite value greater than zero",
                    execution.RequestedSampleDurationMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "Completed playback evidence must record the requested observation window.");
            }

            if (RequiresCompletedPlaybackSample(report.Result) &&
                execution.RequestedSampleDurationMs > 0 &&
                report.Source.FrameRate > 0)
            {
                var observedDurationMs =
                    PlaybackQualitySampleWindowPolicy.GetObservedMediaDurationMs(report);
                var requiredDurationMs =
                    PlaybackQualitySampleWindowPolicy.GetRequiredMediaDurationMs(report);
                var boundaryToleranceMs =
                    PlaybackQualitySampleWindowPolicy.GetCaptureBoundaryToleranceMs(report);
                var hasCompleteWallClockObservation = true;
                if (!double.IsFinite(execution.ObservedSampleWallClockDurationMs) ||
                    execution.ObservedSampleWallClockDurationMs <= 0)
                {
                    hasCompleteWallClockObservation = false;
                    AddExecutionError(
                        validation,
                        status,
                        "report.execution.sample-wall-clock.invalid",
                        "execution.observedSampleWallClockDurationMs",
                        "finite value greater than zero",
                        execution.ObservedSampleWallClockDurationMs.ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        "Completed playback evidence must record the observed wall-clock sample duration.");
                }
                else if (execution.ObservedSampleWallClockDurationMs +
                    boundaryToleranceMs < requiredDurationMs)
                {
                    hasCompleteWallClockObservation = false;
                    AddExecutionError(
                        validation,
                        status,
                        "report.execution.sample-wall-clock.incomplete",
                        "execution.observedSampleWallClockDurationMs",
                        requiredDurationMs.ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        execution.ObservedSampleWallClockDurationMs.ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        "The collector did not observe playback for the requested wall-clock window.");
                }

                if (hasCompleteWallClockObservation &&
                    observedDurationMs + boundaryToleranceMs < requiredDurationMs)
                {
                    if (!PlaybackQualitySampleWindowPolicy.HasMatchingIncompleteSampleFailure(
                            report,
                            requiredDurationMs,
                            observedDurationMs))
                    {
                        AddExecutionError(
                            validation,
                            status,
                            "report.execution.sample-window.incomplete",
                            "execution.requestedSampleDurationMs",
                            requiredDurationMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            observedDurationMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            "Rendered playback evidence did not cover the requested observation window and the report did not classify the playback failure.");
                    }
                }
            }

            var expectedLocatorHash = PlaybackQualitySourceFingerprint.Compute(referenceCase.Uri);
            if (!string.Equals(execution.SourceLocatorHash, expectedLocatorHash, StringComparison.Ordinal))
            {
                AddExecutionError(validation, status, "report.execution.source-locator-hash.mismatch", "execution.sourceLocatorHash", expectedLocatorHash, string.IsNullOrWhiteSpace(execution.SourceLocatorHash) ? "missing" : execution.SourceLocatorHash, "Playback quality report execution source does not match the reference case locator.");
            }

            if (!execution.SourceOpenAttempted)
            {
                AddExecutionError(validation, status, "report.execution.source-open-attempt.missing", "execution.sourceOpenAttempted", "true", "false", "Native playback evidence does not show a source-open attempt.");
            }

            if (execution.SourceOpened && string.IsNullOrWhiteSpace(execution.OpenedSourceHash))
            {
                AddExecutionError(validation, status, "report.execution.opened-source-hash.missing", "execution.openedSourceHash", "present when sourceOpened is true", "missing", "Opened media source identity is missing from native playback evidence.");
            }

            if (execution.SourceOpened &&
                !string.IsNullOrWhiteSpace(execution.OpenedSourceHash) &&
                string.Equals(
                    execution.OpenedSourceHash,
                    execution.SourceLocatorHash,
                    StringComparison.Ordinal))
            {
                AddExecutionError(
                    validation,
                    status,
                    "report.execution.opened-source-hash.aliases-locator",
                    "execution.openedSourceHash",
                    "observed media signature distinct from source locator hash",
                    execution.OpenedSourceHash,
                    "Opened media identity aliases the manifest locator and does not prove which media was parsed.");
            }

            if (execution.SourceOpened &&
                !string.Equals(
                    execution.OpenedSourceHashKind,
                    PlaybackQualitySourceFingerprint.OpenedMediaSignatureKind,
                    StringComparison.Ordinal))
            {
                AddExecutionError(
                    validation,
                    status,
                    "report.execution.opened-source-hash-kind.invalid",
                    "execution.openedSourceHashKind",
                    PlaybackQualitySourceFingerprint.OpenedMediaSignatureKind,
                    string.IsNullOrWhiteSpace(execution.OpenedSourceHashKind) ? "missing" : execution.OpenedSourceHashKind,
                    "Opened media identity must declare the observed signature algorithm used by the native playback evidence.");
            }

            if (!RequiresCompletedPlaybackSample(report.Result))
            {
                return;
            }

            RequireExecutionStage(validation, status, execution.SourceOpened, "report.execution.source-opened.missing", "execution.sourceOpened", "Completed playback result did not open the media source.");
            RequireExecutionStage(validation, status, execution.NativeGraphOpened, "report.execution.native-graph-opened.missing", "execution.nativeGraphOpened", "Completed playback result did not open the native playback graph.");
            RequireExecutionStage(validation, status, execution.DemuxStarted, "report.execution.demux-started.missing", "execution.demuxStarted", "Completed playback result did not start demuxing.");
            RequireExecutionStage(validation, status, execution.DecoderOpened, "report.execution.decoder-opened.missing", "execution.decoderOpened", "Completed playback result did not open a decoder.");
            RequireExecutionStage(validation, status, execution.PlaybackSampleObserved, "report.execution.playback-sample.missing", "execution.playbackSampleObserved", "Completed playback result is missing an observed playback sample.");
            RequireExecutionStage(validation, status, report.RuntimeMetrics?.HasPlaybackSample == true, "report.execution.runtime-playback-sample.missing", "runtimeMetrics.hasPlaybackSample", "Runtime metrics do not confirm the claimed playback sample.");
            RequireExecutionStage(validation, status, report.Timing?.DecodedVideoFrames > 0, "report.execution.decoded-frame.missing", "timing.decodedVideoFrames", "Completed video playback result has no decoded frame evidence.");
            RequireExecutionStage(validation, status, report.Timing?.RenderedVideoFrames > 0, "report.execution.rendered-frame.missing", "timing.renderedVideoFrames", "Completed video playback result has no rendered frame evidence.");
        }

        private static void ValidateExecutionStatusForResult(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            PlaybackQualityReferenceCase referenceCase,
            string result,
            string executionStatus)
        {
            if (string.Equals(result, PlaybackQualityReportResult.Skip, StringComparison.Ordinal) &&
                !string.Equals(NormalizeCaseCategory(referenceCase.Category), "quarantine", StringComparison.Ordinal))
            {
                AddExecutionError(
                    validation,
                    status,
                    "report.execution.stable-skip.not-allowed",
                    "execution.status",
                    "completed, failed, unsupported, cancelled, timed-out",
                    executionStatus,
                    "Stable and challenge playback cases cannot satisfy execution coverage with a skip result.");
                return;
            }

            string expectedStatus;
            var matches = false;
            switch (result)
            {
                case PlaybackQualityReportResult.Pass:
                case PlaybackQualityReportResult.Fail:
                    expectedStatus = PlaybackQualityExecutionStatus.Completed;
                    matches = executionStatus == PlaybackQualityExecutionStatus.Completed;
                    break;
                case PlaybackQualityReportResult.Error:
                    expectedStatus = "failed, timed-out, cancelled";
                    matches = executionStatus == PlaybackQualityExecutionStatus.Failed ||
                        executionStatus == PlaybackQualityExecutionStatus.TimedOut ||
                        executionStatus == PlaybackQualityExecutionStatus.Cancelled;
                    break;
                case PlaybackQualityReportResult.Unsupported:
                    expectedStatus = PlaybackQualityExecutionStatus.Unsupported;
                    matches = executionStatus == PlaybackQualityExecutionStatus.Unsupported;
                    break;
                case PlaybackQualityReportResult.Skip:
                    expectedStatus = PlaybackQualityExecutionStatus.Skipped;
                    matches = executionStatus == PlaybackQualityExecutionStatus.Skipped;
                    break;
                default:
                    return;
            }

            if (!matches)
            {
                AddExecutionError(
                    validation,
                    status,
                    "report.execution.status-result.mismatch",
                    "execution.status",
                    expectedStatus,
                    executionStatus,
                    "Playback quality result and execution status do not describe the same outcome.");
            }
        }

        private static void RequireExecutionStage(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            bool observed,
            string code,
            string signal,
            string message)
        {
            if (!observed)
            {
                AddExecutionError(validation, status, code, signal, "true", "false", message);
            }
        }

        private static void AddExecutionError(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            string code,
            string signal,
            string expected,
            string actual,
            string message)
        {
            AddUnique(status.Signals, signal);
            AddError(validation, code, status.CaseId, status.ReportRunId, signal, expected, actual, message);
        }

        private static void RecordExecutionCoverage(
            PlaybackQualityExecutionCoverage coverage,
            PlaybackQualityReport report)
        {
            var execution = report.Execution ?? new PlaybackQualityExecutionEvidence();
            if (execution.SourceOpenAttempted && !string.IsNullOrWhiteSpace(execution.AttemptId))
            {
                coverage.AttemptedCaseCount++;
            }

            if (execution.SourceOpened)
            {
                coverage.OpenedCaseCount++;
            }

            if (report.Timing?.DecodedVideoFrames > 0)
            {
                coverage.DecodedCaseCount++;
            }

            if (report.Timing?.RenderedVideoFrames > 0)
            {
                coverage.RenderedCaseCount++;
            }

            switch (execution.Status)
            {
                case PlaybackQualityExecutionStatus.Completed:
                    coverage.CompletedCaseCount++;
                    break;
                case PlaybackQualityExecutionStatus.Unsupported:
                    coverage.UnsupportedCaseCount++;
                    break;
                case PlaybackQualityExecutionStatus.Skipped:
                    coverage.SkippedCaseCount++;
                    break;
                case PlaybackQualityExecutionStatus.Failed:
                case PlaybackQualityExecutionStatus.Cancelled:
                case PlaybackQualityExecutionStatus.TimedOut:
                    coverage.FailedCaseCount++;
                    break;
            }
        }

        private static bool RequiresCompletedPlaybackSample(string result)
        {
            return string.Equals(result, PlaybackQualityReportResult.Pass, StringComparison.Ordinal) ||
                string.Equals(result, PlaybackQualityReportResult.Fail, StringComparison.Ordinal);
        }

        private static void ValidateRequiredEnvironmentSignal(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            string signal,
            string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            AddUnique(status.Signals, signal);
            AddError(
                validation,
                "report.environment.missing",
                status.CaseId,
                status.ReportRunId,
                signal,
                "present",
                "missing",
                "Playback quality report is missing required player identity.");
        }

        private static void ValidateFailureAreas(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            PlaybackQualityReport report)
        {
            ValidateFailureArea(
                validation,
                status,
                "analysis.primaryFailureArea",
                report.Analysis.PrimaryFailureArea);
            ValidateFailureArea(
                validation,
                status,
                "error.failureArea",
                report.Error.FailureArea);
            ValidateFailureArea(
                validation,
                status,
                "skip.failureArea",
                report.Skip.FailureArea);
            foreach (var check in report.Checks)
            {
                ValidateFailureArea(
                    validation,
                    status,
                    "checks.failureArea",
                    check.FailureArea);
            }
        }

        private static void ValidateFailureArea(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            string signal,
            string failureArea)
        {
            if (string.IsNullOrWhiteSpace(failureArea) ||
                PlaybackQualityCodeTargetCatalog.IsKnownFailureArea(failureArea))
            {
                return;
            }

            AddUnique(status.Signals, signal);
            AddError(
                validation,
                "report.failureArea.invalid",
                status.CaseId,
                status.ReportRunId,
                signal,
                string.Join(", ", PlaybackQualityCodeTargetCatalog.KnownFailureAreas),
                failureArea,
                "Playback quality report contains an unknown failure area.");
        }

        private static void ValidateFailureClasses(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            PlaybackQualityReport report)
        {
            ValidateFailureClass(
                validation,
                status,
                "error.failureClass",
                report.Error.FailureClass);
            ValidateFailureClass(
                validation,
                status,
                "skip.failureClass",
                report.Skip.FailureClass);
            foreach (var check in report.Checks)
            {
                ValidateFailureClass(
                    validation,
                    status,
                    "checks.failureClass",
                    check.FailureClass);
            }
        }

        private static void ValidateFailureClass(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            string signal,
            string failureClass)
        {
            if (string.IsNullOrWhiteSpace(failureClass) ||
                PlaybackQualityFailureClassification.IsKnown(failureClass))
            {
                return;
            }

            AddUnique(status.Signals, signal);
            AddError(
                validation,
                "report.failureClass.invalid",
                status.CaseId,
                status.ReportRunId,
                signal,
                string.Join(", ", PlaybackQualityFailureClassification.KnownFailureClasses),
                failureClass,
                "Playback quality report contains an unknown failure class.");
        }

        private static string NormalizeCaseCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? "stable" : category;
        }

        private static string NormalizeCaseSeverity(string severity)
        {
            return string.IsNullOrWhiteSpace(severity) ? "medium" : severity;
        }

        private static string NormalizeCaseStability(string stability)
        {
            return string.IsNullOrWhiteSpace(stability) ? "stable" : stability;
        }

        private static bool IsErrorHandlingReport(
            PlaybackQualityReferenceCase referenceCase,
            PlaybackQualityReport report)
        {
            return referenceCase != null &&
                report != null &&
                string.Equals(report.Result, "error", StringComparison.OrdinalIgnoreCase) &&
                referenceCase.Purpose.Contains("error-handling");
        }

        private static bool IsSkipReport(PlaybackQualityReport report)
        {
            return report != null &&
                string.Equals(report.Result, "skip", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateSource(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            PlaybackQualityExpected expected,
            PlaybackQualityReport report)
        {
            if (expected == null)
            {
                return;
            }

            CheckString(
                validation,
                status,
                "source.codec",
                expected.Codec,
                report.Source.Codec,
                ignoreCase: true);
            CheckInt(
                validation,
                status,
                "source.width",
                expected.Width,
                report.Source.Width);
            CheckInt(
                validation,
                status,
                "source.height",
                expected.Height,
                report.Source.Height);
            CheckFrameRate(validation, status, expected.FrameRate, report.Source.FrameRate);
            CheckString(
                validation,
                status,
                "source.hdrKind",
                expected.HdrKind,
                report.Source.HdrKind,
                ignoreCase: false);
            CheckString(
                validation,
                status,
                "source.hdrPlaybackStrategy",
                expected.HdrPlaybackStrategy,
                report.Source.HdrPlaybackStrategy,
                ignoreCase: false);
            CheckBool(
                validation,
                status,
                "source.isHdr",
                expected.IsHdr,
                report.Source.IsHdr);
            CheckBool(
                validation,
                status,
                "source.isDirectPlayable",
                expected.IsDirectPlayable,
                report.Source.IsDirectPlayable);
            CheckBool(
                validation,
                status,
                "source.isDolbyVision",
                expected.IsDolbyVision,
                report.Source.IsDolbyVision);
            CheckNullableInt(
                validation,
                status,
                "source.dolbyVisionProfile",
                expected.DolbyVisionProfile,
                report.Source.DolbyVisionProfile);
            CheckNullableInt(
                validation,
                status,
                "source.dolbyVisionCompatibilityId",
                expected.DolbyVisionCompatibilityId,
                report.Source.DolbyVisionCompatibilityId);
            CheckBool(
                validation,
                status,
                "source.hasHdr10BaseLayer",
                expected.HasHdr10BaseLayer,
                report.Source.HasHdr10BaseLayer);
            CheckBool(
                validation,
                status,
                "source.hasHlgBaseLayer",
                expected.HasHlgBaseLayer,
                report.Source.HasHlgBaseLayer);
        }

        private static void ValidateRequiredSignals(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            PlaybackQualityReferenceCase referenceCase,
            PlaybackQualityReferenceReportSetEntry entry)
        {
            foreach (var signal in PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(
                referenceCase,
                entry.Report.Result))
            {
                if (PlaybackQualityRequiredSignalPolicy.RequiresNativePlaybackEvidence(signal) &&
                    !PlaybackQualityEvidenceLevel.MeetsMinimum(
                        entry.Report.Execution?.EvidenceLevel ?? "",
                        PlaybackQualityEvidenceLevel.NativePlayback))
                {
                    continue;
                }

                if (status.Signals.Contains(signal) ||
                    PlaybackQualityRequiredSignalPolicy.HasReportSignal(
                        entry.Report,
                        signal,
                        entry.HasSignalPresenceEvidence ? entry.PresentSignals : null))
                {
                    continue;
                }

                AddUnique(status.Signals, signal);
                AddError(
                    validation,
                    "report.requiredSignal.missing",
                    status.CaseId,
                    status.ReportRunId,
                    signal,
                    "present",
                    "",
                    "Playback quality report is missing a required telemetry signal.");
            }
        }

        private static void ValidateSkipSignals(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            PlaybackQualityReferenceReportSetEntry entry)
        {
            foreach (var signal in RequiredSkipSignals)
            {
                if (status.Signals.Contains(signal) ||
                    PlaybackQualityRequiredSignalPolicy.HasReportSignal(
                        entry.Report,
                        signal,
                        entry.HasSignalPresenceEvidence ? entry.PresentSignals : null))
                {
                    continue;
                }

                AddUnique(status.Signals, signal);
                AddError(
                    validation,
                    "report.requiredSignal.missing",
                    status.CaseId,
                    status.ReportRunId,
                    signal,
                    "present",
                    "",
                    "Playback quality skip report is missing a required skip signal.");
            }
        }

        private static void CheckString(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            string signal,
            string expected,
            string actual,
            bool ignoreCase)
        {
            if (string.IsNullOrWhiteSpace(expected))
            {
                return;
            }

            var comparison = ignoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (string.Equals(expected, actual, comparison))
            {
                return;
            }

            AddUnique(status.Signals, signal);
            AddMismatch(validation, status, signal, expected, actual);
        }

        private static void CheckInt(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            string signal,
            int expected,
            int actual)
        {
            if (expected <= 0 || expected == actual)
            {
                return;
            }

            AddUnique(status.Signals, signal);
            AddMismatch(
                validation,
                status,
                signal,
                expected.ToString(System.Globalization.CultureInfo.InvariantCulture),
                actual.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void CheckNullableInt(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            string signal,
            int? expected,
            int? actual)
        {
            if (!expected.HasValue || expected == actual)
            {
                return;
            }

            AddUnique(status.Signals, signal);
            AddMismatch(
                validation,
                status,
                signal,
                expected.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                actual.HasValue ? actual.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");
        }

        private static void CheckBool(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            string signal,
            bool? expected,
            bool actual)
        {
            if (!expected.HasValue || expected.Value == actual)
            {
                return;
            }

            AddUnique(status.Signals, signal);
            AddMismatch(
                validation,
                status,
                signal,
                expected.Value.ToString(),
                actual.ToString());
        }

        private static void CheckFrameRate(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            double expected,
            double actual)
        {
            if (expected <= 0 || Math.Abs(expected - actual) <= FrameRateTolerance)
            {
                return;
            }

            AddUnique(status.Signals, "source.frameRate");
            AddMismatch(
                validation,
                status,
                "source.frameRate",
                expected.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                actual.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void AddMismatch(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            string signal,
            string expected,
            string actual)
        {
            AddError(
                validation,
                "report." + signal + ".mismatch",
                status.CaseId,
                status.ReportRunId,
                signal,
                expected,
                actual,
                "Playback quality report source metadata did not match reference case.");
        }

        private static void AddExtraReports(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceManifest manifest,
            List<PlaybackQualityReferenceReportSetEntry> entries)
        {
            var caseIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var referenceCase in manifest.Cases)
            {
                caseIds.Add(referenceCase.CaseId ?? "");
            }

            foreach (var entry in entries)
            {
                var report = entry.Report;
                var runId = report.RunId ?? "";
                if (caseIds.Contains(runId))
                {
                    continue;
                }

                validation.Cases.Add(new PlaybackQualityReferenceReportCaseStatus
                {
                    ReportRunId = runId,
                    Status = "extra"
                });
                AddError(
                    validation,
                    "report.extra",
                    "",
                    runId,
                    "runId",
                    "reference case id",
                    runId,
                    "Playback quality report does not match any reference case.");
            }
        }

        private static void AddError(
            PlaybackQualityReferenceReportSetValidation validation,
            string code,
            string caseId,
            string reportRunId,
            string signal,
            string expected,
            string actual,
            string message)
        {
            var triage = CreateSignalTriage(signal);
            var error = new PlaybackQualityReferenceReportSetError
            {
                Code = code,
                CaseId = caseId,
                ReportRunId = reportRunId,
                Signal = signal,
                Expected = expected,
                Actual = actual,
                Message = message,
                FailureArea = triage.FailureArea,
                FailureClass = ClassifyReportSetFailure(code, signal),
                SuggestedNextAction = triage.SuggestedNextAction
            };
            foreach (var target in triage.CodeTargets)
            {
                AddUnique(error.CodeTargets, target);
            }

            validation.Errors.Add(error);
            if (code.StartsWith("report.execution.", StringComparison.Ordinal) ||
                string.Equals(code, "report.missing", StringComparison.Ordinal))
            {
                validation.ExecutionValid = false;
            }
            else
            {
                validation.StructureValid = false;
            }
        }

        private static void ValidateStartupTransportCallEvidence(
            PlaybackQualityReferenceReportSetValidation validation,
            PlaybackQualityReferenceReportCaseStatus status,
            PlaybackQualityReferenceCase referenceCase,
            PlaybackQualityReport report)
        {
            var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(
                referenceCase,
                report.Result);
            var contractRequired = false;
            foreach (var signal in requiredSignals)
            {
                if (PlaybackQualityStartupTransportCallEvidence.TryParseSignal(
                    signal,
                    out _,
                    out _))
                {
                    contractRequired = true;
                    break;
                }
            }

            if (!contractRequired)
            {
                return;
            }

            foreach (var componentName in PlaybackQualityStartupTransportCallEvidence.ComponentNames)
            {
                var component = PlaybackQualityStartupTransportCallEvidence.FindComponent(
                    report,
                    componentName);
                if (component == null)
                {
                    continue;
                }

                var signal = PlaybackQualityStartupTransportCallEvidence.CreateSignal(
                    componentName,
                    "transportCallEvidenceStatus");
                if (!PlaybackQualityStartupTransportCallEvidence.HasConsistentContract(component))
                {
                    AddUnique(status.Signals, signal);
                    AddError(
                        validation,
                        "report.startup.transport-call.contract.invalid",
                        status.CaseId,
                        status.ReportRunId,
                        signal,
                        "provider, status, and callback metrics must be internally consistent",
                        component.TransportProvider + "/" + component.TransportCallEvidenceStatus,
                        "Native startup transport-call evidence is contradictory or invalid.");
                    continue;
                }
            }
        }

        private static string ClassifyReportSetFailure(string code, string signal)
        {
            if (string.Equals(code, "report.requiredSignal.missing", StringComparison.Ordinal) ||
                string.Equals(code, "report.environment.missing", StringComparison.Ordinal) ||
                code.StartsWith("report.execution.", StringComparison.Ordinal))
            {
                return PlaybackQualityFailureClassification.InsufficientInstrumentation;
            }

            if (string.Equals(code, "report.missing", StringComparison.Ordinal))
            {
                return PlaybackQualityFailureClassification.EnvironmentIssue;
            }

            if (string.Equals(code, "report.duplicate-run-id", StringComparison.Ordinal) ||
                string.Equals(code, "report.extra", StringComparison.Ordinal) ||
                string.Equals(code, "report.result.invalid", StringComparison.Ordinal) ||
                string.Equals(code, "report.failureArea.invalid", StringComparison.Ordinal) ||
                string.Equals(code, "report.failureClass.invalid", StringComparison.Ordinal))
            {
                return PlaybackQualityFailureClassification.EvaluationHarnessBug;
            }

            if (StartsWithSignal(signal, "source."))
            {
                return PlaybackQualityFailureClassification.ExternalServiceOrProtocolIssue;
            }

            return PlaybackQualityFailureClassification.NeedsHumanConfirmation;
        }

        private static PlaybackQualityReportSetSignalTriage CreateSignalTriage(string signal)
        {
            if (StartsWithSignal(signal, "source."))
            {
                return NewTriage(
                    "unsupported-source",
                    "Verify media source selection, codec support, HDR/Dolby Vision classification, and fallback policy before tuning playback timing.",
                    PlaybackQualityCodeTargetCatalog.GetForFailureArea("unsupported-source"));
            }

            if (StartsWithSignal(signal, "error."))
            {
                return NewTriage(
                    "error-handling",
                    "Collect runtime open, cancel, timeout, source availability, and native error evidence before interpreting playback-quality metrics.",
                    PlaybackQualityCodeTargetCatalog.GetForFailureArea("error-handling"));
            }

            if (StartsWithSignal(signal, "startup."))
            {
                return NewTriage(
                    "startup",
                    "Separate Emby request latency, native open/demux initialization, and first-frame readiness before changing render pacing.",
                    PlaybackQualityCodeTargetCatalog.GetForFailureArea("startup"));
            }

            if (StartsWithSignal(signal, "position."))
            {
                return NewTriage(
                    "timeline",
                    "Collect seek target, actual playback position, and derived position error before changing seek or resume behavior.",
                    PlaybackQualityCodeTargetCatalog.GetForFailureArea("timeline"));
            }

            if (StartsWithSignal(signal, "timing.") ||
                string.Equals(signal, "display.refreshRateHz", StringComparison.Ordinal))
            {
                return NewTriage(
                    "frame-pacing",
                    "Collect render intervals, frame gaps, source/display cadence, and wait/drop threshold evidence before changing frame pacing behavior.",
                    PlaybackQualityCodeTargetCatalog.GetForFailureArea("frame-pacing"));
            }

            if (StartsWithSignal(signal, "sync."))
            {
                return NewTriage(
                    "av-sync",
                    "Inspect XAudio clock derivation, queued buffer depth, video PTS comparison, and audio-wait policy.",
                    PlaybackQualityCodeTargetCatalog.GetForFailureArea("av-sync"));
            }

            if (StartsWithSignal(signal, "buffers."))
            {
                return NewTriage(
                    "buffering",
                    "Inspect demux, network, decode starvation, and audio queue depth before changing frame drop thresholds.",
                    PlaybackQualityCodeTargetCatalog.GetForFailureArea("buffering"));
            }

            if (StartsWithSignal(signal, "readRecovery."))
            {
                return NewTriage(
                    "buffering",
                    "Inspect FFmpeg read errors, bounded retry decisions, and recovery budget exhaustion.",
                    PlaybackQualityCodeTargetCatalog.GetForFailureArea("buffering"));
            }

            if (StartsWithSignal(signal, "colorPipeline.") ||
                string.Equals(signal, "display.hdrStatus", StringComparison.Ordinal))
            {
                return NewTriage(
                    "color-pipeline",
                    "Collect display HDR state, swapchain format/color space, DXGI mapping, and conversion validation before optimizing color pipeline behavior.",
                    PlaybackQualityCodeTargetCatalog.GetForFailureArea("color-pipeline"));
            }

            return NewTriage(
                "evidence-collection",
                "Collect missing telemetry before optimizing playback behavior; absent evidence is separate from a real playback failure.",
                PlaybackQualityCodeTargetCatalog.GetForFailureArea("evidence-collection"));
        }

        private static bool StartsWithSignal(string signal, string prefix)
        {
            return !string.IsNullOrWhiteSpace(signal) &&
                signal.StartsWith(prefix, StringComparison.Ordinal);
        }

        private static PlaybackQualityReportSetSignalTriage NewTriage(
            string failureArea,
            string suggestedNextAction,
            IEnumerable<string> codeTargets)
        {
            var triage = new PlaybackQualityReportSetSignalTriage
            {
                FailureArea = failureArea,
                SuggestedNextAction = suggestedNextAction
            };
            foreach (var target in codeTargets)
            {
                triage.CodeTargets.Add(target);
            }

            return triage;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }
    }

    internal sealed class PlaybackQualityReportSetSignalTriage
    {
        public string FailureArea { get; set; } = "";

        public string SuggestedNextAction { get; set; } = "";

        public List<string> CodeTargets { get; } = new List<string>();
    }
}
