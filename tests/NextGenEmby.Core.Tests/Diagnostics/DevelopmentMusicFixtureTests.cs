using System.IO;
using System.Linq;
using NextGenEmby.Core.Diagnostics;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentMusicFixtureTests
{
    [Fact]
    public void Create_Provides_Albums_Songs_And_Artwork_For_Positive_Browse_Route()
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
        Assert.NotEmpty(fixture.ArtworkUris);
    }

    [Fact]
    public void ArtworkUris_Point_To_Packaged_Qa_Assets()
    {
        var fixture = DevelopmentMusicFixture.Create();
        var root = FindRepositoryRoot();
        var expectedKeys = fixture.Artists
            .Concat(fixture.Albums)
            .Concat(fixture.Songs)
            .Select(item => DevelopmentMusicFixture.ArtworkKey(item.Id, "Primary"))
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
