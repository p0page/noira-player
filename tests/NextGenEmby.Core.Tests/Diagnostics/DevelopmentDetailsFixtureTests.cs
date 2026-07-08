using System;
using System.IO;
using System.Linq;
using NextGenEmby.Core.Diagnostics;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentDetailsFixtureTests
{
    [Fact]
    public void Create_Covers_Details_Decision_And_Below_Fold_Rails()
    {
        var fixture = DevelopmentDetailsFixture.Create();

        Assert.Equal("fixture-detail-aurora", fixture.Item.Id);
        Assert.Equal("Movie", fixture.Item.Type);
        Assert.NotEmpty(fixture.Item.Overview);
        Assert.True(fixture.Item.People.Count >= 3);
        Assert.True(fixture.MediaSources.Count >= 2);
        Assert.NotEmpty(fixture.OrganizeAncestors);
        Assert.NotEmpty(fixture.CollectionTargets);
        Assert.NotEmpty(fixture.PlaylistTargets);
        Assert.True(fixture.SimilarItems.Count >= 4);
        Assert.NotEmpty(fixture.Item.GenreItems);
        Assert.NotEmpty(fixture.Item.StudioItems);
        Assert.NotEmpty(fixture.Item.TagItems);
    }

    [Fact]
    public void Create_Provides_Packaged_Artwork_For_Visible_Details_Surfaces()
    {
        var root = FindRepositoryRoot();
        var fixture = DevelopmentDetailsFixture.Create();
        var visibleArtworkKeys = new[]
            {
                DevelopmentDetailsFixture.ArtworkKey(fixture.Item.Id, "Primary"),
                DevelopmentDetailsFixture.ArtworkKey(fixture.Item.Id, "Backdrop")
            }
            .Concat(fixture.SimilarItems.Select(item => DevelopmentDetailsFixture.ArtworkKey(item.Id, "Primary")))
            .Concat(fixture.CollectionTargets.Select(item => DevelopmentDetailsFixture.ArtworkKey(item.Id, "Thumb")))
            .Concat(fixture.PlaylistTargets.Select(item => DevelopmentDetailsFixture.ArtworkKey(item.Id, "Thumb")))
            .Concat(fixture.Item.People.Select(person => DevelopmentDetailsFixture.ArtworkKey(person.Id, "Primary")))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(visibleArtworkKeys);
        foreach (var key in visibleArtworkKeys)
        {
            Assert.True(fixture.ArtworkUris.TryGetValue(key, out var uri), "Missing Details artwork URI for " + key);
            Assert.StartsWith("ms-appx:///Assets/QaHome/", uri, StringComparison.Ordinal);

            var relativePath = uri.Substring("ms-appx:///".Length).Replace('/', Path.DirectorySeparatorChar);
            var assetPath = Path.Combine(root, "src", "NextGenEmby.App", relativePath);
            Assert.True(File.Exists(assetPath), "Missing packaged QA artwork asset " + assetPath);
        }
    }

    [Fact]
    public void Create_Includes_Long_Stream_Labels_For_Details_Overflow_Coverage()
    {
        var fixture = DevelopmentDetailsFixture.Create();
        var audioLabels = fixture.MediaSources
            .SelectMany(source => source.AudioStreams)
            .Select(stream => stream.DisplayTitle)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();
        var subtitleLabels = fixture.MediaSources
            .SelectMany(source => source.SubtitleStreams)
            .Select(stream => stream.DisplayTitle)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        Assert.Contains(audioLabels, label =>
            label.Length >= 56 &&
            label.IndexOf("commentary", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.Contains(subtitleLabels, label =>
            label.Length >= 56 &&
            label.IndexOf("descriptive", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void CreateWithoutArtwork_Leaves_Main_Item_Artwork_Empty_For_Fallback_Coverage()
    {
        var fixture = DevelopmentDetailsFixture.CreateWithoutArtwork();

        Assert.Equal("fixture-detail-no-art", fixture.Item.Id);
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.PrimaryImageTag));
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.PrimaryImageItemId));
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.BackdropImageTag));
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.BackdropImageItemId));
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.ThumbImageTag));
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.ThumbImageItemId));
        Assert.False(fixture.ArtworkUris.ContainsKey(DevelopmentDetailsFixture.ArtworkKey(fixture.Item.Id, "Primary")));
        Assert.False(fixture.ArtworkUris.ContainsKey(DevelopmentDetailsFixture.ArtworkKey(fixture.Item.Id, "Backdrop")));
        Assert.False(fixture.ArtworkUris.ContainsKey(DevelopmentDetailsFixture.ArtworkKey(fixture.Item.Id, "Thumb")));
        Assert.True(fixture.MediaSources.Count >= 2);
        Assert.NotEmpty(fixture.SimilarItems);
    }

    [Fact]
    public void CreateWithPrimaryOnlyArtwork_Provides_Poster_Only_Atmosphere_Coverage()
    {
        var root = FindRepositoryRoot();
        var fixture = DevelopmentDetailsFixture.CreateWithPrimaryOnlyArtwork();

        Assert.Equal("fixture-detail-primary-only", fixture.Item.Id);
        Assert.Equal("Poster Only Signal", fixture.Item.Name);
        Assert.Equal("qa", fixture.Item.PrimaryImageTag);
        Assert.Equal(fixture.Item.Id, fixture.Item.PrimaryImageItemId);
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.BackdropImageTag));
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.BackdropImageItemId));
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.ThumbImageTag));
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.ThumbImageItemId));
        Assert.True(fixture.ArtworkUris.ContainsKey(DevelopmentDetailsFixture.ArtworkKey(fixture.Item.Id, "Primary")));
        Assert.False(fixture.ArtworkUris.ContainsKey(DevelopmentDetailsFixture.ArtworkKey(fixture.Item.Id, "Backdrop")));
        Assert.False(fixture.ArtworkUris.ContainsKey(DevelopmentDetailsFixture.ArtworkKey(fixture.Item.Id, "Thumb")));
        Assert.True(fixture.MediaSources.Count >= 2);
        Assert.NotEmpty(fixture.SimilarItems);

        var uri = fixture.ArtworkUris[DevelopmentDetailsFixture.ArtworkKey(fixture.Item.Id, "Primary")];
        Assert.StartsWith("ms-appx:///Assets/QaHome/", uri, StringComparison.Ordinal);
        Assert.EndsWith("qa-poster-13.png", uri, StringComparison.Ordinal);
        var relativePath = uri.Substring("ms-appx:///".Length).Replace('/', Path.DirectorySeparatorChar);
        var assetPath = Path.Combine(root, "src", "NextGenEmby.App", relativePath);
        Assert.True(File.Exists(assetPath), "Missing packaged Primary-only Details artwork asset " + assetPath);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "Generate-AppIconAssets.ps1")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
