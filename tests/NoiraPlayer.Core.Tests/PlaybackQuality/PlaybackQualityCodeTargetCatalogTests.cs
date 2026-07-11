using System.Collections.Generic;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityCodeTargetCatalogTests
{
    [Fact]
    public void GetForFailureArea_Returns_FramePacing_Targets()
    {
        var targets = PlaybackQualityCodeTargetCatalog.GetForFailureArea("frame-pacing");

        Assert.Contains("src/NoiraPlayer.Native/Media/FramePacing.h", targets);
        Assert.Contains("src/NoiraPlayer.Native/Media/PlaybackGraph.cpp", targets);
        Assert.Contains("src/NoiraPlayer.Core/PlaybackQuality/PlaybackRefreshRatePolicy.cs", targets);
    }

    [Fact]
    public void AddForFailureAreas_Deduplicates_Targets()
    {
        var targets = new List<string>();

        PlaybackQualityCodeTargetCatalog.AddForFailureArea(targets, "frame-pacing");
        PlaybackQualityCodeTargetCatalog.AddForFailureArea(targets, "av-sync");

        Assert.Equal(
            targets.Count,
            new HashSet<string>(targets).Count);
    }

    [Fact]
    public void GetForFailureArea_Returns_Unknown_Targets_For_Unmapped_Area()
    {
        var targets = PlaybackQualityCodeTargetCatalog.GetForFailureArea("new-area");

        Assert.Contains("src/NoiraPlayer.Core/PlaybackQuality", targets);
        Assert.Contains("src/NoiraPlayer.Native", targets);
    }

    [Fact]
    public void GetForSignal_Returns_Source_Targets_For_Source_Signal()
    {
        var targets = PlaybackQualityCodeTargetCatalog.GetForSignal("source.hdrKind");

        Assert.Contains("src/NoiraPlayer.Core/Playback/HdrPlaybackProfileClassifier.cs", targets);
    }

    [Fact]
    public void GetForSignal_Returns_Timeline_Targets_For_Position_Signal()
    {
        var targets = PlaybackQualityCodeTargetCatalog.GetForSignal("position.seekPositionErrorMs");

        Assert.Contains("src/NoiraPlayer.Core/Playback/PlaybackOrchestrator.cs", targets);
        Assert.Contains("src/NoiraPlayer.Native/Media/PlaybackGraph.cpp", targets);
    }

    [Fact]
    public void GetForSignal_Returns_Evidence_Targets_For_Unknown_Signal()
    {
        var targets = PlaybackQualityCodeTargetCatalog.GetForSignal("custom.missingSignal");

        Assert.Contains("src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportMapper.cs", targets);
    }

    [Theory]
    [InlineData("lifecycle.audio-switch", "tracks")]
    [InlineData("lifecycle.subtitle-switch", "subtitles")]
    [InlineData("lifecycle.pause", "playback-lifecycle")]
    public void GetFailureAreaForSignal_Maps_Lifecycle_Operations(
        string signal,
        string expectedArea)
    {
        Assert.Equal(expectedArea, PlaybackQualityCodeTargetCatalog.GetFailureAreaForSignal(signal));
        Assert.NotEqual(
            PlaybackQualityCodeTargetCatalog.GetForFailureArea("unknown"),
            PlaybackQualityCodeTargetCatalog.GetForFailureArea(expectedArea));
    }
}
