using System;
using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Playback;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityReportComposerTests
{
    [Fact]
    public void Compose_Evaluates_Report_And_Model_Analysis_From_Playback_Evidence()
    {
        var descriptor = CreatePlaybackDescriptor(frameRate: 23.976);
        var displayStatus = CreateHdrDisplayStatus(refreshRateHz: 59.94006);
        var metrics = CreateStableMetrics(maxFrameGapMs: 60);
        var expected = CreateHdrExpected(maxFrameGapMs: 105);

        var result = PlaybackQualityReportComposer.Compose(new PlaybackQualityReportRequest
        {
            RunId = "hdr10-stable",
            Descriptor = descriptor,
            DisplayStatus = displayStatus,
            Metrics = metrics,
            Expected = expected
        });

        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal("hdr10-stable", result.Report.RunId);
        Assert.Equal("pass", result.Report.Result);
        Assert.Empty(result.Report.FailureReasons);
        Assert.Equal("item-1", result.Report.Source.ItemId);
        Assert.Equal("source-1", result.Report.Source.MediaSourceId);
        Assert.Equal("hevc", result.Report.Source.Codec);
        Assert.Equal("Hdr10", result.Report.ColorPipeline.ActualHdrOutput);
        Assert.Equal(59.94006, result.Report.Display.RefreshRateHz);
        Assert.Equal(1000.0 / 23.976, result.Report.Timing.ExpectedFrameDurationMs, precision: 6);
        Assert.Contains(result.Report.Checks, check =>
            check.Name == "DisplayRefreshRateHz" &&
            check.Status == "pass" &&
            check.Signal == "display.refreshRateHz");
        Assert.Equal("pass", result.ModelAnalysis.Result);
        Assert.Equal("none", result.ModelAnalysis.PrimaryFailureArea);
        Assert.DoesNotContain("timing.expectedFrameDurationMs", result.ModelAnalysis.MissingEvidence);
        Assert.DoesNotContain("display.refreshRateHz", result.ModelAnalysis.MissingEvidence);
    }

    [Fact]
    public void Compose_Surfaces_Frame_Pacing_Evidence_When_Max_Frame_Gap_Fails()
    {
        var descriptor = CreatePlaybackDescriptor(frameRate: 23.976);
        var displayStatus = CreateHdrDisplayStatus(refreshRateHz: 59.94006);
        var metrics = CreateStableMetrics(maxFrameGapMs: 180);
        var expected = CreateHdrExpected(maxFrameGapMs: 105);

        var result = PlaybackQualityReportComposer.Compose(new PlaybackQualityReportRequest
        {
            RunId = "hdr10-pacing-bad",
            Descriptor = descriptor,
            DisplayStatus = displayStatus,
            Metrics = metrics,
            Expected = expected
        });

        Assert.Equal("fail", result.Report.Result);
        Assert.Equal("frame-pacing", result.Report.Analysis.PrimaryFailureArea);
        Assert.Contains(
            "MaxFrameGapMs 180.000 exceeded MaxFrameGapMs 105.000.",
            result.Report.FailureReasons);
        Assert.Equal("frame-pacing", result.ModelAnalysis.PrimaryFailureArea);
        Assert.Contains("frame-pacing", result.ModelAnalysis.FailureAreas);
        Assert.Contains("timing.maxFrameGapMs", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("timing.expectedFrameDurationMs", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains(result.ModelAnalysis.FailedChecks, check =>
            check.Name == "MaxFrameGapMs" &&
            check.Expected == "105.000" &&
            check.Actual == "180.000");
    }

    [Fact]
    public void Serializer_Writes_Run_Result_With_Report_And_Model_Analysis()
    {
        var result = PlaybackQualityReportComposer.Compose(new PlaybackQualityReportRequest
        {
            RunId = "json-run",
            CaseMetadata = new PlaybackQualityCaseMetadata
            {
                CaseId = "json-run",
                Category = "challenge",
                Severity = "high",
                Stability = "variable"
            },
            Descriptor = CreatePlaybackDescriptor(frameRate: 23.976),
            DisplayStatus = CreateHdrDisplayStatus(refreshRateHz: 59.94006),
            Metrics = CreateStableMetrics(maxFrameGapMs: 60),
            Expected = CreateHdrExpected(maxFrameGapMs: 105)
        });

        var json = PlaybackQualityReportSerializer.Serialize(result);

        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"evaluationVersion\": \"playback-quality-v0.2\"", json);
        Assert.Contains("\"report\"", json);
        Assert.Contains("\"modelAnalysis\"", json);
        Assert.Contains("\"caseMetadata\"", json);
        Assert.Contains("\"category\": \"challenge\"", json);
        Assert.Contains("\"severity\": \"high\"", json);
        Assert.Contains("\"stability\": \"variable\"", json);
        Assert.Contains("\"runId\": \"json-run\"", json);
        Assert.Contains("\"result\": \"pass\"", json);
        Assert.Contains("\"display.refreshRateHz\"", json);
    }

    [Fact]
    public void Compose_Copies_Case_Metadata_For_Model_Consumable_Report_Envelope()
    {
        var result = PlaybackQualityReportComposer.Compose(new PlaybackQualityReportRequest
        {
            RunId = "case-metadata-run",
            CaseMetadata = new PlaybackQualityCaseMetadata
            {
                CaseId = "case-metadata-run",
                Category = "stable",
                Severity = "critical",
                Stability = "stable"
            },
            Descriptor = CreatePlaybackDescriptor(frameRate: 23.976),
            DisplayStatus = CreateHdrDisplayStatus(refreshRateHz: 59.94006),
            Metrics = CreateStableMetrics(maxFrameGapMs: 60),
            Expected = CreateHdrExpected(maxFrameGapMs: 105)
        });

        Assert.Equal("case-metadata-run", result.CaseMetadata.CaseId);
        Assert.Equal("stable", result.CaseMetadata.Category);
        Assert.Equal("critical", result.CaseMetadata.Severity);
        Assert.Equal("stable", result.CaseMetadata.Stability);
    }

    [Fact]
    public void Compose_Copies_Startup_Evidence_And_Evaluates_Startup_Threshold()
    {
        var expected = CreateHdrExpected(maxFrameGapMs: 105);
        expected.MaxStartupDurationMs = 2000;

        var result = PlaybackQualityReportComposer.Compose(new PlaybackQualityReportRequest
        {
            RunId = "slow-startup",
            Descriptor = CreatePlaybackDescriptor(frameRate: 23.976),
            DisplayStatus = CreateHdrDisplayStatus(refreshRateHz: 59.94006),
            Metrics = CreateStableMetrics(maxFrameGapMs: 60),
            Startup = new PlaybackQualityStartup
            {
                CommandReceivedAt = "2026-07-07T00:00:00Z",
                PlaybackStartedAt = "2026-07-07T00:00:03.500Z",
                StartupDurationMs = 3500
            },
            Expected = expected
        });

        Assert.Equal("2026-07-07T00:00:00Z", result.Report.Startup.CommandReceivedAt);
        Assert.Equal("2026-07-07T00:00:03.500Z", result.Report.Startup.PlaybackStartedAt);
        Assert.Equal(3500, result.Report.Startup.StartupDurationMs);
        Assert.Equal("fail", result.Report.Result);
        Assert.Equal("startup", result.ModelAnalysis.PrimaryFailureArea);
        Assert.Contains("startup.startupDurationMs", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains(result.ModelAnalysis.FailedChecks, check =>
            check.Name == "StartupDurationMs" &&
            check.Actual == "3500.000");
    }

    [Fact]
    public void Compose_Copies_Position_Evidence_Before_Evaluating_Seek_Threshold()
    {
        var expected = CreateHdrExpected(maxFrameGapMs: 105);
        expected.MaxSeekPositionErrorMs = 250;

        var result = PlaybackQualityReportComposer.Compose(new PlaybackQualityReportRequest
        {
            RunId = "seek-position-run",
            Descriptor = CreatePlaybackDescriptor(frameRate: 23.976),
            DisplayStatus = CreateHdrDisplayStatus(refreshRateHz: 59.94006),
            Metrics = CreateStableMetrics(maxFrameGapMs: 60),
            Expected = expected,
            Position = new PlaybackQualityPosition
            {
                RequestedStartPositionTicks = 100_000_000,
                SeekTargetPositionTicks = 300_000_000,
                ActualPositionTicks = 301_000_000
            }
        });

        Assert.Equal(100_000_000, result.Report.Position.RequestedStartPositionTicks);
        Assert.Equal(300_000_000, result.Report.Position.SeekTargetPositionTicks);
        Assert.Equal(301_000_000, result.Report.Position.ActualPositionTicks);
        Assert.Equal(100, result.Report.Position.SeekPositionErrorMs);
        Assert.Contains(result.Report.Checks, check =>
            check.Name == "SeekPositionErrorMs" &&
            check.Status == "pass" &&
            check.Signal == "position.seekPositionErrorMs");
        Assert.DoesNotContain("position.seekPositionErrorMs", result.ModelAnalysis.MissingEvidence);
        Assert.Contains("position.seekPositionErrorMs", result.ModelAnalysis.EvidenceSignals);
    }

    [Fact]
    public void Compose_Classifies_Empty_Runtime_Metrics_Snapshot_For_Model()
    {
        var expected = CreateHdrExpected(maxFrameGapMs: 105);
        expected.MinRenderedVideoFrames = 120;

        var result = PlaybackQualityReportComposer.Compose(new PlaybackQualityReportRequest
        {
            RunId = "empty-runtime-metrics",
            Descriptor = CreatePlaybackDescriptor(frameRate: 23.976),
            Metrics = new PlaybackQualityMetricsSnapshot(),
            Expected = expected
        });

        Assert.Equal("empty-snapshot", result.Report.RuntimeMetrics.Status);
        Assert.Equal("not-applicable", result.Report.RuntimeMetrics.ProviderStatus);
        Assert.True(result.Report.RuntimeMetrics.HasSnapshot);
        Assert.False(result.Report.RuntimeMetrics.HasPlaybackSample);
        Assert.Equal("empty-snapshot", result.ModelAnalysis.RuntimeMetrics.Status);
        Assert.Equal("not-applicable", result.ModelAnalysis.RuntimeMetrics.ProviderStatus);
        Assert.True(result.ModelAnalysis.RuntimeMetrics.HasSnapshot);
        Assert.False(result.ModelAnalysis.RuntimeMetrics.HasPlaybackSample);
        Assert.Contains("runtimeMetrics.status", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("runtimeMetrics.reason", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("timing.renderedVideoFrames", result.ModelAnalysis.MissingEvidence);
    }

    [Fact]
    public void Compose_Does_Not_Report_Default_Runtime_Metrics_As_Evidence()
    {
        var result = PlaybackQualityReportComposer.Compose(new PlaybackQualityReportRequest
        {
            RunId = "no-runtime-metrics",
            Descriptor = CreatePlaybackDescriptor(frameRate: 23.976),
            Expected = CreateHdrExpected(maxFrameGapMs: 105)
        });

        Assert.Equal("unknown", result.Report.RuntimeMetrics.Status);
        Assert.Equal("unknown", result.ModelAnalysis.RuntimeMetrics.Status);
        Assert.DoesNotContain("runtimeMetrics.status", result.ModelAnalysis.EvidenceSignals);
        Assert.DoesNotContain("runtimeMetrics.providerStatus", result.ModelAnalysis.EvidenceSignals);
        Assert.DoesNotContain("runtimeMetrics.reason", result.ModelAnalysis.EvidenceSignals);
        Assert.DoesNotContain("runtimeMetrics.hasSnapshot", result.ModelAnalysis.EvidenceSignals);
        Assert.DoesNotContain("runtimeMetrics.hasPlaybackSample", result.ModelAnalysis.EvidenceSignals);
    }

    [Fact]
    public void Compose_Copies_Environment_Evidence_For_Model()
    {
        var result = PlaybackQualityReportComposer.Compose(new PlaybackQualityReportRequest
        {
            RunId = "build-identity",
            Descriptor = CreatePlaybackDescriptor(frameRate: 23.976),
            DisplayStatus = CreateHdrDisplayStatus(refreshRateHz: 59.94006),
            Metrics = CreateStableMetrics(maxFrameGapMs: 60),
            Expected = CreateHdrExpected(maxFrameGapMs: 105),
            Environment = new PlaybackQualityEnvironment
            {
                CollectorVersion = "quality-run-v2",
                PlayerCoreVersion = "native-core-v42",
                SourceRevision = "abc1234",
                BuildConfiguration = "Debug"
            }
        });

        Assert.Equal("quality-run-v2", result.Report.Environment.CollectorVersion);
        Assert.Equal("native-core-v42", result.Report.Environment.PlayerCoreVersion);
        Assert.Equal("abc1234", result.Report.Environment.SourceRevision);
        Assert.Equal("Debug", result.Report.Environment.BuildConfiguration);
        Assert.Equal("quality-run-v2", result.ModelAnalysis.Environment.CollectorVersion);
        Assert.Equal("native-core-v42", result.ModelAnalysis.Environment.PlayerCoreVersion);
        Assert.Equal("abc1234", result.ModelAnalysis.Environment.SourceRevision);
        Assert.Equal("Debug", result.ModelAnalysis.Environment.BuildConfiguration);
        Assert.Contains("environment.collectorVersion", result.ModelAnalysis.Environment.Signals);
        Assert.Contains("environment.playerCoreVersion", result.ModelAnalysis.Environment.Signals);
        Assert.Contains("environment.sourceRevision", result.ModelAnalysis.Environment.Signals);
        Assert.Contains("environment.buildConfiguration", result.ModelAnalysis.Environment.Signals);
        Assert.Contains("environment.sourceRevision", result.ModelAnalysis.EvidenceSignals);

        var json = PlaybackQualityReportSerializer.Serialize(result);

        Assert.Contains("\"environment\"", json);
        Assert.Contains("\"sourceRevision\": \"abc1234\"", json);
    }

    [Fact]
    public void Compose_Fills_Missing_Environment_Evidence_From_Process()
    {
        WithEnvironment("NOIRAPLAYER_PLAYBACK_QUALITY_COLLECTOR_VERSION", "quality-run-env", () =>
        WithEnvironment("NOIRAPLAYER_PLAYER_CORE_VERSION", "native-core-env", () =>
        WithEnvironment("NOIRAPLAYER_SOURCE_REVISION", "env1234", () =>
        WithEnvironment("NOIRAPLAYER_BUILD_CONFIGURATION", "Release", () =>
        {
            var result = PlaybackQualityReportComposer.Compose(new PlaybackQualityReportRequest
            {
                RunId = "environment-from-process",
                Descriptor = CreatePlaybackDescriptor(frameRate: 23.976),
                DisplayStatus = CreateHdrDisplayStatus(refreshRateHz: 59.94006),
                Metrics = CreateStableMetrics(maxFrameGapMs: 60),
                Expected = CreateHdrExpected(maxFrameGapMs: 105),
                Environment = new PlaybackQualityEnvironment
                {
                    CollectorVersion = "explicit-collector"
                }
            });

            Assert.Equal("explicit-collector", result.Report.Environment.CollectorVersion);
            Assert.Equal("native-core-env", result.Report.Environment.PlayerCoreVersion);
            Assert.Equal("env1234", result.Report.Environment.SourceRevision);
            Assert.Equal("Release", result.Report.Environment.BuildConfiguration);
            Assert.Contains("environment.playerCoreVersion", result.ModelAnalysis.Environment.Signals);
            Assert.Contains("environment.sourceRevision", result.ModelAnalysis.EvidenceSignals);
        }))));
    }

    [Fact]
    public void Compose_Uses_Default_Expected_Thresholds_When_Requested()
    {
        var result = PlaybackQualityReportComposer.Compose(new PlaybackQualityReportRequest
        {
            RunId = "default-expected",
            Descriptor = CreatePlaybackDescriptor(frameRate: 23.976),
            DisplayStatus = CreateHdrDisplayStatus(refreshRateHz: 59.94006),
            Metrics = CreateStableMetrics(maxFrameGapMs: 60),
            UseDefaultExpectedWhenMissing = true
        });

        Assert.NotNull(result.Report.Expected);
        Assert.Equal(23.976, result.Report.Expected!.FrameRate);
        Assert.Equal("Hdr10", result.Report.Expected.HdrOutput);
        Assert.Equal("pass", result.Report.Result);
        Assert.Contains(result.Report.Checks, check =>
            check.Name == "MaxFrameGapMs" &&
            check.Status == "pass" &&
            check.Signal == "timing.maxFrameGapMs");
        Assert.Equal("pass", result.ModelAnalysis.Result);
    }

    private static PlaybackDescriptor CreatePlaybackDescriptor(double frameRate)
    {
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            Width = 3840,
            Height = 2160,
            VideoFrameRate = frameRate,
            HdrProfile = new HdrPlaybackProfile
            {
                Kind = HdrPlaybackKind.Hdr10,
                Codec = "hevc"
            }
        };
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Video,
            Codec = "hevc",
            Index = 0
        });
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Audio,
            Codec = "eac3",
            Index = 1
        });

        return new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 0,
            audioStreamIndex: 1);
    }

    private static PlaybackDisplayStatus CreateHdrDisplayStatus(double refreshRateHz)
    {
        return new PlaybackDisplayStatus(
            HdrOutputStatus.On,
            isHdrDisplayAvailable: true,
            isHdrOutputActive: true,
            message: "HDR active",
            swapChainFormat: "R10G10B10A2_UNORM",
            swapChainColorSpace: "RGB_FULL_G2084_NONE_P2020",
            isTenBitSwapChain: true,
            isVideoProcessorColorSpaceValidated: true,
            videoProcessorInputColorSpace: "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
            videoProcessorOutputColorSpace: "RGB_FULL_G2084_NONE_P2020",
            videoProcessorConversionStatus: "validated",
            refreshRateHz: refreshRateHz);
    }

    private static PlaybackQualityMetricsSnapshot CreateStableMetrics(double maxFrameGapMs)
    {
        return new PlaybackQualityMetricsSnapshot
        {
            RenderPasses = 240,
            DecodedVideoFrames = 240,
            RenderedVideoFrames = 240,
            SubmittedAudioFrames = 240,
            DroppedVideoFrames = 0,
            SeekPrerollDroppedFrames = 0,
            VideoAheadWaitCount = 0,
            VideoStarvedPasses = 0,
            AudioStarvedPasses = 0,
            QueuedAudioBuffers = 4,
            AudioClockTicks = 10000000,
            VideoPositionTicks = 10000000,
            RenderIntervalMsP50 = 41.708,
            RenderIntervalMsP95 = 42.1,
            RenderIntervalMsP99 = 48.0,
            MaxFrameGapMs = maxFrameGapMs,
            AudioVideoDriftMsP50 = 4,
            AudioVideoDriftMsP95 = 16,
            AudioVideoDriftMsP99 = 24,
            AudioVideoDriftMsMax = 32
        };
    }

    private static PlaybackQualityExpected CreateHdrExpected(double maxFrameGapMs)
    {
        return new PlaybackQualityExpected
        {
            HdrOutput = "Hdr10",
            DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
            DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
            MaxDroppedFrames = 0,
            MaxFrameGapMs = maxFrameGapMs,
            MaxAudioVideoDriftMsP95 = 40,
            MaxVideoStarvedPasses = 0,
            MaxAudioStarvedPasses = 0,
            RequireValidatedConversion = true,
            RequireMatchedDisplayRefreshRate = true
        };
    }

    private static void WithEnvironment(string name, string value, Action action)
    {
        var previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        try
        {
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }
}
