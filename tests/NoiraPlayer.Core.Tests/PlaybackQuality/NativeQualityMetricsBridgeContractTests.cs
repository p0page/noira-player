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
        Assert.Contains("SeekFallbackReason = nativeMetrics.SeekFallbackReason", source, StringComparison.Ordinal);

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

    [Fact]
    public void Native_Interaction_Timing_Metrics_Cross_The_Graph_WinRt_And_Core_Boundaries()
    {
        var root = FindRepositoryRoot();
        var idl = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.idl"));
        var runtimeMetrics = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackQualityMetrics.h"));
        var nativeEngine = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.cpp"));

        Assert.Contains("String LastInteractionScenario;", idl, StringComparison.Ordinal);
        Assert.Contains("UInt64 LastInteractionSequence;", idl, StringComparison.Ordinal);
        Assert.Contains("auto const timing = m_graph->SwitchAudioStream", nativeEngine, StringComparison.Ordinal);
        Assert.Contains("auto const timing = m_graph->SwitchSubtitleStream", nativeEngine, StringComparison.Ordinal);
        Assert.Contains("ResetLastInteractionTiming();", nativeEngine, StringComparison.Ordinal);

        foreach (var property in NativeInteractionDoubleProperties)
        {
            Assert.Contains("Double " + property + ";", idl, StringComparison.Ordinal);
            Assert.Contains("metrics." + property + "(interaction." + property[15..] + ");", nativeEngine, StringComparison.Ordinal);
            Assert.Contains("double " + property + "() const noexcept", runtimeMetrics, StringComparison.Ordinal);
        }

        foreach (var property in NativeInteractionUInt64Properties)
        {
            Assert.Contains("UInt64 " + property + ";", idl, StringComparison.Ordinal);
            Assert.Contains(property + " = nativeMetrics." + property, File.ReadAllText(Path.Combine(
                root, "src", "NoiraPlayer.App", "Playback", "WinRtNativePlaybackEngine.cs")), StringComparison.Ordinal);
        }

        foreach (var property in NativeInteractionBooleanProperties)
        {
            Assert.Contains("Boolean " + property + ";", idl, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Native_Headless_Startup_Breakdown_Crosses_Helper_Parser_And_Report()
    {
        var root = FindRepositoryRoot();
        var helper = File.ReadAllText(Path.Combine(
            root, "tests", "NoiraPlayer.Native.Tests", "NativePlaybackGraphHeadlessSmokeTests.cpp"));
        var parser = File.ReadAllText(Path.Combine(
            root, "tools", "NoiraPlayer.PlaybackQuality.Headless", "Program.cs"));

        foreach (var property in NativeOpenTimingProperties)
        {
            var key = char.ToLowerInvariant(property[0]) + property[1..];
            Assert.Contains("\" " + key + "=\"", helper, StringComparison.Ordinal);
            Assert.Contains("TrySetRequiredNonNegativeDouble(values, \"" + key + "\"", parser, StringComparison.Ordinal);
            Assert.Contains("value => metrics." + property + " = value", parser, StringComparison.Ordinal);
        }

        Assert.Contains(
            "StartupDurationMs = metrics.NativeGraphOpenDurationMs > 0",
            parser,
            StringComparison.Ordinal);
        Assert.Contains("Name = \"native.open\"", parser, StringComparison.Ordinal);
        Assert.Contains("ProcessWallClockMs = processWallClockMs", parser, StringComparison.Ordinal);
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
        "SelectedAudioStreamIndex",
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
        "NativeStartupSeekDurationMs",
        "NativeFirstFrameDurationMs",
        "NativeFirstFrameDemuxReadDurationMs",
        "NativeFirstFramePresentDurationMs",
        "NativeFirstFrameDemuxPacketCount",
        "NativeFirstFrameDemuxBytes",
        "ContainerStartTimeTicks",
        "VideoStreamStartTimeTicks",
        "SeekDemuxTargetTicks",
        "FirstPresentedPositionTicks",
        "SeekPacketCacheEnabled",
        "SeekPacketCacheHit",
        "SeekPacketCachePacketCount",
        "SeekPacketCacheBytes",
        "SeekPacketCacheWindowDurationTicks",
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
        "LastInteractionSequence",
        "LastInteractionLockWaitDurationMs",
        "LastInteractionExecutionDurationMs",
        "LastInteractionQuiesceDurationMs",
        "LastInteractionSeekDurationMs",
        "LastInteractionDecoderOpenDurationMs",
        "LastInteractionRendererOpenDurationMs",
        "LastInteractionPacketCacheHit",
        "LastInteractionPacketCacheEnabled",
        "LastInteractionPacketCachePacketCount",
        "LastInteractionPacketCacheBytes",
        "LastInteractionPacketCacheWindowDurationTicks",
    };

    private static readonly IReadOnlyList<string> NativeOpenTimingProperties = new[]
    {
        "NativeGraphOpenDurationMs",
        "FfmpegOpenInputDurationMs",
        "FfmpegStreamInfoDurationMs",
        "NativeStartupSeekDurationMs",
        "NativeFirstFrameDurationMs",
        "NativeFirstFrameDemuxReadDurationMs",
        "NativeFirstFramePresentDurationMs",
    };

    private static readonly IReadOnlyList<string> NativeInteractionDoubleProperties = new[]
    {
        "LastInteractionLockWaitDurationMs",
        "LastInteractionExecutionDurationMs",
        "LastInteractionQuiesceDurationMs",
        "LastInteractionSeekDurationMs",
        "LastInteractionDecoderOpenDurationMs",
        "LastInteractionRendererOpenDurationMs",
    };

    private static readonly IReadOnlyList<string> NativeInteractionUInt64Properties = new[]
    {
        "LastInteractionSequence",
        "LastInteractionPacketCachePacketCount",
        "LastInteractionPacketCacheBytes",
    };

    private static readonly IReadOnlyList<string> NativeInteractionBooleanProperties = new[]
    {
        "LastInteractionPacketCacheHit",
        "LastInteractionPacketCacheEnabled",
    };
}
