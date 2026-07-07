using System;
using System.Collections.Generic;

namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityReferenceReportSetValidation
    {
        public int SchemaVersion { get; set; } = 1;

        public bool IsValid => Errors.Count == 0;

        public int ExpectedCaseCount { get; set; }

        public int ReportCount { get; set; }

        public int MatchedCaseCount { get; set; }

        public List<PlaybackQualityReferenceReportCaseStatus> Cases { get; } =
            new List<PlaybackQualityReferenceReportCaseStatus>();

        public List<PlaybackQualityReferenceReportSetError> Errors { get; } =
            new List<PlaybackQualityReferenceReportSetError>();
    }

    public sealed class PlaybackQualityReferenceReportCaseStatus
    {
        public string CaseId { get; set; } = "";

        public string Category { get; set; } = "stable";

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
                    validation.Cases.Add(new PlaybackQualityReferenceReportCaseStatus
                    {
                        CaseId = caseId,
                        Category = NormalizeCaseCategory(referenceCase.Category),
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
                var status = new PlaybackQualityReferenceReportCaseStatus
                {
                    CaseId = caseId,
                    Category = NormalizeCaseCategory(referenceCase.Category),
                    ReportRunId = report.RunId,
                    Status = "matched"
                };
                ValidateSource(validation, status, referenceCase.Expected, report);
                ValidateRequiredSignals(validation, status, referenceCase, entry);
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

        private static string NormalizeCaseCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? "stable" : category;
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
            foreach (var signal in PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase))
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
                    "Playback quality report is missing a required telemetry signal.");
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
                SuggestedNextAction = triage.SuggestedNextAction
            };
            foreach (var target in triage.CodeTargets)
            {
                AddUnique(error.CodeTargets, target);
            }

            validation.Errors.Add(error);
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
