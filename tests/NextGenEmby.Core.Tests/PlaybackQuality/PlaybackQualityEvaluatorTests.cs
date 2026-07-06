using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityEvaluatorTests
{
    [Fact]
    public void Evaluate_Returns_Observed_When_No_Expected_Thresholds_Are_Provided()
    {
        var report = new PlaybackQualityReport { RunId = "manual-observation" };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("observed", report.Result);
        Assert.Equal("software-quality-v1", report.MetricVersion);
        Assert.Empty(report.FailureReasons);
        Assert.Equal("none", report.Analysis.PrimaryFailureArea);
        Assert.Equal("No thresholds supplied; inspect raw metrics only.", report.Analysis.SuggestedNextAction);
        Assert.Contains(
            "software-only: does not verify actual HDMI InfoFrame output",
            report.Limitations);
    }

    [Fact]
    public void Evaluate_Passes_When_Metrics_Match_Expected_Thresholds()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "hdr10-ok",
            Expected = new PlaybackQualityExpected
            {
                HdrOutput = "Hdr10",
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
                MaxDroppedFrames = 1,
                MaxFrameGapMs = 105,
                MaxAudioVideoDriftMsP95 = 40,
                MaxVideoStarvedPasses = 0,
                MaxAudioStarvedPasses = 0
            },
            Timing = new PlaybackQualityTiming
            {
                DroppedVideoFrames = 1,
                MaxFrameGapMs = 83.4
            },
            Sync = new PlaybackQualitySync
            {
                AudioVideoDriftMsP95 = 23.5
            },
            Buffers = new PlaybackQualityBuffers
            {
                VideoStarvedPasses = 0,
                AudioStarvedPasses = 0
            },
            ColorPipeline = new PlaybackQualityColorPipeline
            {
                ActualHdrOutput = "Hdr10",
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
                ConversionStatus = "validated"
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("pass", report.Result);
        Assert.Empty(report.FailureReasons);
        Assert.Equal("none", report.Analysis.PrimaryFailureArea);
        Assert.Equal("No failing thresholds.", report.Analysis.SuggestedNextAction);
    }

    [Fact]
    public void Evaluate_Fails_When_Startup_Duration_Exceeds_Threshold()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "slow-startup",
            Expected = new PlaybackQualityExpected
            {
                MaxStartupDurationMs = 2000,
                RequireValidatedConversion = false
            },
            Startup = new PlaybackQualityStartup
            {
                StartupDurationMs = 3500
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "StartupDurationMs 3500.000 exceeded MaxStartupDurationMs 2000.000.",
            report.FailureReasons);
        Assert.Equal("startup", report.Analysis.PrimaryFailureArea);
        Assert.Equal(
            "Inspect Emby request, source open, demux initialization, and first-frame readiness.",
            report.Analysis.SuggestedNextAction);
        Assert.Contains("startup.startupDurationMs", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "StartupDurationMs" &&
            check.Signal == "startup.startupDurationMs" &&
            check.Status == "fail" &&
            check.FailureArea == "startup");
    }

    [Fact]
    public void Evaluate_Fails_When_Startup_Duration_Is_Required_But_Missing()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-startup",
            Expected = new PlaybackQualityExpected
            {
                MaxStartupDurationMs = 2000,
                RequireValidatedConversion = false
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "StartupDurationMs is missing for startup validation.",
            report.FailureReasons);
        Assert.Equal("startup", report.Analysis.PrimaryFailureArea);
        Assert.Contains("startup.startupDurationMs", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "StartupDurationMs" &&
            check.Signal == "startup.startupDurationMs" &&
            check.Expected == "2000.000" &&
            check.Actual == "" &&
            check.Status == "fail" &&
            check.FailureArea == "startup");
    }

    [Fact]
    public void Evaluate_Fails_When_Source_Frame_Rate_Does_Not_Match_Expected_Frame_Rate()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "wrong-source-cadence",
            Expected = new PlaybackQualityExpected
            {
                FrameRate = 23.976,
                RequireValidatedConversion = false
            },
            Source = new PlaybackQualitySource
            {
                FrameRate = 25.0
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "ExpectedFrameRate 23.976 did not match source frame rate 25.000.",
            report.FailureReasons);
        Assert.Equal("unsupported-source", report.Analysis.PrimaryFailureArea);
        Assert.Equal(
            "Inspect container, codec, Dolby Vision profile, and media source selection.",
            report.Analysis.SuggestedNextAction);
        Assert.Contains("source.frameRate", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedFrameRate" &&
            check.Signal == "source.frameRate" &&
            check.Expected == "23.976" &&
            check.Actual == "25.000" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
    }

    [Fact]
    public void Evaluate_Fails_With_Actionable_Reasons_When_Thresholds_Are_Exceeded()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "hdr10-bad",
            Expected = new PlaybackQualityExpected
            {
                HdrOutput = "Hdr10",
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
                MaxDroppedFrames = 0,
                MaxFrameGapMs = 105,
                MaxAudioVideoDriftMsP95 = 40,
                MaxVideoStarvedPasses = 0,
                MaxAudioStarvedPasses = 0
            },
            Timing = new PlaybackQualityTiming
            {
                DroppedVideoFrames = 3,
                MaxFrameGapMs = 180
            },
            Sync = new PlaybackQualitySync
            {
                AudioVideoDriftMsP95 = 55
            },
            Buffers = new PlaybackQualityBuffers
            {
                VideoStarvedPasses = 2,
                AudioStarvedPasses = 1
            },
            ColorPipeline = new PlaybackQualityColorPipeline
            {
                ActualHdrOutput = "Sdr",
                DxgiInput = "YCBCR_STUDIO_G22_LEFT_P709",
                DxgiOutput = "RGB_FULL_G22_NONE_P709",
                ConversionStatus = "not-run"
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains("DroppedVideoFrames 3 exceeded MaxDroppedFrames 0.", report.FailureReasons);
        Assert.Contains("MaxFrameGapMs 180.000 exceeded MaxFrameGapMs 105.000.", report.FailureReasons);
        Assert.Contains("AudioVideoDriftMsP95 55.000 exceeded MaxAudioVideoDriftMsP95 40.000.", report.FailureReasons);
        Assert.Contains("VideoStarvedPasses 2 exceeded MaxVideoStarvedPasses 0.", report.FailureReasons);
        Assert.Contains("AudioStarvedPasses 1 exceeded MaxAudioStarvedPasses 0.", report.FailureReasons);
        Assert.Contains("ActualHdrOutput Sdr did not match expected Hdr10.", report.FailureReasons);
        Assert.Contains("DxgiInput YCBCR_STUDIO_G22_LEFT_P709 did not match expected YCBCR_STUDIO_G2084_TOPLEFT_P2020.", report.FailureReasons);
        Assert.Contains("DxgiOutput RGB_FULL_G22_NONE_P709 did not match expected RGB_FULL_G2084_NONE_P2020.", report.FailureReasons);
        Assert.Contains("ConversionStatus not-run is not validated.", report.FailureReasons);
        Assert.Equal("color-pipeline", report.Analysis.PrimaryFailureArea);
        Assert.Equal("Inspect HDR display switch and DXGI color-space mapping.", report.Analysis.SuggestedNextAction);
        Assert.Contains("colorPipeline.actualHdrOutput", report.Analysis.RelevantSignals);
        Assert.Contains("colorPipeline.dxgiInput", report.Analysis.RelevantSignals);
        Assert.Contains("timing.maxFrameGapMs", report.Analysis.RelevantSignals);
        Assert.Contains("sync.audioVideoDriftMsP95", report.Analysis.RelevantSignals);
        Assert.Contains("buffers.videoStarvedPasses", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "ActualHdrOutput" &&
            check.Signal == "colorPipeline.actualHdrOutput" &&
            check.Status == "fail" &&
            check.FailureArea == "color-pipeline");
        Assert.Contains(report.Checks, check =>
            check.Name == "MaxFrameGapMs" &&
            check.Signal == "timing.maxFrameGapMs" &&
            check.Expected == "105.000" &&
            check.Actual == "180.000" &&
            check.Status == "fail" &&
            check.FailureArea == "frame-pacing");
    }

    [Fact]
    public void Evaluate_Fails_When_Display_Refresh_Does_Not_Match_Source_Frame_Rate()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "bad-cadence",
            Expected = new PlaybackQualityExpected
            {
                RequireMatchedDisplayRefreshRate = true,
                RequireValidatedConversion = false
            },
            Source = new PlaybackQualitySource
            {
                FrameRate = 23.976
            },
            Display = new PlaybackQualityDisplay
            {
                RefreshRateHz = 50.0
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "DisplayRefreshRateHz 50.000 does not match source frame rate 23.976.",
            report.FailureReasons);
        Assert.Equal("frame-pacing", report.Analysis.PrimaryFailureArea);
        Assert.Contains("display.refreshRateHz", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "DisplayRefreshRateHz" &&
            check.Signal == "display.refreshRateHz" &&
            check.Expected == "matched to source.frameRate 23.976" &&
            check.Actual == "50.000" &&
            check.Status == "fail" &&
            check.FailureArea == "frame-pacing");
    }

    [Fact]
    public void Serializer_RoundTrips_Report_With_CamelCase_Names()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "roundtrip",
            Result = "pass",
            Source = new PlaybackQualitySource { ItemId = "item-1", MediaSourceId = "source-1" }
        };

        var json = PlaybackQualityReportSerializer.Serialize(report);
        var parsed = PlaybackQualityReportSerializer.Deserialize(json);

        Assert.Contains("\"schemaVersion\"", json);
        Assert.Contains("\"metricVersion\"", json);
        Assert.Contains("\"runId\"", json);
        Assert.Contains("\"analysis\"", json);
        Assert.Contains("\"checks\"", json);
        Assert.Contains("\"limitations\"", json);
        Assert.Equal("roundtrip", parsed.RunId);
        Assert.Equal("source-1", parsed.Source.MediaSourceId);
    }
}
