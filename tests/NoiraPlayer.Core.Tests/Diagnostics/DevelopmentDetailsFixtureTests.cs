using System;
using System.Linq;
using NoiraPlayer.Core.Diagnostics;
using Xunit;

namespace NoiraPlayer.Core.Tests.Diagnostics;

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
    public void Create_Does_Not_Depend_On_Packaged_Qa_Artwork()
    {
        var fixture = DevelopmentDetailsFixture.Create();

        Assert.Empty(fixture.ArtworkUris);
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.PrimaryImageTag));
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.BackdropImageTag));
        Assert.All(fixture.SimilarItems, item => Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageTag)));
        Assert.All(fixture.CollectionTargets, item => Assert.True(string.IsNullOrWhiteSpace(item.ThumbImageTag)));
        Assert.All(fixture.PlaylistTargets, item => Assert.True(string.IsNullOrWhiteSpace(item.ThumbImageTag)));
        Assert.All(fixture.Item.People, person => Assert.True(string.IsNullOrWhiteSpace(person.PrimaryImageTag)));
    }

    [Fact]
    public void CreateWithLongSourceLabels_Includes_Long_Stream_Labels_For_Details_Overflow_Coverage()
    {
        var fixture = DevelopmentDetailsFixture.CreateWithLongSourceLabels();
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
    public void Create_Uses_Concise_Stream_Labels_For_Default_Details_Visual_Baseline()
    {
        var fixture = DevelopmentDetailsFixture.Create();
        var visibleLabels = fixture.MediaSources
            .Select(source => source.Name)
            .Concat(fixture.MediaSources.SelectMany(source => source.AudioStreams).Select(stream => stream.DisplayTitle))
            .Concat(fixture.MediaSources.SelectMany(source => source.SubtitleStreams).Select(stream => stream.DisplayTitle))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        Assert.NotEmpty(visibleLabels);
        Assert.All(visibleLabels, label => Assert.True(label.Length <= 48, "Default Details fixture label is too long: " + label));
        Assert.DoesNotContain(visibleLabels, label => label.IndexOf("commentary", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.DoesNotContain(visibleLabels, label => label.IndexOf("descriptive", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void CreateWithLongSourceLabels_Covers_Details_Decision_Overflow_As_Separate_Stress_Fixture()
    {
        var fixture = DevelopmentDetailsFixture.CreateWithLongSourceLabels();
        var sourceNames = fixture.MediaSources
            .Select(source => source.Name)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();
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

        Assert.Equal("fixture-detail-long-source", fixture.Item.Id);
        Assert.Contains(sourceNames, label =>
            label.Length >= 56 &&
            label.IndexOf("archival", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.Contains(audioLabels, label =>
            label.Length >= 56 &&
            label.IndexOf("commentary", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.Contains(subtitleLabels, label =>
            label.Length >= 56 &&
            label.IndexOf("descriptive", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.NotEmpty(fixture.SimilarItems);
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
    public void CreateWithPrimaryOnlyArtwork_NoLonger_Uses_Packaged_Qa_Artwork()
    {
        var fixture = DevelopmentDetailsFixture.CreateWithPrimaryOnlyArtwork();

        Assert.Equal("fixture-detail-primary-only", fixture.Item.Id);
        Assert.Equal("Poster Only Signal", fixture.Item.Name);
        Assert.True(string.IsNullOrWhiteSpace(fixture.Item.PrimaryImageTag));
        Assert.Equal(fixture.Item.Id, fixture.Item.PrimaryImageItemId);
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
}
