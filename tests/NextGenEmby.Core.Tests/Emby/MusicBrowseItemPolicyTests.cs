using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class MusicBrowseItemPolicyTests
{
    [Fact]
    public void KeepAlbums_Removes_Server_Section_Cards()
    {
        var items = new[]
        {
            new EmbyMediaItem { Id = "section-1", Type = "CollectionFolder" },
            new EmbyMediaItem { Id = "album-1", Type = "MusicAlbum" },
            new EmbyMediaItem { Id = "song-1", Type = "Audio" }
        };

        var filtered = MusicBrowseItemPolicy.KeepAlbums(items);

        var item = Assert.Single(filtered);
        Assert.Equal("album-1", item.Id);
    }

    [Fact]
    public void KeepSongs_Removes_Server_Section_Cards_And_Albums()
    {
        var items = new[]
        {
            new EmbyMediaItem { Id = "section-1", Type = "CollectionFolder" },
            new EmbyMediaItem { Id = "album-1", Type = "MusicAlbum" },
            new EmbyMediaItem { Id = "song-1", Type = "Audio" }
        };

        var filtered = MusicBrowseItemPolicy.KeepSongs(items);

        var item = Assert.Single(filtered);
        Assert.Equal("song-1", item.Id);
    }
}
