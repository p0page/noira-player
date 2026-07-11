using System.Text.Json.Serialization;

namespace NoiraPlayer.Core.PlaybackQuality
{
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        WriteIndented = true)]
    [JsonSerializable(typeof(PlaybackQualityReport))]
    [JsonSerializable(typeof(PlaybackQualityModelAnalysis))]
    [JsonSerializable(typeof(PlaybackQualityRunResult))]
    [JsonSerializable(typeof(PlaybackQualityRunComparison))]
    [JsonSerializable(typeof(PlaybackQualityComparisonSuite))]
    [JsonSerializable(typeof(PlaybackQualityExecutionEvidence))]
    [JsonSerializable(typeof(PlaybackQualityExecutionRequirement))]
    [JsonSerializable(typeof(PlaybackQualityExecutionCoverage))]
    internal sealed partial class PlaybackQualityJsonContext : JsonSerializerContext
    {
    }
}
