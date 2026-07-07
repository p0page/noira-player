using System.Collections.Generic;
using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityCodeTargetCatalogTests
{
    [Fact]
    public void GetForFailureArea_Returns_FramePacing_Targets()
    {
        var targets = PlaybackQualityCodeTargetCatalog.GetForFailureArea("frame-pacing");

        Assert.Contains("src/NextGenEmby.Native/Media/FramePacing.h", targets);
        Assert.Contains("src/NextGenEmby.Native/Media/PlaybackGraph.cpp", targets);
        Assert.Contains("src/NextGenEmby.Core/PlaybackQuality/PlaybackRefreshRatePolicy.cs", targets);
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

        Assert.Contains("src/NextGenEmby.Core/PlaybackQuality", targets);
        Assert.Contains("src/NextGenEmby.Native", targets);
    }

    [Fact]
    public void GetForSignal_Returns_Source_Targets_For_Source_Signal()
    {
        var targets = PlaybackQualityCodeTargetCatalog.GetForSignal("source.hdrKind");

        Assert.Contains("src/NextGenEmby.Core/Playback/HdrPlaybackProfileClassifier.cs", targets);
    }

    [Fact]
    public void GetForSignal_Returns_Timeline_Targets_For_Position_Signal()
    {
        var targets = PlaybackQualityCodeTargetCatalog.GetForSignal("position.seekPositionErrorMs");

        Assert.Contains("src/NextGenEmby.Core/Playback/PlaybackOrchestrator.cs", targets);
        Assert.Contains("src/NextGenEmby.Native/Media/PlaybackGraph.cpp", targets);
    }

    [Fact]
    public void GetForSignal_Returns_Evidence_Targets_For_Unknown_Signal()
    {
        var targets = PlaybackQualityCodeTargetCatalog.GetForSignal("custom.missingSignal");

        Assert.Contains("src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportMapper.cs", targets);
    }
}
