using System;
using System.IO;
using System.Linq;
using NextGenEmby.Core.Diagnostics;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentSearchFixtureTests
{
    [Fact]
    public void CreateItemsForScope_Returns_Items_For_Every_Search_Surface()
    {
        foreach (var scope in EmbySearchScopePolicy.AllScopes)
        {
            var items = DevelopmentSearchFixture.CreateItemsForScope(scope.Key);

            Assert.NotEmpty(items);
        }
    }

    [Fact]
    public void CreateItemsForScope_Respects_Specific_Item_Type_Filters()
    {
        foreach (var scope in EmbySearchScopePolicy.AllScopes.Where(scope => scope.RequireItemTypeMatch))
        {
            var allowedTypes = scope.IncludeItemTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var items = DevelopmentSearchFixture.CreateItemsForScope(scope.Key);

            Assert.All(items, item => Assert.Contains(item.Type, allowedTypes));
        }
    }

    [Fact]
    public void CreateItemsForScope_LiveTv_Covers_Far_Right_Scope()
    {
        var items = DevelopmentSearchFixture.CreateItemsForScope("livetv");

        var item = Assert.Single(items);
        Assert.Equal("TvChannel", item.Type);
        Assert.Equal("News 24", item.Name);
    }

    [Fact]
    public void CreateItemsForScope_Provides_Packaged_Artwork_For_Result_Cards()
    {
        var root = FindRepositoryRoot();
        var artworkUris = DevelopmentSearchFixture.CreateArtworkUris();
        var items = DevelopmentSearchFixture.CreateItemsForScope("all")
            .Where(item => !string.IsNullOrWhiteSpace(item.PrimaryImageTag))
            .ToList();

        Assert.NotEmpty(items);
        foreach (var item in items)
        {
            var key = DevelopmentSearchFixture.ArtworkKey(item.Id, "Primary");
            Assert.True(artworkUris.TryGetValue(key, out var uri), "Missing search artwork URI for " + key);
            Assert.StartsWith("ms-appx:///Assets/QaHome/", uri, StringComparison.Ordinal);

            var relativePath = uri.Substring("ms-appx:///".Length).Replace('/', Path.DirectorySeparatorChar);
            var assetPath = Path.Combine(root, "src", "NextGenEmby.App", relativePath);
            Assert.True(File.Exists(assetPath), "Missing packaged QA artwork asset " + assetPath);

            Assert.Equal("qa", item.PrimaryImageTag);
            Assert.Equal(item.Id, item.PrimaryImageItemId);
        }
    }

    [Fact]
    public void CreateItemsForScope_Includes_NoArtwork_Result_For_Fallback_Validation()
    {
        var artworkUris = DevelopmentSearchFixture.CreateArtworkUris();
        var items = DevelopmentSearchFixture.CreateItemsForScope("movies");

        var item = Assert.Single(items, item => item.Id == "fixture-movie-no-artwork");

        Assert.Equal("No Poster Signal", item.Name);
        Assert.Equal("Movie", item.Type);
        Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageTag));
        Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageItemId));
        Assert.False(artworkUris.ContainsKey(DevelopmentSearchFixture.ArtworkKey(item.Id, "Primary")));
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
