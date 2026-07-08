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
    public void ArtworkUris_Are_Empty_After_Removing_Packaged_Qa_Assets()
    {
        var fixture = DevelopmentLibraryOrganizationFixture.Create();

        Assert.Empty(fixture.ArtworkUris);
        Assert.All(fixture.Items, item => Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageTag)));
        Assert.All(fixture.Items, item => Assert.True(string.IsNullOrWhiteSpace(item.ThumbImageTag)));
    }
}
