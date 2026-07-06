using System.IO;
using System.Linq;
using NextGenEmby.Core.Diagnostics;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentLibraryOrganizationFixtureTests
{
    [Fact]
    public void Create_Provides_Root_Collections_Playlists_And_Child_Items()
    {
        var fixture = DevelopmentLibraryOrganizationFixture.Create();

        Assert.Contains(fixture.Items, item => item.Id == "fixture-collection-signal" && item.Type == "BoxSet" && item.ParentId == "");
        Assert.Contains(fixture.Items, item => item.Id == "fixture-collection-city" && item.Type == "BoxSet" && item.ParentId == "");
        Assert.Contains(fixture.Items, item => item.Id == "fixture-playlist-weekend" && item.Type == "Playlist" && item.ParentId == "");
        Assert.Contains(fixture.Items, item => item.Id == "fixture-playlist-documentary" && item.Type == "Playlist" && item.ParentId == "");
        Assert.Contains(fixture.GetItemsForParent("fixture-collection-signal"), item => item.Type == "Movie");
        Assert.Contains(fixture.GetItemsForParent("fixture-playlist-weekend"), item => item.Type == "Episode");
    }

    [Fact]
    public void GetItemsForParent_Returns_Only_Root_Items_For_Blank_Parent()
    {
        var fixture = DevelopmentLibraryOrganizationFixture.Create();

        var items = fixture.GetItemsForParent("");

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal("", item.ParentId));
        Assert.Contains(items, item => item.Type == "BoxSet");
        Assert.Contains(items, item => item.Type == "Playlist");
    }

    [Fact]
    public void ArtworkUris_Point_To_Packaged_Qa_Assets()
    {
        var fixture = DevelopmentLibraryOrganizationFixture.Create();
        var root = FindRepositoryRoot();
        var expectedKeys = fixture.Items
            .Select(item => item.Type == "BoxSet" || item.Type == "Playlist"
                ? DevelopmentLibraryOrganizationFixture.ArtworkKey(item.Id, "Thumb")
                : DevelopmentLibraryOrganizationFixture.ArtworkKey(item.Id, "Primary"))
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
