using System.Linq;
using NextGenEmby.Core.Diagnostics;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentMusicFixtureTests
{
    [Fact]
    public void Create_Provides_Albums_Songs_And_Artists_For_Positive_Browse_Route()
    {
        var fixture = DevelopmentMusicFixture.Create();

        Assert.True(fixture.Albums.Count >= 3);
        Assert.True(fixture.Songs.Count >= 5);
        Assert.True(fixture.Artists.Count >= 3);
        Assert.Contains(fixture.Artists, item => item.Type == "MusicArtist");
        Assert.Contains(fixture.Albums, item => item.Type == "MusicAlbum" && item.ChildCount >= 8);
        Assert.Contains(fixture.Songs, item => item.Type == "Audio" && item.RunTimeTicks > 0);
        Assert.Contains(fixture.Songs, item => item.ParentId == fixture.Albums[0].Id);
        Assert.Contains(
            fixture.Albums,
            item => item.AlbumArtists.Any(artist => artist.Id == fixture.Artists[0].Id));
        Assert.Contains(
            fixture.Songs,
            item => item.ArtistItems.Any(artist => artist.Id == fixture.Artists[0].Id));
        Assert.Empty(fixture.ArtworkUris);
    }

    [Fact]
    public void ArtworkUris_Are_Empty_After_Removing_Packaged_Qa_Assets()
    {
        var fixture = DevelopmentMusicFixture.Create();

        Assert.Empty(fixture.ArtworkUris);
        Assert.All(fixture.Artists, item => Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageTag)));
        Assert.All(fixture.Albums, item => Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageTag)));
        Assert.All(fixture.Songs, item => Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageTag)));
    }
}
