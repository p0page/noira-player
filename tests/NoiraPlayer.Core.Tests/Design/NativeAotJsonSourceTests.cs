using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class NativeAotJsonSourceTests
{
    [Fact]
    public void Core_Json_Serialization_Uses_Source_Generated_Metadata()
    {
        var coreSource = ReadCoreSources();
        var embyApiClientSource = ReadCoreSource("Emby", "EmbyApiClient.cs");
        var playbackQualitySerializerSource = ReadCoreSource("PlaybackQuality", "PlaybackQualityReportSerializer.cs");

        Assert.DoesNotContain("JsonSerializer.Deserialize<", coreSource);
        Assert.DoesNotContain("JsonSerializer.Serialize(new {", coreSource);
        Assert.DoesNotContain("JsonSerializer.Serialize(body, _writeJsonOptions)", coreSource);
        Assert.DoesNotContain("new JsonStringEnumConverter()", coreSource);

        Assert.Contains("EmbyApiJsonContext", embyApiClientSource);
        Assert.Contains("DevelopmentDiagnosticsJsonContext", coreSource);
        Assert.Contains("PlaybackQualityJsonContext", playbackQualitySerializerSource);
        Assert.Contains("JsonSerializerContext", coreSource);
        Assert.Contains("JsonSerializable", coreSource);
    }

    private static string ReadCoreSources()
    {
        var root = Path.Combine(FindRepositoryRoot(), "src", "NoiraPlayer.Core");
        var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
        return string.Join(Environment.NewLine, Array.ConvertAll(sources, File.ReadAllText));
    }

    private static string ReadCoreSource(params string[] segments)
    {
        var parts = new string[segments.Length + 3];
        parts[0] = FindRepositoryRoot();
        parts[1] = "src";
        parts[2] = "NoiraPlayer.Core";
        Array.Copy(segments, 0, parts, 3, segments.Length);
        return File.ReadAllText(Path.Combine(parts));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "Generate-AppIconAssets.ps1")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
