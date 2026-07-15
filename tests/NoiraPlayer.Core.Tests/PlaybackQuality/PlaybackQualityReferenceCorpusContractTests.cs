using System;
using System.IO;
using System.Text.Json;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityReferenceCorpusContractTests
{
    [Fact]
    public void Public_Corpus_Includes_Executable_Http_Timeline_Seek_Case()
    {
        var manifestPath = Path.Combine(
            FindRepositoryRoot(),
            "docs",
            "qa",
            "playback-quality-reference-manifest.example.json");
        var manifest = JsonSerializer.Deserialize<PlaybackQualityReferenceManifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Populate
            });

        var referenceCase = Assert.Single(manifest!.Cases, item =>
            item.CaseId == "jellyfin/hdr10-http-timeline-10s");
        Assert.Equal("challenge", referenceCase.Category);
        Assert.Equal("variable", referenceCase.Stability);
        Assert.Equal("timeline", referenceCase.ExecutionRequirement.Scenario);
        Assert.StartsWith("https://repo.jellyfin.org/", referenceCase.Uri, StringComparison.Ordinal);
        Assert.Equal(0, referenceCase.StartPositionTicks);
        Assert.Equal(100_000_000, referenceCase.SeekTargetPositionTicks);
        Assert.Contains("timeline", referenceCase.Purpose);
        Assert.NotNull(referenceCase.Expected.SdrDisplayFallback);
        Assert.Equal(5_000, referenceCase.Expected.MaxSeekRecoveryDurationMs);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NoiraPlayer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
