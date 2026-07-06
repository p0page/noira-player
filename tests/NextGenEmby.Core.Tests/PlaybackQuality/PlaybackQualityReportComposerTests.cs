using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

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
}
