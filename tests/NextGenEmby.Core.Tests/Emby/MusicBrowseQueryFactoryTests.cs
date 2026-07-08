using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class MusicBrowseQueryFactoryTests
{
    [Fact]
    public void CreateAlbumsQuery_Uses_MusicAlbum_View()
    {
        var query = MusicBrowseQueryFactory.CreateAlbumsQuery();

        Assert.Equal("MusicAlbum", query.IncludeItemTypes);
        Assert.Equal("SortName", query.SortBy);
        Assert.Equal("Ascending", query.SortOrder);
        Assert.Equal(60, query.Limit);
        Assert.True(query.Recursive);
        Assert.Equal("", query.ParentId);
    }

    [Fact]
    public void CreateSongsQuery_Uses_Audio_View()
    {
        var query = MusicBrowseQueryFactory.CreateSongsQuery();

        Assert.Equal("Audio", query.IncludeItemTypes);
        Assert.Equal("Artist,Album,SortName", query.SortBy);
        Assert.Equal("Ascending,Ascending,Ascending", query.SortOrder);
        Assert.Equal(80, query.Limit);
        Assert.True(query.Recursive);
        Assert.Equal("", query.ParentId);
    }

    [Fact]
    public void CreateAlbumSongsQuery_Constrains_To_Album()
    {
        var query = MusicBrowseQueryFactory.CreateAlbumSongsQuery(" album-1 ");

        Assert.Equal("album-1", query.ParentId);
        Assert.Equal("Audio", query.IncludeItemTypes);
        Assert.Equal("ParentIndexNumber,IndexNumber,SortName", query.SortBy);
        Assert.Equal("Ascending,Ascending,Ascending", query.SortOrder);
        Assert.Equal(80, query.Limit);
        Assert.False(query.Recursive);
    }

    [Fact]
    public void CreateAlbumSongsQuery_Normalizes_Null_Album()
    {
        var query = MusicBrowseQueryFactory.CreateAlbumSongsQuery(null);

        Assert.Equal("", query.ParentId);
        Assert.Equal("Audio", query.IncludeItemTypes);
    }

    [Fact]
    public void CreateArtistAlbumsQuery_Constrains_To_AlbumArtist()
    {
        var query = MusicBrowseQueryFactory.CreateArtistAlbumsQuery(" artist-1 ");

        Assert.Equal("MusicAlbum", query.IncludeItemTypes);
        Assert.Equal("artist-1", query.AlbumArtistIds);
        Assert.Equal("", query.ArtistIds);
        Assert.Equal("SortName", query.SortBy);
        Assert.True(query.Recursive);
    }

    [Fact]
    public void CreateArtistSongsQuery_Constrains_To_Artist()
    {
        var query = MusicBrowseQueryFactory.CreateArtistSongsQuery(" artist-1 ");

        Assert.Equal("Audio", query.IncludeItemTypes);
        Assert.Equal("artist-1", query.ArtistIds);
        Assert.Equal("", query.AlbumArtistIds);
        Assert.Equal("Artist,Album,SortName", query.SortBy);
        Assert.True(query.Recursive);
    }
}
