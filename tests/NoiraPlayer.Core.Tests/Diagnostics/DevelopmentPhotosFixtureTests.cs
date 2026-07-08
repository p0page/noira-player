using System.Linq;
using NoiraPlayer.Core.Diagnostics;
using Xunit;

namespace NoiraPlayer.Core.Tests.Diagnostics;

public sealed class DevelopmentPhotosFixtureTests
{
    [Fact]
    public void Create_Items_Contains_Root_Album_And_Photos()
    {
        var fixture = DevelopmentPhotosFixture.Create();

        Assert.Contains(fixture.Items, item => item.Id == "fixture-photo-album-night-market" && item.Type == "Folder");
        Assert.Contains(fixture.Items, item => item.Id == "fixture-photo-rooftop" && item.Type == "Photo" && item.ParentId == "");
        Assert.Empty(fixture.ArtworkUris);
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
    public void ArtworkUris_Are_Empty_After_Removing_Packaged_Qa_Assets()
    {
        var fixture = DevelopmentPhotosFixture.Create();

        Assert.Empty(fixture.ArtworkUris);
        Assert.All(fixture.Items, item => Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageTag)));
    }
}
