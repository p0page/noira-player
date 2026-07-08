using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class NativeQualityMetricsBridgeContractTests
{
    [Fact]
    public void WinRt_Native_Playback_Engine_Bridges_Quality_Metrics_To_Core_Provider()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Playback",
            "WinRtNativePlaybackEngine.cs"));

        Assert.Contains("using NextGenEmby.Core.PlaybackQuality;", source, StringComparison.Ordinal);
        Assert.Contains("IPlaybackQualityMetricsProvider", source, StringComparison.Ordinal);
        Assert.Contains("IPlaybackQualityMetricsProviderIdentity", source, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityMetricsProviderId => \"native-winrt\"", source, StringComparison.Ordinal);
        Assert.Contains("_engine.QualityMetrics()", source, StringComparison.Ordinal);

        foreach (var property in RequiredMetricProperties)
        {
            Assert.Contains(property + " = nativeMetrics." + property, source, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "NextGenEmby.App", "Playback", "WinRtNativePlaybackEngine.cs")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NextGenEmby.Core", "PlaybackQuality", "PlaybackQualityMetricsSnapshot.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static readonly IReadOnlyList<string> RequiredMetricProperties = new[]
    {
        "RenderPasses",
        "DecodedVideoFrames",
        "RenderedVideoFrames",
        "SubmittedAudioFrames",
        "DroppedVideoFrames",
        "SeekPrerollDroppedFrames",
        "VideoAheadWaitCount",
        "VideoStarvedPasses",
        "AudioStarvedPasses",
        "QueuedAudioBuffers",
        "AudioClockTicks",
        "VideoPositionTicks",
        "RenderIntervalMsP50",
        "RenderIntervalMsP95",
        "RenderIntervalMsP99",
        "MaxFrameGapMs",
        "PresentDurationMsP50",
        "PresentDurationMsP95",
        "PresentDurationMsP99",
        "PresentDurationMsMax",
        "AudioAheadWaitDurationMsP50",
        "AudioAheadWaitDurationMsP95",
        "AudioAheadWaitDurationMsP99",
        "AudioAheadWaitDurationMsMax",
        "AudioAheadWaitTargetMsP50",
        "AudioAheadWaitTargetMsP95",
        "AudioAheadWaitTargetMsP99",
        "AudioAheadWaitTargetMsMax",
        "AudioAheadWaitOversleepMsP50",
        "AudioAheadWaitOversleepMsP95",
        "AudioAheadWaitOversleepMsP99",
        "AudioAheadWaitOversleepMsMax",
        "FramePacingSourceFrameRate",
        "LateFrameDropToleranceMs",
        "AudioVideoDriftMsP50",
        "AudioVideoDriftMsP95",
        "AudioVideoDriftMsP99",
        "AudioVideoDriftMsMax",
    };
}
