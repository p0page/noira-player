using System;
using System.IO;
using System.Linq;
using NextGenEmby.Core.Diagnostics;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentDetailsFixtureTests
{
    [Fact]
    public void Create_Covers_Details_Decision_And_Below_Fold_Rails()
    {
        var fixture = DevelopmentDetailsFixture.Create();

        Assert.Equal("fixture-detail-aurora", fixture.Item.Id);
        Assert.Equal("Movie", fixture.Item.Type);
        Assert.NotEmpty(fixture.Item.Overview);
        Assert.True(fixture.Item.People.Count >= 3);
        Assert.True(fixture.MediaSources.Count >= 2);
        Assert.NotEmpty(fixture.OrganizeAncestors);
        Assert.NotEmpty(fixture.CollectionTargets);
        Assert.NotEmpty(fixture.PlaylistTargets);
        Assert.True(fixture.SimilarItems.Count >= 4);
    }

    [Fact]
    public void Create_Provides_Packaged_Artwork_For_Visible_Details_Surfaces()
    {
        var root = FindRepositoryRoot();
        var fixture = DevelopmentDetailsFixture.Create();
        var visibleArtworkKeys = new[]
            {
                DevelopmentDetailsFixture.ArtworkKey(fixture.Item.Id, "Primary"),
                DevelopmentDetailsFixture.ArtworkKey(fixture.Item.Id, "Backdrop")
            }
            .Concat(fixture.SimilarItems.Select(item => DevelopmentDetailsFixture.ArtworkKey(item.Id, "Primary")))
            .Concat(fixture.CollectionTargets.Select(item => DevelopmentDetailsFixture.ArtworkKey(item.Id, "Thumb")))
            .Concat(fixture.PlaylistTargets.Select(item => DevelopmentDetailsFixture.ArtworkKey(item.Id, "Thumb")))
            .Concat(fixture.Item.People.Select(person => DevelopmentDetailsFixture.ArtworkKey(person.Id, "Primary")))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(visibleArtworkKeys);
        foreach (var key in visibleArtworkKeys)
        {
            Assert.True(fixture.ArtworkUris.TryGetValue(key, out var uri), "Missing Details artwork URI for " + key);
            Assert.StartsWith("ms-appx:///Assets/QaHome/", uri, StringComparison.Ordinal);

            var relativePath = uri.Substring("ms-appx:///".Length).Replace('/', Path.DirectorySeparatorChar);
            var assetPath = Path.Combine(root, "src", "NextGenEmby.App", relativePath);
            Assert.True(File.Exists(assetPath), "Missing packaged QA artwork asset " + assetPath);
        }
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
