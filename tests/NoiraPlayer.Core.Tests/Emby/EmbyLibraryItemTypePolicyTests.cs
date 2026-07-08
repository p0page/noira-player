using NoiraPlayer.Core.Emby;
using Xunit;

namespace NoiraPlayer.Core.Tests.Emby;

public sealed class EmbyLibraryItemTypePolicyTests
{
    [Fact]
    public void KeepIncludedItemTypes_Removes_Root_Collection_Folders_From_Strict_Playlist_View()
    {
        var items = new[]
        {
            new EmbyMediaItem { Id = "view-1", Type = "CollectionFolder" },
            new EmbyMediaItem { Id = "playlist-1", Type = "Playlist" },
            new EmbyMediaItem { Id = "movie-1", Type = "Movie" }
        };

        var filtered = EmbyLibraryItemTypePolicy.KeepIncludedItemTypes(items, "Playlist");

        var item = Assert.Single(filtered);
        Assert.Equal("playlist-1", item.Id);
    }

    [Fact]
    public void KeepIncludedItemTypes_Returns_Items_When_No_Type_Filter_Is_Requested()
    {
        var items = new[]
        {
            new EmbyMediaItem { Id = "view-1", Type = "CollectionFolder" }
        };

        var filtered = EmbyLibraryItemTypePolicy.KeepIncludedItemTypes(items, "");

        Assert.Same(items, filtered);
    }
}
