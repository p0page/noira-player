using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class NativeQualityMetricsBridgeContractTests
{
    [Fact]
    public void WinRt_Native_Playback_Engine_Bridges_Quality_Metrics_To_Core_Provider()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
            "Playback",
            "WinRtNativePlaybackEngine.cs"));

        Assert.Contains("using NoiraPlayer.Core.PlaybackQuality;", source, StringComparison.Ordinal);
        Assert.Contains("IPlaybackQualityMetricsProvider", source, StringComparison.Ordinal);
        Assert.Contains("IPlaybackQualityMetricsProviderIdentity", source, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityMetricsProviderId => \"native-winrt\"", source, StringComparison.Ordinal);
        Assert.Contains("_engine.QualityMetrics()", source, StringComparison.Ordinal);

        foreach (var property in RequiredMetricProperties)
        {
            Assert.Contains(property + " = nativeMetrics." + property, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Native_Open_Timing_Metrics_Cross_The_Cpp_WinRt_And_Core_Boundaries()
    {
        var root = FindRepositoryRoot();
        var nativeMetrics = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackQualityMetrics.h"));
        var runtimeMetrics = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackQualityMetrics.h"));
        var idl = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.idl"));
        var nativeEngine = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.cpp"));

        foreach (var property in NativeOpenTimingProperties)
        {
            Assert.Contains("double " + property + "{0.0};", nativeMetrics, StringComparison.Ordinal);
            Assert.Contains("Double " + property + ";", idl, StringComparison.Ordinal);
            Assert.Contains("metrics." + property + "(snapshot." + property + ");", nativeEngine, StringComparison.Ordinal);
            Assert.Contains("double " + property + "() const noexcept", runtimeMetrics, StringComparison.Ordinal);
            Assert.Contains("void " + property + "(double value) noexcept", runtimeMetrics, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.App", "Playback", "WinRtNativePlaybackEngine.cs")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.Core", "PlaybackQuality", "PlaybackQualityMetricsSnapshot.cs")))
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
        "HardwareDecodedVideoFrames",
        "SoftwareDecodedVideoFrames",
        "RenderedVideoFrames",
        "SubmittedAudioFrames",
        "DroppedVideoFrames",
        "SeekPrerollDroppedFrames",
        "VideoAheadWaitCount",
        "AudioAheadWaitCount",
        "VideoClockWaitCount",
        "VideoStarvedPasses",
        "AudioStarvedPasses",
        "QueuedAudioBuffers",
        "AudioClockTicks",
        "VideoPositionTicks",
        "NativeGraphOpenDurationMs",
        "FfmpegOpenInputDurationMs",
        "FfmpegStreamInfoDurationMs",
        "ContainerStartTimeTicks",
        "VideoStreamStartTimeTicks",
        "SeekDemuxTargetTicks",
        "FirstPresentedPositionTicks",
        "RenderIntervalMsP05",
        "RenderIntervalMsP50",
        "RenderIntervalMsP95",
        "RenderIntervalMsP99",
        "MinFrameGapMs",
        "MaxFrameGapMs",
        "RenderIntervalSampleCount",
        "RenderIntervalOverExpected2MsCount",
        "RenderIntervalOverExpected4MsCount",
        "RenderIntervalUnderExpected2MsCount",
        "RenderIntervalUnderExpected4MsCount",
        "RenderIntervalAfterAudioAheadWaitSampleCount",
        "RenderIntervalAfterAudioAheadWaitMsP95",
        "RenderIntervalAfterAudioAheadWaitMsP99",
        "RenderIntervalAfterAudioAheadWaitMsMax",
        "AudioAheadWaitEndToPresentSampleCount",
        "AudioAheadWaitEndToPresentMsP50",
        "AudioAheadWaitEndToPresentMsP95",
        "AudioAheadWaitEndToPresentMsP99",
        "AudioAheadWaitEndToPresentMsMax",
        "RenderIntervalAfterNonAudioWaitSampleCount",
        "RenderIntervalAfterNonAudioWaitMsP95",
        "RenderIntervalAfterNonAudioWaitMsP99",
        "RenderIntervalAfterNonAudioWaitMsMax",
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
        "AudioAheadWaitFinalDeltaAbsMsP50",
        "AudioAheadWaitFinalDeltaAbsMsP95",
        "AudioAheadWaitFinalDeltaAbsMsP99",
        "AudioAheadWaitFinalDeltaAbsMsMax",
        "AudioAheadWaitEpisodeCount",
        "AudioAheadWaitPassesPerEpisodeP50",
        "AudioAheadWaitPassesPerEpisodeP95",
        "AudioAheadWaitPassesPerEpisodeP99",
        "AudioAheadWaitPassesPerEpisodeMax",
        "AudioAheadWaitPassDurationMsP50",
        "AudioAheadWaitPassDurationMsP95",
        "AudioAheadWaitPassDurationMsP99",
        "AudioAheadWaitPassDurationMsMax",
        "AudioAheadWaitPassTargetMsP50",
        "AudioAheadWaitPassTargetMsP95",
        "AudioAheadWaitPassTargetMsP99",
        "AudioAheadWaitPassTargetMsMax",
        "AudioAheadWaitPassOversleepMsP50",
        "AudioAheadWaitPassOversleepMsP95",
        "AudioAheadWaitPassOversleepMsP99",
        "AudioAheadWaitPassOversleepMsMax",
        "FramePacingSourceFrameRate",
        "LateFrameDropToleranceMs",
        "AudioVideoDriftMsP50",
        "AudioVideoDriftMsP95",
        "AudioVideoDriftMsP99",
        "AudioVideoDriftMsMax",
    };

    private static readonly IReadOnlyList<string> NativeOpenTimingProperties = new[]
    {
        "NativeGraphOpenDurationMs",
        "FfmpegOpenInputDurationMs",
        "FfmpegStreamInfoDurationMs",
    };
}
