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

        public PlaybackQualityExpected? Expected { get; set; }
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
        public static PlaybackQualityRunResult Compose(PlaybackQualityReportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var report = new PlaybackQualityReport
            {
                RunId = request.RunId ?? "",
                Expected = request.Expected
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

            PlaybackQualityEvaluator.Evaluate(report);
            return new PlaybackQualityRunResult(
                report,
                PlaybackQualityReportAnalyzer.Analyze(report));
        }
    }
}
