using System.IO;
using System.Linq;
using NextGenEmby.Core.Diagnostics;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentPhotosFixtureTests
{
    [Fact]
    public void Create_Items_Contains_Root_Album_And_Photos()
    {
        var fixture = DevelopmentPhotosFixture.Create();

        Assert.Contains(fixture.Items, item => item.Id == "fixture-photo-album-night-market" && item.Type == "Folder");
        Assert.Contains(fixture.Items, item => item.Id == "fixture-photo-rooftop" && item.Type == "Photo" && item.ParentId == "");
        Assert.Contains(fixture.ArtworkUris.Keys, key => key == DevelopmentPhotosFixture.ArtworkKey("fixture-photo-rooftop", "Primary"));
    }

    [Fact]
    public void GetItemsForParent_Returns_Root_Items_When_Parent_Is_Blank()
    {
        var fixture = DevelopmentPhotosFixture.Create();

        var items = fixture.GetItemsForParent("");

        Assert.Contains(items, item => item.Id == "fixture-photo-album-night-market");
        Assert.Contains(items, item => item.Id == "fixture-photo-rooftop");
        Assert.DoesNotContain(items, item => item.ParentId == "fixture-photo-album-night-market");
    }

    [Fact]
    public void GetItemsForParent_Returns_Nested_Photos_For_Album()
    {
        var fixture = DevelopmentPhotosFixture.Create();

        var items = fixture.GetItemsForParent("fixture-photo-album-night-market");

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal("fixture-photo-album-night-market", item.ParentId));
        Assert.Contains(items, item => item.Id == "fixture-photo-lanterns");
    }

    [Fact]
    public void Create_Provides_Dense_Album_For_Photo_Grid_Wrap_Validation()
    {
        var fixture = DevelopmentPhotosFixture.Create();
        var album = fixture.Items.Single(item => item.Id == "fixture-photo-album-night-market");
        var albumItems = fixture.GetItemsForParent(album.Id);

        Assert.True(albumItems.Count >= 10, "Photos fixture album should be dense enough to validate grid wrap.");
        Assert.Equal(albumItems.Count, album.ChildCount);
        Assert.All(albumItems, item => Assert.Equal("Photo", item.Type));
        Assert.Equal(albumItems.Count, albumItems.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void ArtworkUris_Point_To_Packaged_Qa_Assets()
    {
        var fixture = DevelopmentPhotosFixture.Create();
        var root = FindRepositoryRoot();
        var expectedKeys = fixture.Items
            .Where(item => item.Type == "Photo" || item.Type == "Folder")
            .Select(item => DevelopmentPhotosFixture.ArtworkKey(item.Id, "Primary"))
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
