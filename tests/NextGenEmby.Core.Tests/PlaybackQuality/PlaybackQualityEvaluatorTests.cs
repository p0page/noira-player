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
    public void Evaluate_Fails_When_Parsed_Source_Metadata_Does_Not_Match_Expected_Source()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "wrong-source-metadata",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                HdrKind = "Hdr10",
                RequireValidatedConversion = false
            },
            Source = new PlaybackQualitySource
            {
                Codec = "av1",
                Width = 1920,
                Height = 1080,
                HdrKind = "Sdr"
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Equal("unsupported-source", report.Analysis.PrimaryFailureArea);
        Assert.Contains("source.codec", report.Analysis.RelevantSignals);
        Assert.Contains("source.width", report.Analysis.RelevantSignals);
        Assert.Contains("source.height", report.Analysis.RelevantSignals);
        Assert.Contains("source.hdrKind", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedCodec" &&
            check.Signal == "source.codec" &&
            check.Expected == "hevc" &&
            check.Actual == "av1" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedWidth" &&
            check.Signal == "source.width" &&
            check.Expected == "3840" &&
            check.Actual == "1920" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedHeight" &&
            check.Signal == "source.height" &&
            check.Expected == "2160" &&
            check.Actual == "1080" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedHdrKind" &&
            check.Signal == "source.hdrKind" &&
            check.Expected == "Hdr10" &&
            check.Actual == "Sdr" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
    }

    [Fact]
    public void Evaluate_Fails_When_Hdr_Source_Strategy_Does_Not_Match_Expected_Source()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "wrong-dv-strategy",
            Expected = new PlaybackQualityExpected
            {
                HdrPlaybackStrategy = "HDR10 fallback from Dolby Vision",
                IsHdr = true,
                IsDirectPlayable = true,
                IsDolbyVision = true,
                DolbyVisionProfile = 8,
                DolbyVisionCompatibilityId = 1,
                HasHdr10BaseLayer = true,
                HasHlgBaseLayer = false,
                RequireValidatedConversion = false
            },
            Source = new PlaybackQualitySource
            {
                HdrPlaybackStrategy = "Unsupported Dolby Vision profile",
                IsHdr = true,
                IsDirectPlayable = false,
                IsDolbyVision = true,
                DolbyVisionProfile = 5,
                DolbyVisionCompatibilityId = 0,
                HasHdr10BaseLayer = false,
                HasHlgBaseLayer = false
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Equal("unsupported-source", report.Analysis.PrimaryFailureArea);
        Assert.Contains("source.hdrPlaybackStrategy", report.Analysis.RelevantSignals);
        Assert.Contains("source.isDirectPlayable", report.Analysis.RelevantSignals);
        Assert.Contains("source.dolbyVisionProfile", report.Analysis.RelevantSignals);
        Assert.Contains("source.dolbyVisionCompatibilityId", report.Analysis.RelevantSignals);
        Assert.Contains("source.hasHdr10BaseLayer", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedHdrPlaybackStrategy" &&
            check.Signal == "source.hdrPlaybackStrategy" &&
            check.Expected == "HDR10 fallback from Dolby Vision" &&
            check.Actual == "Unsupported Dolby Vision profile" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedIsDirectPlayable" &&
            check.Signal == "source.isDirectPlayable" &&
            check.Expected == "True" &&
            check.Actual == "False" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedDolbyVisionProfile" &&
            check.Signal == "source.dolbyVisionProfile" &&
            check.Expected == "8" &&
            check.Actual == "5" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
    }

    [Fact]
    public void Evaluate_Fails_When_Rendered_Frame_Count_Is_Below_Minimum()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "no-rendered-frames",
            Expected = new PlaybackQualityExpected
            {
                MinRenderedVideoFrames = 120,
                RequireValidatedConversion = false
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 0
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "RenderedVideoFrames 0 was below MinRenderedVideoFrames 120.",
            report.FailureReasons);
        Assert.Equal("frame-pacing", report.Analysis.PrimaryFailureArea);
        Assert.Contains("timing.renderedVideoFrames", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "RenderedVideoFrames" &&
            check.Signal == "timing.renderedVideoFrames" &&
            check.Expected == "120" &&
            check.Actual == "0" &&
            check.Status == "fail" &&
            check.FailureArea == "frame-pacing");
    }

    [Fact]
    public void Evaluate_Fails_When_Render_Interval_Percentiles_Exceed_Thresholds()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "jittery-render-intervals",
            Expected = new PlaybackQualityExpected
            {
                MaxRenderIntervalMsP95 = 50,
                MaxRenderIntervalMsP99 = 70,
                RequireValidatedConversion = false
            },
            Timing = new PlaybackQualityTiming
            {
                RenderIntervalMsP95 = 65,
                RenderIntervalMsP99 = 90
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "RenderIntervalMsP95 65.000 exceeded MaxRenderIntervalMsP95 50.000.",
            report.FailureReasons);
        Assert.Contains(
            "RenderIntervalMsP99 90.000 exceeded MaxRenderIntervalMsP99 70.000.",
            report.FailureReasons);
        Assert.Equal("frame-pacing", report.Analysis.PrimaryFailureArea);
        Assert.Contains("timing.renderIntervalMsP95", report.Analysis.RelevantSignals);
        Assert.Contains("timing.renderIntervalMsP99", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "RenderIntervalMsP95" &&
            check.Signal == "timing.renderIntervalMsP95" &&
            check.Expected == "50.000" &&
            check.Actual == "65.000" &&
            check.Status == "fail" &&
            check.FailureArea == "frame-pacing");
        Assert.Contains(report.Checks, check =>
            check.Name == "RenderIntervalMsP99" &&
            check.Signal == "timing.renderIntervalMsP99" &&
            check.Expected == "70.000" &&
            check.Actual == "90.000" &&
            check.Status == "fail" &&
            check.FailureArea == "frame-pacing");
    }

    [Fact]
    public void Evaluate_Fails_When_Render_Interval_Percentile_Is_Required_But_Missing()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-render-intervals",
            Expected = new PlaybackQualityExpected
            {
                MaxRenderIntervalMsP95 = 50,
                MaxRenderIntervalMsP99 = 70,
                RequireValidatedConversion = false
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240,
                RenderIntervalMsP95 = 0,
                RenderIntervalMsP99 = 0
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "RenderIntervalMsP95 is missing for frame-pacing validation.",
            report.FailureReasons);
        Assert.Contains(
            "RenderIntervalMsP99 is missing for frame-pacing validation.",
            report.FailureReasons);
        Assert.Equal("frame-pacing", report.Analysis.PrimaryFailureArea);
        Assert.Contains("timing.renderIntervalMsP95", report.Analysis.RelevantSignals);
        Assert.Contains("timing.renderIntervalMsP99", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "RenderIntervalMsP95" &&
            check.Signal == "timing.renderIntervalMsP95" &&
            check.Expected == "50.000" &&
            check.Actual == "" &&
            check.Status == "fail" &&
            check.FailureArea == "frame-pacing");
        Assert.Contains(report.Checks, check =>
            check.Name == "RenderIntervalMsP99" &&
            check.Signal == "timing.renderIntervalMsP99" &&
            check.Expected == "70.000" &&
            check.Actual == "" &&
            check.Status == "fail" &&
            check.FailureArea == "frame-pacing");
    }

    [Fact]
    public void Evaluate_Fails_When_Max_Frame_Gap_Is_Required_But_Missing()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-max-frame-gap",
            Expected = new PlaybackQualityExpected
            {
                MaxFrameGapMs = 105,
                RequireValidatedConversion = false
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240,
                MaxFrameGapMs = 0
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "MaxFrameGapMs is missing for frame-pacing validation.",
            report.FailureReasons);
        Assert.Equal("frame-pacing", report.Analysis.PrimaryFailureArea);
        Assert.Contains("timing.maxFrameGapMs", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "MaxFrameGapMs" &&
            check.Signal == "timing.maxFrameGapMs" &&
            check.Expected == "105.000" &&
            check.Actual == "" &&
            check.Status == "fail" &&
            check.FailureArea == "frame-pacing");
    }

    [Fact]
    public void Evaluate_Fails_When_Audio_Video_Drift_Is_Required_But_Missing()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-av-drift",
            Expected = new PlaybackQualityExpected
            {
                MaxAudioVideoDriftMsP95 = 40,
                RequireValidatedConversion = false
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240
            },
            Sync = new PlaybackQualitySync
            {
                AudioVideoDriftMsP95 = 0
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "AudioVideoDriftMsP95 is missing for av-sync validation.",
            report.FailureReasons);
        Assert.Equal("av-sync", report.Analysis.PrimaryFailureArea);
        Assert.Contains("sync.audioVideoDriftMsP95", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "AudioVideoDriftMsP95" &&
            check.Signal == "sync.audioVideoDriftMsP95" &&
            check.Expected == "40.000" &&
            check.Actual == "" &&
            check.Status == "fail" &&
            check.FailureArea == "av-sync");
    }

    [Fact]
    public void Evaluate_Fails_With_Missing_Color_Pipeline_Reasons_When_Color_Evidence_Is_Required()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-color-pipeline",
            Expected = new PlaybackQualityExpected
            {
                HdrOutput = "Hdr10",
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
                RequireValidatedConversion = true
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "ActualHdrOutput is missing for color-pipeline validation.",
            report.FailureReasons);
        Assert.Contains(
            "DxgiInput is missing for color-pipeline validation.",
            report.FailureReasons);
        Assert.Contains(
            "DxgiOutput is missing for color-pipeline validation.",
            report.FailureReasons);
        Assert.Contains(
            "ConversionStatus is missing for color-pipeline validation.",
            report.FailureReasons);
        Assert.Equal("color-pipeline", report.Analysis.PrimaryFailureArea);
        Assert.Contains("colorPipeline.actualHdrOutput", report.Analysis.RelevantSignals);
        Assert.Contains("colorPipeline.dxgiInput", report.Analysis.RelevantSignals);
        Assert.Contains("colorPipeline.dxgiOutput", report.Analysis.RelevantSignals);
        Assert.Contains("colorPipeline.conversionStatus", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "ActualHdrOutput" &&
            check.Signal == "colorPipeline.actualHdrOutput" &&
            check.Expected == "Hdr10" &&
            check.Actual == "" &&
            check.Status == "fail" &&
            check.FailureArea == "color-pipeline");
        Assert.Contains(report.Checks, check =>
            check.Name == "ConversionStatus" &&
            check.Signal == "colorPipeline.conversionStatus" &&
            check.Expected == "validated" &&
            check.Actual == "" &&
            check.Status == "fail" &&
            check.FailureArea == "color-pipeline");
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
