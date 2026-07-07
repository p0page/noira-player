using System;
using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityReportRequest
    {
        public string RunId { get; set; } = "";

        public PlaybackQualityCaseMetadata? CaseMetadata { get; set; }

        public PlaybackDescriptor? Descriptor { get; set; }

        public PlaybackDisplayStatus? DisplayStatus { get; set; }

        public PlaybackQualityMetricsSnapshot? Metrics { get; set; }

        public PlaybackQualityStartup? Startup { get; set; }

        public PlaybackQualityEnvironment? Environment { get; set; }

        public PlaybackQualityExpected? Expected { get; set; }

        public bool UseDefaultExpectedWhenMissing { get; set; }
    }

    public sealed class PlaybackQualityCaseMetadata
    {
        public string CaseId { get; set; } = "";

        public string Category { get; set; } = "stable";

        public string Severity { get; set; } = "medium";

        public string Stability { get; set; } = "stable";
    }

    public sealed class PlaybackQualityRunResult
    {
        public const string CurrentEvaluationVersion = "playback-quality-v0.1";

        public PlaybackQualityRunResult(
            PlaybackQualityReport report,
            PlaybackQualityModelAnalysis modelAnalysis,
            PlaybackQualityCaseMetadata? caseMetadata = null)
        {
            Report = report ?? throw new ArgumentNullException(nameof(report));
            ModelAnalysis = modelAnalysis ?? throw new ArgumentNullException(nameof(modelAnalysis));
            CaseMetadata = caseMetadata ?? new PlaybackQualityCaseMetadata
            {
                CaseId = report.RunId
            };
        }

        public int SchemaVersion { get; set; } = 1;

        public string EvaluationVersion { get; set; } = CurrentEvaluationVersion;

        public PlaybackQualityCaseMetadata CaseMetadata { get; }

        public PlaybackQualityReport Report { get; }

        public PlaybackQualityModelAnalysis ModelAnalysis { get; }
    }

    public static class PlaybackQualityReportComposer
    {
        private const string CollectorVersionEnvironmentVariable =
            "NEXTGENEMBY_PLAYBACK_QUALITY_COLLECTOR_VERSION";
        private const string PlayerCoreVersionEnvironmentVariable =
            "NEXTGENEMBY_PLAYER_CORE_VERSION";
        private const string SourceRevisionEnvironmentVariable =
            "NEXTGENEMBY_SOURCE_REVISION";
        private const string BuildConfigurationEnvironmentVariable =
            "NEXTGENEMBY_BUILD_CONFIGURATION";

        public static PlaybackQualityRunResult Compose(PlaybackQualityReportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var expected = request.Expected ??
                (request.UseDefaultExpectedWhenMissing && request.Descriptor != null
                    ? PlaybackQualityExpectedFactory.CreateDefault(request.Descriptor)
                    : null);
            var report = new PlaybackQualityReport
            {
                RunId = request.RunId ?? "",
                Expected = expected
            };

            if (request.Descriptor != null)
            {
                PlaybackQualityReportMapper.ApplySource(report, request.Descriptor);
            }

            if (request.DisplayStatus != null)
            {
                PlaybackQualityReportMapper.ApplyDisplayStatus(report, request.DisplayStatus);
            }

            if (request.Metrics != null)
            {
                PlaybackQualityReportMapper.ApplyMetrics(report, request.Metrics);
            }

            if (request.Startup != null)
            {
                report.Startup = request.Startup;
            }

            if (request.Environment != null)
            {
                report.Environment = MergeEnvironment(request.Environment);
            }
            else
            {
                report.Environment = MergeEnvironment(null);
            }

            PlaybackQualityEvaluator.Evaluate(report);
            return new PlaybackQualityRunResult(
                report,
                PlaybackQualityReportAnalyzer.Analyze(report),
                CloneCaseMetadata(request.CaseMetadata, report.RunId));
        }

        private static PlaybackQualityCaseMetadata CloneCaseMetadata(
            PlaybackQualityCaseMetadata? source,
            string runId)
        {
            if (source == null)
            {
                return new PlaybackQualityCaseMetadata
                {
                    CaseId = runId ?? ""
                };
            }

            return new PlaybackQualityCaseMetadata
            {
                CaseId = string.IsNullOrWhiteSpace(source.CaseId) ? runId ?? "" : source.CaseId,
                Category = string.IsNullOrWhiteSpace(source.Category) ? "stable" : source.Category,
                Severity = string.IsNullOrWhiteSpace(source.Severity) ? "medium" : source.Severity,
                Stability = string.IsNullOrWhiteSpace(source.Stability) ? "stable" : source.Stability
            };
        }

        private static PlaybackQualityEnvironment MergeEnvironment(
            PlaybackQualityEnvironment? requested)
        {
            return new PlaybackQualityEnvironment
            {
                CollectorVersion = ValueOrEnvironment(
                    requested == null ? "" : requested.CollectorVersion,
                    CollectorVersionEnvironmentVariable),
                PlayerCoreVersion = ValueOrEnvironment(
                    requested == null ? "" : requested.PlayerCoreVersion,
                    PlayerCoreVersionEnvironmentVariable),
                SourceRevision = ValueOrEnvironment(
                    requested == null ? "" : requested.SourceRevision,
                    SourceRevisionEnvironmentVariable),
                BuildConfiguration = ValueOrEnvironment(
                    requested == null ? "" : requested.BuildConfiguration,
                    BuildConfigurationEnvironmentVariable)
            };
        }

        private static string ValueOrEnvironment(string value, string environmentVariable)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return Environment.GetEnvironmentVariable(environmentVariable) ?? "";
        }
    }
}
