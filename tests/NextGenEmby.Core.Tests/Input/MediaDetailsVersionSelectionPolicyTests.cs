using System;
using NextGenEmby.Core.Input;
using Xunit;

namespace NextGenEmby.Core.Tests.Input;

public sealed class MediaDetailsVersionSelectionPolicyTests
{
    [Fact]
    public void Select_Existing_Source_Updates_Selection_Without_Starting_Playback()
    {
        var decision = MediaDetailsVersionSelectionPolicy.Select(
            new[] { "source-1080p", "source-4k" },
            "source-4k",
            "4K HEVC");

        Assert.Equal("source-4k", decision.SelectedMediaSourceId);
        Assert.False(decision.StartPlayback);
        Assert.Equal("Version selected: 4K HEVC. Press Play to start.", decision.StatusMessage);
    }

    [Fact]
    public void Select_Missing_Source_Keeps_Current_Selection()
    {
        var decision = MediaDetailsVersionSelectionPolicy.Select(
            new[] { "source-1080p", "source-4k" },
            "missing",
            "Missing",
            "source-1080p");

        Assert.Equal("source-1080p", decision.SelectedMediaSourceId);
        Assert.False(decision.StartPlayback);
        Assert.Equal("Version unavailable.", decision.StatusMessage);
    }

    [Fact]
    public void ResolvePlaybackSource_Uses_Selected_Source_When_Available()
    {
        Assert.Equal(
            "source-4k",
            MediaDetailsVersionSelectionPolicy.ResolvePlaybackSource(
                new[] { "source-1080p", "source-4k" },
                "source-4k"));
    }

    [Fact]
    public void ResolvePlaybackSource_Falls_Back_To_First_Source_When_Selection_Is_Stale()
    {
        Assert.Equal(
            "source-1080p",
            MediaDetailsVersionSelectionPolicy.ResolvePlaybackSource(
                new[] { "source-1080p", "source-4k" },
                "old-source"));
    }
}
