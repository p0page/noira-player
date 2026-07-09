using System.Linq;
using NoiraPlayer.Core.Emby;
using Xunit;

namespace NoiraPlayer.Core.Tests.Emby;

public sealed class EmbySearchScopePolicyTests
{
    [Fact]
    public void AllScopes_Returns_Tv_Search_Order_With_Complete_Emby_Surfaces()
    {
        var scopes = EmbySearchScopePolicy.AllScopes;

        Assert.Collection(
            scopes.Select(scope => scope.Key),
            key => Assert.Equal("all", key),
            key => Assert.Equal("movies", key),
            key => Assert.Equal("shows", key),
            key => Assert.Equal("episodes", key),
            key => Assert.Equal("collections", key),
            key => Assert.Equal("playlists", key),
            key => Assert.Equal("people", key),
            key => Assert.Equal("music", key),
            key => Assert.Equal("photos", key),
            key => Assert.Equal("livetv", key));
    }

    [Fact]
    public void AllScope_Includes_Primary_Browsable_Emby_Item_Types()
    {
        var scope = EmbySearchScopePolicy.GetScope("all");

        Assert.Equal(
            "Movie,Series,Episode,Video,MusicVideo,BoxSet,Playlist,Person,MusicAlbum,Audio,Photo,TvChannel",
            scope.IncludeItemTypes);
    }

    [Theory]
    [InlineData("movies", "Movie")]
    [InlineData("shows", "Series")]
    [InlineData("episodes", "Episode")]
    [InlineData("collections", "BoxSet")]
    [InlineData("playlists", "Playlist")]
    [InlineData("people", "Person")]
    [InlineData("music", "MusicAlbum,Audio")]
    [InlineData("photos", "Photo")]
    [InlineData("livetv", "TvChannel")]
    public void GetScope_Returns_Filter_Query_For_Scope(string key, string includeItemTypes)
    {
        var scope = EmbySearchScopePolicy.GetScope(key);

        Assert.Equal(includeItemTypes, scope.IncludeItemTypes);
    }

    [Fact]
    public void Specific_Scopes_Require_Client_Item_Type_Match()
    {
        Assert.False(EmbySearchScopePolicy.GetScope("all").RequireItemTypeMatch);
        Assert.True(EmbySearchScopePolicy.GetScope("movies").RequireItemTypeMatch);
        Assert.True(EmbySearchScopePolicy.GetScope("shows").RequireItemTypeMatch);
        Assert.True(EmbySearchScopePolicy.GetScope("playlists").RequireItemTypeMatch);
        Assert.True(EmbySearchScopePolicy.GetScope("photos").RequireItemTypeMatch);
    }

    [Fact]
    public void GetScope_Is_Case_Insensitive_And_Falls_Back_To_All()
    {
        Assert.Equal("Movie", EmbySearchScopePolicy.GetScope("MOVIES").IncludeItemTypes);
        Assert.Equal("all", EmbySearchScopePolicy.GetScope("missing").Key);
        Assert.Equal("all", EmbySearchScopePolicy.GetScope("").Key);
        Assert.Equal("all", EmbySearchScopePolicy.GetScope(null).Key);
    }
}
