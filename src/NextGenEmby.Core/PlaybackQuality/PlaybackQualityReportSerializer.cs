using System.Text.Json;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityReportSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public static string Serialize(PlaybackQualityReport report)
        {
            return JsonSerializer.Serialize(report, Options);
        }

        public static string Serialize(PlaybackQualityModelAnalysis analysis)
        {
            return JsonSerializer.Serialize(analysis, Options);
        }

        public static string Serialize(PlaybackQualityRunResult result)
        {
            return JsonSerializer.Serialize(result, Options);
        }

        public static string Serialize(PlaybackQualityRunComparison comparison)
        {
            return JsonSerializer.Serialize(comparison, Options);
        }

        public static PlaybackQualityReport Deserialize(string json)
        {
            return JsonSerializer.Deserialize<PlaybackQualityReport>(json, Options) ??
                new PlaybackQualityReport();
        }
    }
}
