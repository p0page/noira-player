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

        foreach (var property in NativeOpenTransportProperties)
        {
            Assert.Contains("uint64_t " + property + "{0};", nativeMetrics, StringComparison.Ordinal);
            Assert.Contains("UInt64 " + property + ";", idl, StringComparison.Ordinal);
            Assert.Contains("metrics." + property + "(snapshot." + property + ");", nativeEngine, StringComparison.Ordinal);
            Assert.Contains("uint64_t " + property + "() const noexcept", runtimeMetrics, StringComparison.Ordinal);
            Assert.Contains("void " + property + "(uint64_t value) noexcept", runtimeMetrics, StringComparison.Ordinal);
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
    public void Demux_Read_Recovery_Metrics_Cross_Native_WinRt_Core_And_App_Capture_Boundaries()
    {
        var root = FindRepositoryRoot();
        var runtimeMetrics = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackQualityMetrics.h"));
        var idl = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.idl"));
        var nativeEngine = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.cpp"));
        var coreSnapshot = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Core", "PlaybackQuality", "PlaybackQualityMetricsSnapshot.cs"));
        var appBridge = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Playback", "WinRtNativePlaybackEngine.cs"));
        var appCapture = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs"));

        foreach (var property in new[] { "ReadErrorCount", "ReadRetryCount", "ReadRecoveryCount" })
        {
            Assert.Contains("UInt64 " + property + ";", idl, StringComparison.Ordinal);
            Assert.Contains("uint64_t " + property + "() const noexcept", runtimeMetrics, StringComparison.Ordinal);
            Assert.Contains("metrics." + property + "(snapshot." + property + ");", nativeEngine, StringComparison.Ordinal);
            Assert.Contains("public ulong " + property + " { get; set; }", coreSnapshot, StringComparison.Ordinal);
            Assert.Contains(property + " = nativeMetrics." + property, appBridge, StringComparison.Ordinal);
            Assert.Contains(property + " = source." + property, appCapture, StringComparison.Ordinal);
        }

        Assert.Contains("UInt32 MaxConsecutiveReadErrors;", idl, StringComparison.Ordinal);
        Assert.Contains("uint32_t MaxConsecutiveReadErrors() const noexcept", runtimeMetrics, StringComparison.Ordinal);
        Assert.Contains("public uint MaxConsecutiveReadErrors { get; set; }", coreSnapshot, StringComparison.Ordinal);

        foreach (var property in new[] { "LastReadErrorCode", "FatalReadErrorCode" })
        {
            Assert.Contains("Int32 " + property + ";", idl, StringComparison.Ordinal);
            Assert.Contains("int32_t " + property + "() const noexcept", runtimeMetrics, StringComparison.Ordinal);
            Assert.Contains("public int " + property + " { get; set; }", coreSnapshot, StringComparison.Ordinal);
        }

        Assert.Contains("Double LastReadRecoveryDurationMs;", idl, StringComparison.Ordinal);
        Assert.Contains("double LastReadRecoveryDurationMs() const noexcept", runtimeMetrics, StringComparison.Ordinal);
        Assert.Contains("public double LastReadRecoveryDurationMs { get; set; }", coreSnapshot, StringComparison.Ordinal);

        foreach (var property in new[]
        {
            "MaxConsecutiveReadErrors",
            "LastReadErrorCode",
            "FatalReadErrorCode",
            "LastReadRecoveryDurationMs"
        })
        {
            Assert.Contains("metrics." + property + "(snapshot." + property + ");", nativeEngine, StringComparison.Ordinal);
            Assert.Contains(property + " = nativeMetrics." + property, appBridge, StringComparison.Ordinal);
            Assert.Contains(property + " = source." + property, appCapture, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Playback_Demux_And_Transport_Metrics_Cross_All_Collector_Boundaries()
    {
        var root = FindRepositoryRoot();
        var nativeMetrics = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackQualityMetrics.h"));
        var graph = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));
        var runtimeMetrics = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackQualityMetrics.h"));
        var idl = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.idl"));
        var nativeEngine = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.cpp"));
        var coreSnapshot = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Core", "PlaybackQuality", "PlaybackQualityMetricsSnapshot.cs"));
        var appBridge = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Playback", "WinRtNativePlaybackEngine.cs"));
        var appCapture = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs"));
        var helper = File.ReadAllText(Path.Combine(root, "tests", "NoiraPlayer.Native.Tests", "NativePlaybackGraphHeadlessSmokeTests.cpp"));
        var parser = File.ReadAllText(Path.Combine(root, "tools", "NoiraPlayer.PlaybackQuality.Headless", "Program.cs"));

        Assert.Contains("PlaybackDemuxReadDurationMs", nativeMetrics, StringComparison.Ordinal);
        Assert.Contains("PlaybackDemuxPacketCount", nativeMetrics, StringComparison.Ordinal);
        Assert.Contains("PlaybackDemuxBytes", nativeMetrics, StringComparison.Ordinal);
        Assert.Contains("PlaybackTransportCalls", nativeMetrics, StringComparison.Ordinal);
        Assert.Contains("SubtractReadTimingSnapshots", graph, StringComparison.Ordinal);
        Assert.Contains("SubtractTransportCallSnapshots", graph, StringComparison.Ordinal);

        foreach (var property in new[] { "PlaybackDemuxReadDurationMs", "PlaybackDemuxPacketCount", "PlaybackDemuxBytes" })
        {
            Assert.Contains(property, idl, StringComparison.Ordinal);
            Assert.Contains("metrics." + property + "(snapshot." + property + ");", nativeEngine, StringComparison.Ordinal);
            Assert.Contains(property + " = nativeMetrics." + property, appBridge, StringComparison.Ordinal);
            Assert.Contains(property + " = source." + property, appCapture, StringComparison.Ordinal);
            Assert.Contains("public ", coreSnapshot, StringComparison.Ordinal);
            var key = char.ToLowerInvariant(property[0]) + property[1..];
            Assert.Contains("\" " + key + "=\"", helper, StringComparison.Ordinal);
            Assert.Contains("values, \"" + key + "\"", parser, StringComparison.Ordinal);
        }

        foreach (var property in new[]
        {
            "PlaybackTransportProvider",
            "PlaybackTransportCallEvidenceAvailable",
            "PlaybackTransportReadCalls",
            "PlaybackTransportSeekCalls",
            "PlaybackTransportReadWaitMs",
            "PlaybackTransportSeekWaitMs",
            "PlaybackTransportSeekDistanceBytes"
        })
        {
            Assert.Contains(property, idl, StringComparison.Ordinal);
            Assert.Contains(property, runtimeMetrics, StringComparison.Ordinal);
            Assert.Contains(property, nativeEngine, StringComparison.Ordinal);
            var key = char.ToLowerInvariant(property[0]) + property[1..];
            Assert.Contains("\" " + key + "=\"", helper, StringComparison.Ordinal);
        }

        Assert.Contains("\"playbackTransport\"", parser, StringComparison.Ordinal);
        Assert.Contains("metrics.PlaybackTransportCalls", parser, StringComparison.Ordinal);
        Assert.Contains("PlaybackTransportCalls = new PlaybackQualityTransportCallSnapshot", appBridge, StringComparison.Ordinal);
        Assert.Contains("Provider = nativeMetrics.PlaybackTransportProvider", appBridge, StringComparison.Ordinal);
        Assert.Contains("EvidenceAvailable = nativeMetrics.PlaybackTransportCallEvidenceAvailable", appBridge, StringComparison.Ordinal);
        Assert.Contains("ReadWaitMs = nativeMetrics.PlaybackTransportReadWaitMs", appBridge, StringComparison.Ordinal);
        Assert.Contains("PlaybackTransportCalls = new PlaybackQualityTransportCallSnapshot", appCapture, StringComparison.Ordinal);
        Assert.Contains("Provider = source.PlaybackTransportCalls.Provider", appCapture, StringComparison.Ordinal);
        Assert.Contains("EvidenceAvailable = source.PlaybackTransportCalls.EvidenceAvailable", appCapture, StringComparison.Ordinal);
        Assert.Contains("ReadWaitMs = source.PlaybackTransportCalls.ReadWaitMs", appCapture, StringComparison.Ordinal);
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


        foreach (var property in NativeOpenTransportProperties)
        {
            var key = char.ToLowerInvariant(property[0]) + property[1..];
            Assert.Contains("\" " + key + "=\"", helper, StringComparison.Ordinal);
            Assert.Contains("TrySetRequiredUInt64(values, \"" + key + "\"", parser, StringComparison.Ordinal);
            Assert.Contains("value => metrics." + property + " = value", parser, StringComparison.Ordinal);
        }

        Assert.Contains(
            "StartupDurationMs = metrics.NativeGraphOpenDurationMs > 0",
            parser,
            StringComparison.Ordinal);
        Assert.Contains("Name = \"native.open\"", parser, StringComparison.Ordinal);
        Assert.Contains("ProcessWallClockMs = processWallClockMs", parser, StringComparison.Ordinal);
    }

    [Fact]
    public void Instrumented_Avio_Call_Evidence_Crosses_Native_WinRt_App_And_Headless_Boundaries()
    {
        var root = FindRepositoryRoot();
        var nativeMetrics = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackQualityMetrics.h"));
        var runtimeMetrics = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackQualityMetrics.h"));
        var idl = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.idl"));
        var nativeEngine = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.cpp"));
        var appBridge = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Playback", "WinRtNativePlaybackEngine.cs"));
        var helper = File.ReadAllText(Path.Combine(root, "tests", "NoiraPlayer.Native.Tests", "NativePlaybackGraphHeadlessSmokeTests.cpp"));
        var parser = File.ReadAllText(Path.Combine(root, "tools", "NoiraPlayer.PlaybackQuality.Headless", "Program.cs"));

        Assert.Contains("String StartupTransportProvider;", idl, StringComparison.Ordinal);
        Assert.Contains("Boolean StartupTransportCallEvidenceAvailable;", idl, StringComparison.Ordinal);
        Assert.Contains("StartupTransportProvider = nativeMetrics.StartupTransportProvider", appBridge, StringComparison.Ordinal);
        Assert.Contains("StartupTransportCallEvidenceAvailable = nativeMetrics.StartupTransportCallEvidenceAvailable", appBridge, StringComparison.Ordinal);
        Assert.Contains("struct PlaybackTransportCallMetrics", nativeMetrics, StringComparison.Ordinal);
        foreach (var phase in new[] { "FfmpegOpenInput", "FfmpegStreamInfo", "NativeStartupSeek", "NativeFirstFrame" })
        {
            Assert.Contains("PlaybackTransportCallMetrics " + phase + "TransportCalls", nativeMetrics, StringComparison.Ordinal);
            Assert.Contains("String " + phase + "TransportProvider;", idl, StringComparison.Ordinal);
            Assert.Contains("Boolean " + phase + "TransportCallEvidenceAvailable;", idl, StringComparison.Ordinal);
            Assert.Contains("metrics." + phase + "TransportProvider(winrt::to_hstring(snapshot." + phase + "TransportCalls.Provider));", nativeEngine, StringComparison.Ordinal);
            Assert.Contains("metrics." + phase + "TransportCallEvidenceAvailable(snapshot." + phase + "TransportCalls.EvidenceAvailable);", nativeEngine, StringComparison.Ordinal);
            Assert.Contains("winrt::hstring " + phase + "TransportProvider() const", runtimeMetrics, StringComparison.Ordinal);
            Assert.Contains("bool " + phase + "TransportCallEvidenceAvailable() const noexcept", runtimeMetrics, StringComparison.Ordinal);
            Assert.Contains("Provider = nativeMetrics." + phase + "TransportProvider", appBridge, StringComparison.Ordinal);
            Assert.Contains("EvidenceAvailable = nativeMetrics." + phase + "TransportCallEvidenceAvailable", appBridge, StringComparison.Ordinal);
            var key = char.ToLowerInvariant(phase[0]) + phase[1..] + "Transport";
            Assert.Contains("\" " + key + "Provider=\"", helper, StringComparison.Ordinal);
            Assert.Contains("\" " + key + "CallEvidenceAvailable=\"", helper, StringComparison.Ordinal);
        }

        foreach (var property in NativeTransportCallUInt64Properties)
        {
            Assert.Contains("UInt64 " + property + ";", idl, StringComparison.Ordinal);
            Assert.Contains("metrics." + property + "(" + NativeTransportSnapshotExpression(property) + ");", nativeEngine, StringComparison.Ordinal);
            Assert.Contains("uint64_t " + property + "() const noexcept", runtimeMetrics, StringComparison.Ordinal);
            var key = char.ToLowerInvariant(property[0]) + property[1..];
            Assert.Contains("\" " + key + "=\"", helper, StringComparison.Ordinal);
            Assert.Contains("TrySetRequiredUInt64(values, \"" + key + "\"", parser, StringComparison.Ordinal);
        }

        foreach (var property in NativeTransportCallDoubleProperties)
        {
            Assert.Contains("Double " + property + ";", idl, StringComparison.Ordinal);
            Assert.Contains("metrics." + property + "(" + NativeTransportSnapshotExpression(property) + ");", nativeEngine, StringComparison.Ordinal);
            Assert.Contains("double " + property + "() const noexcept", runtimeMetrics, StringComparison.Ordinal);
            var key = char.ToLowerInvariant(property[0]) + property[1..];
            Assert.Contains("\" " + key + "=\"", helper, StringComparison.Ordinal);
            Assert.Contains("TrySetRequiredNonNegativeDouble(values, \"" + key + "\"", parser, StringComparison.Ordinal);
        }
    }

    private static string NativeTransportSnapshotExpression(string property)
    {
        var marker = "Transport";
        var markerIndex = property.IndexOf(marker, StringComparison.Ordinal);
        var phase = property[..markerIndex];
        var field = property[(markerIndex + marker.Length)..];
        return "snapshot." + phase + "TransportCalls." + field;
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
        "FfmpegOpenInputBytesRead",
        "FfmpegStreamInfoBytesRead",
        "NativeStartupSeekBytesRead",
        "NativeFirstFrameTransportBytesRead",
        "PlaybackDemuxReadDurationMs",
        "PlaybackDemuxPacketCount",
        "PlaybackDemuxBytes",
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

    private static readonly IReadOnlyList<string> NativeOpenTransportProperties = new[]
    {
        "FfmpegOpenInputBytesRead",
        "FfmpegStreamInfoBytesRead",
        "NativeStartupSeekBytesRead",
        "NativeFirstFrameTransportBytesRead",
    };

    private static readonly IReadOnlyList<string> NativeTransportCallUInt64Properties = new[]
    {
        "FfmpegOpenInputTransportReadCalls",
        "FfmpegOpenInputTransportSeekCalls",
        "FfmpegOpenInputTransportSeekDistanceBytes",
        "FfmpegStreamInfoTransportReadCalls",
        "FfmpegStreamInfoTransportSeekCalls",
        "FfmpegStreamInfoTransportSeekDistanceBytes",
        "NativeStartupSeekTransportReadCalls",
        "NativeStartupSeekTransportSeekCalls",
        "NativeStartupSeekTransportSeekDistanceBytes",
        "NativeFirstFrameTransportReadCalls",
        "NativeFirstFrameTransportSeekCalls",
        "NativeFirstFrameTransportSeekDistanceBytes",
    };

    private static readonly IReadOnlyList<string> NativeTransportCallDoubleProperties = new[]
    {
        "FfmpegOpenInputTransportReadWaitMs",
        "FfmpegOpenInputTransportSeekWaitMs",
        "FfmpegStreamInfoTransportReadWaitMs",
        "FfmpegStreamInfoTransportSeekWaitMs",
        "NativeStartupSeekTransportReadWaitMs",
        "NativeStartupSeekTransportSeekWaitMs",
        "NativeFirstFrameTransportReadWaitMs",
        "NativeFirstFrameTransportSeekWaitMs",
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
