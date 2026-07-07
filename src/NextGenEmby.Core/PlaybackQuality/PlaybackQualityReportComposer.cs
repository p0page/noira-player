using System;
using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityReportRequest
    {
        public string RunId { get; set; } = "";

        public PlaybackDescriptor? Descriptor { get; set; }

        public PlaybackDisplayStatus? DisplayStatus { get; set; }

        public PlaybackQualityMetricsSnapshot? Metrics { get; set; }

        public PlaybackQualityStartup? Startup { get; set; }

        public PlaybackQualityEnvironment? Environment { get; set; }

        public PlaybackQualityExpected? Expected { get; set; }

        public bool UseDefaultExpectedWhenMissing { get; set; }
    }

    public sealed class PlaybackQualityRunResult
    {
        public PlaybackQualityRunResult(
            PlaybackQualityReport report,
            PlaybackQualityModelAnalysis modelAnalysis)
        {
            Report = report ?? throw new ArgumentNullException(nameof(report));
            ModelAnalysis = modelAnalysis ?? throw new ArgumentNullException(nameof(modelAnalysis));
        }

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
                PlaybackQualityReportAnalyzer.Analyze(report));
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
