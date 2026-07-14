using System.Text.Json;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityReportSerializer
    {
        public static string Serialize(PlaybackQualityReport report)
        {
            return JsonSerializer.Serialize(report, PlaybackQualityJsonContext.Default.PlaybackQualityReport);
        }

        public static string Serialize(PlaybackQualityModelAnalysis analysis)
        {
            return JsonSerializer.Serialize(analysis, PlaybackQualityJsonContext.Default.PlaybackQualityModelAnalysis);
        }

        public static string Serialize(PlaybackQualityRunResult result)
        {
            return JsonSerializer.Serialize(result, PlaybackQualityJsonContext.Default.PlaybackQualityRunResult);
        }

        public static string Serialize(PlaybackQualityRunComparison comparison)
        {
            return JsonSerializer.Serialize(comparison, PlaybackQualityJsonContext.Default.PlaybackQualityRunComparison);
        }

        public static string Serialize(PlaybackQualityRenderPhaseComparison comparison)
        {
            return JsonSerializer.Serialize(
                comparison,
                PlaybackQualityJsonContext.Default.PlaybackQualityRenderPhaseComparison);
        }

        public static string Serialize(PlaybackQualityComparisonSuite suite)
        {
            return JsonSerializer.Serialize(suite, PlaybackQualityJsonContext.Default.PlaybackQualityComparisonSuite);
        }

        public static PlaybackQualityReport Deserialize(string json)
        {
            return JsonSerializer.Deserialize(json, PlaybackQualityJsonContext.Default.PlaybackQualityReport) ??
                new PlaybackQualityReport();
        }
    }
}
