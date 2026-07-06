using System;
using System.IO;
using System.Linq;
using NextGenEmby.Core.Diagnostics;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentHomeFixtureTests
{
    [Fact]
    public void Create_Covers_Representative_Tv_Home_Rails()
    {
        var fixture = DevelopmentHomeFixture.Create();

        Assert.NotEmpty(fixture.ContinueItems);
        Assert.NotEmpty(fixture.NextUpItems);
        Assert.True(fixture.LibraryViews.Count >= 5);
        Assert.Contains(fixture.ConfiguredRows, row => row.Title == "Hot Movies");
        Assert.Contains(fixture.ConfiguredRows, row => row.Title == "Hot TV Series");
        Assert.Contains(fixture.ConfiguredRows, row => row.Title == "Douban Top Rated");
        Assert.Contains(fixture.ConfiguredRows, row => row.Title == "Netflix");
        Assert.True(fixture.LatestItems.Count >= 8);
    }

    [Fact]
    public void Create_Provides_Packaged_Artwork_For_Visible_Home_Surfaces()
    {
        var root = FindRepositoryRoot();
        var fixture = DevelopmentHomeFixture.Create();
        var visibleArtworkKeys = fixture.LibraryViews
            .Select(view => DevelopmentHomeFixture.ArtworkKey(view.Id, "Thumb"))
            .Concat(fixture.ConfiguredRows.Select(row => DevelopmentHomeFixture.ArtworkKey(row.ParentItem.Id, "Thumb")))
            .Concat(fixture.ContinueItems.Take(1).Select(item => DevelopmentHomeFixture.ArtworkKey(item.Id, "Primary")))
            .Concat(fixture.ContinueItems.Take(1).Select(item => DevelopmentHomeFixture.ArtworkKey(item.Id, "Backdrop")))
            .Concat(fixture.LatestItems.Select(item => DevelopmentHomeFixture.ArtworkKey(item.Id, "Primary")))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(visibleArtworkKeys);

        foreach (var key in visibleArtworkKeys)
        {
            Assert.True(fixture.ArtworkUris.TryGetValue(key, out var uri), "Missing artwork URI for " + key);
            Assert.StartsWith("ms-appx:///Assets/QaHome/", uri, StringComparison.Ordinal);

            var relativePath = uri.Substring("ms-appx:///".Length).Replace('/', Path.DirectorySeparatorChar);
            var assetPath = Path.Combine(root, "src", "NextGenEmby.App", relativePath);
            Assert.True(File.Exists(assetPath), "Missing packaged QA artwork asset " + assetPath);
        }
    }

    [Fact]
    public void Create_Configured_Rows_Carry_Section_Owned_Artwork()
    {
        var fixture = DevelopmentHomeFixture.Create();
        var row = fixture.ConfiguredRows.Single(item => item.Title == "Hot Movies");
        var sectionProperty = row.GetType().GetProperty("Section");

        Assert.NotNull(sectionProperty);
        var section = Assert.IsType<EmbyHomeSection>(sectionProperty!.GetValue(row));
        Assert.Equal("qa-section-hot-movies", section.Id);
        Assert.Equal("qa", section.ThumbImageTag);
        Assert.Equal("qa-section-hot-movies", section.ThumbImageItemId);
        Assert.True(fixture.ArtworkUris.ContainsKey(DevelopmentHomeFixture.ArtworkKey(section.Id, "Thumb")));
    }

    [Fact]
    public void Qa_Artwork_Generator_Uses_Light_Global_Scrim()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "Generate-HomeQaArtworkAssets.ps1"));

        Assert.Contains("FromArgb(24, 0, 0, 0)", script);
        Assert.DoesNotContain("FromArgb(80, 0, 0, 0)", script);
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
