using System.IO;
using System.Linq;
using NextGenEmby.Core.Diagnostics;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentLiveTvFixtureTests
{
    [Fact]
    public void Create_Provides_Channels_Current_Programs_And_Artwork()
    {
        var fixture = DevelopmentLiveTvFixture.Create();

        Assert.True(fixture.Channels.Count >= 4);
        Assert.Contains(fixture.Channels, channel => channel.Number == "101" && channel.CurrentProgram != null);
        Assert.Contains(fixture.Channels, channel => channel.CurrentProgram != null && channel.CurrentProgram.IsNews);
        Assert.Contains(fixture.Channels, channel => channel.CurrentProgram != null && channel.CurrentProgram.IsSports);
        Assert.Contains(fixture.Channels, channel => channel.CurrentProgram != null && channel.CurrentProgram.IsKids);
        Assert.NotEmpty(fixture.ArtworkUris);
    }

    [Fact]
    public void Create_Provides_Current_Program_Artwork_For_Media_First_Preview()
    {
        var fixture = DevelopmentLiveTvFixture.Create();

        foreach (var channel in fixture.Channels)
        {
            Assert.NotNull(channel.CurrentProgram);
            var program = channel.CurrentProgram!;
            Assert.False(string.IsNullOrWhiteSpace(program.Id));
            Assert.False(string.IsNullOrWhiteSpace(program.ThumbImageTag));
            Assert.True(
                fixture.ArtworkUris.ContainsKey(DevelopmentLiveTvFixture.ArtworkKey(program.Id, "Thumb")),
                "Missing current-program Thumb artwork for " + program.Id);
        }
    }

    [Fact]
    public void ArtworkUris_Point_To_Packaged_Qa_Assets()
    {
        var fixture = DevelopmentLiveTvFixture.Create();
        var root = FindRepositoryRoot();
        var expectedKeys = fixture.Channels
            .Select(channel => DevelopmentLiveTvFixture.ArtworkKey(channel.Id, "Primary"))
            .Concat(fixture.Channels
                .Where(channel => channel.CurrentProgram != null)
                .Select(channel => DevelopmentLiveTvFixture.ArtworkKey(channel.CurrentProgram!.Id, "Thumb")))
            .ToList();

        foreach (var key in expectedKeys)
        {
            Assert.True(fixture.ArtworkUris.TryGetValue(key, out var uri), "Missing fixture artwork URI for " + key);
            var relativeAsset = uri.Replace("ms-appx:///", "").Replace('/', Path.DirectorySeparatorChar);
            var assetPath = Path.Combine(root, "src", "NextGenEmby.App", relativeAsset);
            Assert.True(File.Exists(assetPath), "Missing packaged QA artwork asset " + assetPath);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "Generate-AppIconAssets.ps1")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
