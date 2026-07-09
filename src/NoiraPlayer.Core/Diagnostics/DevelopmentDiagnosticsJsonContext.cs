using System.Text.Json.Serialization;
using NoiraPlayer.Core.PlaybackQuality;

namespace NoiraPlayer.Core.Diagnostics
{
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(DevelopmentLoginCredentials))]
    [JsonSerializable(typeof(DevelopmentNavigationCommand))]
    [JsonSerializable(typeof(PlaybackQualityExpected))]
    internal sealed partial class DevelopmentDiagnosticsJsonContext : JsonSerializerContext
    {
    }
}
