using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

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
    public void Evaluate_Fails_When_No_Expected_Thresholds_And_Lifecycle_Operation_Fails()
    {
        const string message = "audio switch failed before threshold evaluation";
        var report = new PlaybackQualityReport { RunId = "observed-audio-switch-failed" };
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "audio-switch",
            Status = "failed",
            Message = message
        });

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Equal("tracks", report.Analysis.PrimaryFailureArea);
        Assert.Contains(message, report.FailureReasons);
        Assert.Contains(report.Checks, check =>
            check.Signal == "lifecycle.audio-switch" &&
            check.Status == "fail" &&
            check.FailureArea == "tracks" &&
            check.Message == message);
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
                IsTenBitSwapChain = true,
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
    public void Evaluate_Passes_When_Requested_Sample_Window_Is_Covered()
    {
        var report = CreateSampleWindowReport(renderedVideoFrames: 300);

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal(PlaybackQualityReportResult.Pass, report.Result);
        Assert.Contains(report.Checks, check =>
            check.Name == "SampleWindowCoverage" &&
            check.Status == "pass" &&
            check.Signal == "execution.requestedSampleDurationMs");
    }

    [Fact]
    public void Evaluate_Fails_Incomplete_Sample_As_Environment_Issue_When_Transport_Wait_Dominates()
    {
        var report = CreateSampleWindowReport(renderedVideoFrames: 60);
        report.Buffers.PlaybackTransportProvider = "instrumented-ffmpeg-avio";
        report.Buffers.PlaybackTransportCallEvidenceStatus = "available";
        report.Buffers.PlaybackTransportReadWaitMs = 4000;
        report.Buffers.PlaybackDemuxReadDurationMs = 4100;

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal(PlaybackQualityReportResult.Fail, report.Result);
        Assert.Equal("buffering", report.Analysis.PrimaryFailureArea);
        Assert.Contains(report.Checks, check =>
            check.Name == "SampleWindowCoverage" &&
            check.Status == "fail" &&
            check.FailureArea == "buffering" &&
            check.FailureClass == PlaybackQualityFailureClassification.EnvironmentIssue);
        Assert.Contains("buffers.playbackTransportReadWaitMs", report.Analysis.RelevantSignals);
    }

    [Fact]
    public void Evaluate_Fails_Incomplete_Sample_As_Core_Bug_When_Downstream_Progress_Is_Slow()
    {
        var report = CreateSampleWindowReport(renderedVideoFrames: 60);
        report.Buffers.PlaybackTransportProvider = "instrumented-ffmpeg-avio";
        report.Buffers.PlaybackTransportCallEvidenceStatus = "available";
        report.Buffers.PlaybackTransportReadWaitMs = 50;
        report.Buffers.PlaybackDemuxReadDurationMs = 75;

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal(PlaybackQualityReportResult.Fail, report.Result);
        Assert.Equal("frame-pacing", report.Analysis.PrimaryFailureArea);
        Assert.Contains(report.Checks, check =>
            check.Name == "SampleWindowCoverage" &&
            check.Status == "fail" &&
            check.FailureArea == "frame-pacing" &&
            check.FailureClass == PlaybackQualityFailureClassification.PlayerCoreBug);
        Assert.Contains("timing.renderedVideoFrames", report.Analysis.RelevantSignals);
    }

    [Fact]
    public void Evaluate_Fails_Incomplete_Sample_As_Instrumentation_Gap_When_Transport_Evidence_Is_Missing()
    {
        var report = CreateSampleWindowReport(renderedVideoFrames: 60);

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal(PlaybackQualityReportResult.Fail, report.Result);
        Assert.Equal("evidence-collection", report.Analysis.PrimaryFailureArea);
        Assert.Contains(report.Checks, check =>
            check.Name == "SampleWindowCoverage" &&
            check.Status == "fail" &&
            check.FailureArea == "evidence-collection" &&
            check.FailureClass == PlaybackQualityFailureClassification.InsufficientInstrumentation);
    }

    [Fact]
    public void Evaluate_Fails_When_Hdr10_Output_Uses_Non_Ten_Bit_SwapChain()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "hdr10-eight-bit-swapchain",
            Expected = new PlaybackQualityExpected
            {
                HdrOutput = "Hdr10",
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
                RequireValidatedConversion = false
            },
            ColorPipeline = new PlaybackQualityColorPipeline
            {
                ActualHdrOutput = "Hdr10",
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
                IsTenBitSwapChain = false
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "IsTenBitSwapChain False did not match expected True.",
            report.FailureReasons);
        Assert.Equal("color-pipeline", report.Analysis.PrimaryFailureArea);
        Assert.Contains("colorPipeline.isTenBitSwapChain", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "IsTenBitSwapChain" &&
            check.Signal == "colorPipeline.isTenBitSwapChain" &&
            check.Expected == "True" &&
            check.Actual == "False" &&
            check.Status == "fail" &&
            check.FailureArea == "color-pipeline");
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
            check.FailureArea == "startup" &&
            check.FailureClass == "player-core bug");
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
            check.FailureArea == "startup" &&
            check.FailureClass == "insufficient instrumentation");
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
    public void Evaluate_Fails_When_Source_Color_Metadata_Does_Not_Match_Expected_Source()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "wrong-source-color-metadata",
            Expected = new PlaybackQualityExpected
            {
                VideoRange = "HDR10",
                ColorPrimaries = "bt2020",
                ColorTransfer = "smpte2084",
                ColorSpace = "bt2020nc",
                RequireValidatedConversion = false
            },
            Source = new PlaybackQualitySource
            {
                VideoRange = "SDR",
                ColorPrimaries = "bt709",
                ColorTransfer = "bt709",
                ColorSpace = "bt709"
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Equal("unsupported-source", report.Analysis.PrimaryFailureArea);
        Assert.Contains("source.videoRange", report.Analysis.RelevantSignals);
        Assert.Contains("source.colorPrimaries", report.Analysis.RelevantSignals);
        Assert.Contains("source.colorTransfer", report.Analysis.RelevantSignals);
        Assert.Contains("source.colorSpace", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedVideoRange" &&
            check.Signal == "source.videoRange" &&
            check.Expected == "HDR10" &&
            check.Actual == "SDR" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedColorPrimaries" &&
            check.Signal == "source.colorPrimaries" &&
            check.Expected == "bt2020" &&
            check.Actual == "bt709" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedColorTransfer" &&
            check.Signal == "source.colorTransfer" &&
            check.Expected == "smpte2084" &&
            check.Actual == "bt709" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedColorSpace" &&
            check.Signal == "source.colorSpace" &&
            check.Expected == "bt2020nc" &&
            check.Actual == "bt709" &&
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
            check.FailureArea == "unsupported-source" &&
            check.FailureClass == "unsupported by current MVP");
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedDolbyVisionProfile" &&
            check.Signal == "source.dolbyVisionProfile" &&
            check.Expected == "8" &&
            check.Actual == "5" &&
            check.Status == "fail" &&
            check.FailureArea == "unsupported-source");
    }

    [Fact]
    public void Evaluate_Marks_Expected_Unsupported_Source_Without_Color_Conversion_Failure()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "dv-profile5-unsupported",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 60,
                HdrKind = "DolbyVisionUnsupported",
                HdrPlaybackStrategy = "Dolby Vision unsupported",
                IsHdr = true,
                IsDirectPlayable = false,
                IsDolbyVision = true,
                DolbyVisionProfile = 5,
                HasHdr10BaseLayer = false,
                HasHlgBaseLayer = false,
                MaxStartupDurationMs = 3000,
                RequireValidatedConversion = true
            },
            Source = new PlaybackQualitySource
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 60,
                HdrKind = "DolbyVisionUnsupported",
                HdrPlaybackStrategy = "Dolby Vision unsupported",
                IsHdr = true,
                IsDirectPlayable = false,
                IsDolbyVision = true,
                DolbyVisionProfile = 5,
                HasHdr10BaseLayer = false,
                HasHlgBaseLayer = false
            },
            Startup = new PlaybackQualityStartup
            {
                StartupDurationMs = 250
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("unsupported", report.Result);
        Assert.Empty(report.FailureReasons);
        Assert.Equal("unsupported-source", report.Analysis.PrimaryFailureArea);
        Assert.Equal(
            "Source is expected to be unsupported; preserve source classification evidence and skip playback-quality thresholds.",
            report.Analysis.SuggestedNextAction);
        Assert.Contains(report.Checks, check =>
            check.Name == "ExpectedIsDirectPlayable" &&
            check.Signal == "source.isDirectPlayable" &&
            check.Status == "pass");
        Assert.DoesNotContain(report.Checks, check =>
            check.Signal == "colorPipeline.conversionStatus");
    }

    [Fact]
    public void Evaluate_Does_Not_Apply_Startup_Threshold_To_Expected_Unsupported_Source()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "dv-profile5-unsupported-slow-probe",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                HdrKind = "DolbyVisionUnsupported",
                HdrPlaybackStrategy = "Dolby Vision unsupported",
                IsDirectPlayable = false,
                IsDolbyVision = true,
                DolbyVisionProfile = 5,
                MaxStartupDurationMs = 3000
            },
            Source = new PlaybackQualitySource
            {
                Codec = "hevc",
                HdrKind = "DolbyVisionUnsupported",
                HdrPlaybackStrategy = "Dolby Vision unsupported",
                IsDirectPlayable = false,
                IsDolbyVision = true,
                DolbyVisionProfile = 5
            },
            Startup = new PlaybackQualityStartup
            {
                StartupDurationMs = 3972
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("unsupported", report.Result);
        Assert.Empty(report.FailureReasons);
        Assert.DoesNotContain(report.Checks, check => check.Name == "StartupDurationMs");
    }

    [Fact]
    public void Evaluate_Fails_When_Expected_Unsupported_Source_Has_Failed_Lifecycle_Operation()
    {
        const string message = "seek failed for unsupported source";
        var report = new PlaybackQualityReport
        {
            RunId = "unsupported-seek-failed",
            Expected = new PlaybackQualityExpected
            {
                IsDirectPlayable = false,
                RequireValidatedConversion = false
            }
        };
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "seek",
            Status = "failed",
            Message = message
        });

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(message, report.FailureReasons);
        Assert.Contains(report.Checks, check =>
            check.Signal == "lifecycle.seek" &&
            check.Status == "fail" &&
            check.FailureArea == "timeline" &&
            check.Message == message);
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
    public void Evaluate_Fails_When_Render_Cadence_Is_Faster_Than_Source_Frame_Duration()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "under-paced-video-clock",
            Expected = new PlaybackQualityExpected
            {
                RequireMatchedDisplayRefreshRate = true,
                RequireValidatedConversion = false
            },
            Source = new PlaybackQualitySource
            {
                FrameRate = 24
            },
            Display = new PlaybackQualityDisplay
            {
                RefreshRateHz = 24
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 72,
                ExpectedFrameDurationMs = 1000.0 / 24.0,
                RenderIntervalMsP95 = 16.7,
                RenderIntervalMsP99 = 17.0
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "RenderIntervalMsP95 16.700 was below minimum cadence interval 31.250.",
            report.FailureReasons);
        Assert.Equal("frame-pacing", report.Analysis.PrimaryFailureArea);
        Assert.Contains("timing.renderIntervalMsP95", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "RenderIntervalMsP95Cadence" &&
            check.Signal == "timing.renderIntervalMsP95" &&
            check.Expected == ">= 31.250" &&
            check.Actual == "16.700" &&
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
    public void Evaluate_Skips_Audio_Video_Drift_Threshold_When_Source_Is_Video_Only()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "video-only-av-drift",
            Expected = new PlaybackQualityExpected
            {
                MaxAudioVideoDriftMsP95 = 40,
                RequireValidatedConversion = false
            },
            Tracks = new PlaybackQualityTracks
            {
                VideoTrackCount = 1,
                AudioTrackCount = 0
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

        Assert.Equal("pass", report.Result);
        Assert.Empty(report.FailureReasons);
        Assert.Equal("none", report.Analysis.PrimaryFailureArea);
        Assert.DoesNotContain(report.Checks, check => check.Name == "AudioVideoDriftMsP95");
        Assert.DoesNotContain("sync.audioVideoDriftMsP95", report.Analysis.RelevantSignals);
    }

    [Fact]
    public void Evaluate_Fails_When_Seek_Position_Error_Exceeds_Threshold()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "seek-lands-late",
            Expected = new PlaybackQualityExpected
            {
                MaxSeekPositionErrorMs = 250,
                RequireValidatedConversion = false
            },
            Position = new PlaybackQualityPosition
            {
                SeekTargetPositionTicks = 600_000_000,
                ActualPositionTicks = 610_000_000
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Equal(1000.0, report.Position.SeekPositionErrorMs);
        Assert.Contains(
            "SeekPositionErrorMs 1000.000 exceeded MaxSeekPositionErrorMs 250.000.",
            report.FailureReasons);
        Assert.Equal("timeline", report.Analysis.PrimaryFailureArea);
        Assert.Equal(
            "Inspect seek/resume timeline state, demux seek completion, and playback position reporting.",
            report.Analysis.SuggestedNextAction);
        Assert.Contains("position.seekPositionErrorMs", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Name == "SeekPositionErrorMs" &&
            check.Signal == "position.seekPositionErrorMs" &&
            check.Expected == "250.000" &&
            check.Actual == "1000.000" &&
            check.Status == "fail" &&
            check.FailureArea == "timeline" &&
            check.FailureClass == "player-core bug");
    }

    [Fact]
    public void Evaluate_Fails_When_Seek_Recovery_Exceeds_Threshold()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "seek-recovers-slowly",
            Expected = new PlaybackQualityExpected
            {
                MaxSeekRecoveryDurationMs = 2000,
                RequireValidatedConversion = false
            },
            Position = new PlaybackQualityPosition
            {
                SeekOperationDurationMs = 4800,
                SeekRecoveryDurationMs = 5100
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "SeekRecoveryDurationMs 5100.000 exceeded MaxSeekRecoveryDurationMs 2000.000.",
            report.FailureReasons);
        Assert.Contains(report.Checks, check =>
            check.Name == "SeekRecoveryDurationMs" &&
            check.Signal == "position.seekRecoveryDurationMs" &&
            check.Status == "fail" &&
            check.FailureArea == "timeline");
    }

    [Fact]
    public void Evaluate_Fails_When_Native_Seek_Evidence_Is_Incomplete()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "native-seek-missing-evidence",
            Expected = new PlaybackQualityExpected
            {
                MaxSeekPositionErrorMs = 250,
                RequireValidatedConversion = false
            },
            Execution = new PlaybackQualityExecutionEvidence
            {
                EvidenceLevel = PlaybackQualityEvidenceLevel.NativePlayback
            },
            Position = new PlaybackQualityPosition
            {
                SeekTargetPositionTicks = 10_000_000,
                ActualPositionTicks = 10_000_000
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(report.Checks, check =>
            check.Signal == "position.seekDemuxTargetTicks" &&
            check.FailureClass == PlaybackQualityFailureClassification.InsufficientInstrumentation);
        Assert.Contains(report.Checks, check => check.Signal == "position.firstPresentedPositionTicks");
        Assert.Contains(report.Checks, check => check.Signal == "position.postSeekAdvanced");
        Assert.Contains(report.Checks, check => check.Signal == "source.containerStartTimeTicks");
    }

    [Fact]
    public void Evaluate_Passes_Complete_Normalized_Native_Seek_Evidence()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "native-seek-complete",
            Expected = new PlaybackQualityExpected
            {
                MaxSeekPositionErrorMs = 100,
                RequireValidatedConversion = false
            },
            Execution = new PlaybackQualityExecutionEvidence
            {
                EvidenceLevel = PlaybackQualityEvidenceLevel.NativePlayback
            },
            Source = new PlaybackQualitySource
            {
                DurationTicks = 60_000_000,
                ContainerStartTimeTicks = 14_000_000,
                VideoStreamStartTimeTicks = 14_213_333
            },
            Position = new PlaybackQualityPosition
            {
                SeekTargetPositionTicks = 10_000_000,
                SeekDemuxTargetTicks = 24_000_000,
                ActualPositionTicks = 10_213_333,
                FirstPresentedPositionTicks = 10_213_333,
                PostSeekPositionTicks = 20_000_000,
                PostSeekAdvanced = true,
                SeekOperationDurationMs = 120,
                SeekRecoveryDurationMs = 150,
                SeekPacketCacheEnabled = false,
                SeekPacketCacheHit = false,
                SeekPacketCachePacketCount = 0,
                SeekPacketCacheBytes = 0,
                SeekPacketCacheWindowDurationTicks = 0,
                SeekFallbackReason = "disabled"
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("pass", report.Result);
        Assert.Empty(report.FailureReasons);
        Assert.Equal(21.3333, report.Position.SeekPositionErrorMs!.Value, 4);
    }

    [Fact]
    public void Evaluate_Uses_Explicit_Sdr_Fallback_When_Hdr_Display_Is_Unavailable()
    {
        var report = CreateEnvironmentAwareHdrReport();
        report.Display.HdrStatus = "Off";
        report.Display.IsHdrDisplayAvailable = false;
        report.ColorPipeline.ActualHdrOutput = "Sdr";
        report.ColorPipeline.DxgiInput = "YCBCR_STUDIO_G22_LEFT_P2020";
        report.ColorPipeline.DxgiOutput = "RGB_FULL_G22_NONE_P709";
        report.ColorPipeline.ConversionStatus = "validated;tone-mapped-hable";
        report.ColorPipeline.IsTenBitSwapChain = false;

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("pass", report.Result);
        Assert.Equal("sdr-display-fallback", report.ColorPipeline.ExpectationProfile);
        Assert.DoesNotContain(report.Checks, check => check.Name == "IsTenBitSwapChain");
        Assert.Contains(report.Checks, check =>
            check.Signal == "colorPipeline.expectationProfile" &&
            check.Actual == "sdr-display-fallback" &&
            check.Status == "observed");
    }

    [Fact]
    public void Evaluate_Uses_Primary_Color_Expectation_On_Hdr_Display()
    {
        var report = CreateEnvironmentAwareHdrReport();
        report.Display.HdrStatus = "On";
        report.Display.IsHdrDisplayAvailable = true;
        report.ColorPipeline.ActualHdrOutput = "Hdr10";
        report.ColorPipeline.DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020";
        report.ColorPipeline.DxgiOutput = "RGB_FULL_G2084_NONE_P2020";
        report.ColorPipeline.ConversionStatus = "validated";
        report.ColorPipeline.IsTenBitSwapChain = true;

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("pass", report.Result);
        Assert.Equal("primary", report.ColorPipeline.ExpectationProfile);
    }

    [Fact]
    public void Evaluate_Does_Not_Require_Hdr_Fallback_For_Forced_Sdr_Source()
    {
        var report = new PlaybackQualityReport
        {
            Expected = new PlaybackQualityExpected
            {
                HdrOutput = "Sdr",
                DxgiInput = "YCBCR_STUDIO_G22_LEFT_P709",
                DxgiOutput = "RGB_FULL_G22_NONE_P709",
                RequireValidatedConversion = true
            },
            ColorPipeline = new PlaybackQualityColorPipeline
            {
                ForceSdrOutput = true,
                ActualHdrOutput = "Sdr",
                DxgiInput = "YCBCR_STUDIO_G22_LEFT_P709",
                DxgiOutput = "RGB_FULL_G22_NONE_P709",
                ConversionStatus = "validated"
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("pass", report.Result);
        Assert.Equal("primary", report.ColorPipeline.ExpectationProfile);
    }

    [Fact]
    public void Evaluate_Fails_When_Sdr_Environment_Requires_Undeclared_Fallback()
    {
        var report = CreateEnvironmentAwareHdrReport();
        report.Expected!.SdrDisplayFallback = null;
        report.Display.HdrStatus = "Off";
        report.ColorPipeline.ActualHdrOutput = "Sdr";

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "SDR display fallback is required but expected.sdrDisplayFallback is missing.",
            report.FailureReasons);
    }

    [Fact]
    public void Evaluate_Requires_Exact_Conversion_Status_Token_For_Sdr_Fallback()
    {
        var report = CreateEnvironmentAwareHdrReport();
        report.Display.HdrStatus = "Off";
        report.ColorPipeline.ActualHdrOutput = "Sdr";
        report.ColorPipeline.DxgiInput = "YCBCR_STUDIO_G22_TOPLEFT_P2020";
        report.ColorPipeline.DxgiOutput = "RGB_FULL_G22_NONE_P709";
        report.ColorPipeline.ConversionStatus = "validated;requires-tone-mapping";

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(
            "ConversionStatus requires tone-mapped-hable token tone-mapped-hable.",
            report.FailureReasons);
    }

    private static PlaybackQualityReport CreateEnvironmentAwareHdrReport()
    {
        var fallback = new PlaybackQualityColorExpected
        {
            HdrOutput = "Sdr",
            DxgiOutput = "RGB_FULL_G22_NONE_P709",
            RequireValidatedConversion = true,
            RequiredConversionStatus = "tone-mapped-hable"
        };
        fallback.DxgiInputAnyOf.Add("YCBCR_STUDIO_G22_LEFT_P2020");
        fallback.DxgiInputAnyOf.Add("YCBCR_STUDIO_G22_TOPLEFT_P2020");
        return new PlaybackQualityReport
        {
            Expected = new PlaybackQualityExpected
            {
                HdrOutput = "Hdr10",
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
                RequireValidatedConversion = true,
                SdrDisplayFallback = fallback
            }
        };
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
    public void Evaluate_Fails_When_Subtitle_Switch_Lifecycle_Operation_Fails()
    {
        const string message = "no subtitle cue was rendered after switching";
        var report = new PlaybackQualityReport
        {
            RunId = "subtitle-switch-failed",
            Expected = new PlaybackQualityExpected
            {
                RequireValidatedConversion = false
            }
        };
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "subtitle-switch",
            Status = "failed",
            PositionTicks = 30_000_000,
            Message = message
        });

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Equal("subtitles", report.Analysis.PrimaryFailureArea);
        Assert.Contains(message, report.FailureReasons);
        Assert.Contains("lifecycle.subtitle-switch", report.Analysis.RelevantSignals);
        Assert.Contains(report.Checks, check =>
            check.Signal == "lifecycle.subtitle-switch" &&
            check.Status == "fail" &&
            check.FailureArea == "subtitles" &&
            check.Message == message);
    }

    [Theory]
    [InlineData("audio-switch", "tracks")]
    [InlineData("seek", "timeline")]
    [InlineData("pause", "playback-lifecycle")]
    public void Evaluate_Maps_Failed_Lifecycle_Operation_To_Failure_Area(
        string operation,
        string failureArea)
    {
        var report = new PlaybackQualityReport
        {
            RunId = operation + "-failed",
            Expected = new PlaybackQualityExpected
            {
                RequireValidatedConversion = false
            }
        };
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = operation,
            Status = "failed",
            Message = operation + " failed"
        });

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Equal(failureArea, report.Analysis.PrimaryFailureArea);
        Assert.Contains(report.Checks, check =>
            check.Signal == "lifecycle." + operation &&
            check.Status == "fail" &&
            check.FailureArea == failureArea);
    }

    [Fact]
    public void Evaluate_Uses_Stable_Default_Message_When_Lifecycle_Error_Has_No_Message()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "subtitle-off-error",
            Expected = new PlaybackQualityExpected
            {
                RequireValidatedConversion = false
            }
        };
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "subtitle-off",
            Status = "error"
        });

        PlaybackQualityEvaluator.Evaluate(report);

        const string message = "Lifecycle operation subtitle-off reported error.";
        Assert.Equal("fail", report.Result);
        Assert.Contains(message, report.FailureReasons);
        Assert.Contains(report.Checks, check =>
            check.Signal == "lifecycle.subtitle-off" &&
            check.Status == "fail" &&
            check.FailureArea == "subtitles" &&
            check.Message == message);
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("skipped")]
    public void Evaluate_Does_Not_Fail_For_Non_Failing_Lifecycle_Status(string status)
    {
        var report = new PlaybackQualityReport
        {
            RunId = "seek-" + status,
            Expected = new PlaybackQualityExpected
            {
                RequireValidatedConversion = false
            }
        };
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "seek",
            Status = status,
            Message = "seek " + status
        });

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("pass", report.Result);
        Assert.DoesNotContain(report.Checks, check => check.Signal == "lifecycle.seek");
    }

    [Theory]
    [InlineData("audio-switch", "tracks")]
    [InlineData("subtitle-switch", "subtitles")]
    public void Evaluate_Fails_When_Interaction_Recovery_Exceeds_Expected_Maximum(
        string scenario,
        string failureArea)
    {
        var report = new PlaybackQualityReport
        {
            RunId = scenario + "-slow-recovery",
            Expected = new PlaybackQualityExpected
            {
                MaxInteractionRecoveryDurationMs = 2000,
                RequireValidatedConversion = false
            },
            Interaction = new PlaybackQualityInteractionEvidence
            {
                Scenario = scenario,
                Attempted = true,
                OperationDurationMs = 310,
                RecoveryDurationMs = 5009,
                PositionDeltaTicks = 710000,
                SubmittedAudioFrameDelta = scenario == "audio-switch" ? 24UL : null
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Equal(failureArea, report.Analysis.PrimaryFailureArea);
        Assert.Contains(report.Checks, check =>
            check.Name == "InteractionRecoveryDurationMs" &&
            check.Signal == "interaction.recoveryDurationMs" &&
            check.Status == "fail" &&
            check.FailureArea == failureArea &&
            check.Expected == "2000.000" &&
            check.Actual == "5009.000");
    }

    [Fact]
    public void Evaluate_Fails_When_Expected_Interaction_Recovery_Evidence_Is_Missing()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "audio-switch-missing-recovery",
            Expected = new PlaybackQualityExpected
            {
                MaxInteractionRecoveryDurationMs = 2000,
                RequireValidatedConversion = false
            },
            Interaction = new PlaybackQualityInteractionEvidence
            {
                Scenario = "audio-switch",
                Attempted = true
            }
        };

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Equal("tracks", report.Analysis.PrimaryFailureArea);
        Assert.Contains(report.Checks, check =>
            check.Name == "InteractionRecoveryDurationMs" &&
            check.Signal == "interaction.recoveryDurationMs" &&
            check.Status == "fail" &&
            check.FailureClass == PlaybackQualityFailureClassification.InsufficientInstrumentation);
    }

    [Fact]
    public void Serializer_RoundTrips_Report_With_CamelCase_Names()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "roundtrip",
            Result = "pass",
            Source = new PlaybackQualitySource { ItemId = "item-1", MediaSourceId = "source-1" },
            Interaction = new PlaybackQualityInteractionEvidence
            {
                Scenario = "subtitle-switch",
                Attempted = true,
                OperationDurationMs = 310,
                LockWaitDurationMs = 250,
                ExecutionDurationMs = 60,
                QuiesceDurationMs = 10,
                SeekDurationMs = 20,
                DecoderOpenDurationMs = 20,
                RendererOpenDurationMs = 10,
                PacketCacheHit = true,
                PacketCacheEnabled = true,
                PacketCachePacketCount = 120,
                PacketCacheBytes = 524288,
                PacketCacheWindowDurationTicks = 150000000,
                RecoveryDurationMs = 875,
                CueRenderDurationMs = 5009,
                PositionDeltaTicks = 710000,
                RenderedVideoFrameDelta = 24,
                SubtitleCueRenderCountDelta = 1
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "MaxFrameGapMs",
            Status = "fail",
            FailureArea = "frame-pacing",
            FailureClass = "player-core bug",
            Signal = "timing.maxFrameGapMs"
        });

        var json = PlaybackQualityReportSerializer.Serialize(report);
        var parsed = PlaybackQualityReportSerializer.Deserialize(json);

        Assert.Contains("\"schemaVersion\"", json);
        Assert.Contains("\"metricVersion\"", json);
        Assert.Contains("\"runId\"", json);
        Assert.Contains("\"analysis\"", json);
        Assert.Contains("\"checks\"", json);
        Assert.Contains("\"failureClass\"", json);
        Assert.Contains("\"limitations\"", json);
        Assert.Contains("\"interaction\"", json);
        Assert.Contains("\"recoveryDurationMs\": 875", json);
        Assert.Contains("\"lockWaitDurationMs\": 250", json);
        Assert.Contains("\"executionDurationMs\": 60", json);
        Assert.Contains("\"seekDurationMs\": 20", json);
        Assert.Contains("\"decoderOpenDurationMs\": 20", json);
        Assert.Contains("\"packetCacheHit\": true", json);
        Assert.Contains("\"packetCacheEnabled\": true", json);
        Assert.Contains("\"packetCachePacketCount\": 120", json);
        Assert.Contains("\"cueRenderDurationMs\": 5009", json);
        Assert.Contains("\"renderedVideoFrameDelta\": 24", json);
        Assert.Contains("\"subtitleCueRenderCountDelta\": 1", json);
        Assert.Equal("roundtrip", parsed.RunId);
        Assert.Equal("source-1", parsed.Source.MediaSourceId);
        Assert.Equal("subtitle-switch", parsed.Interaction.Scenario);
        Assert.Equal(875, parsed.Interaction.RecoveryDurationMs);
        Assert.Equal(250, parsed.Interaction.LockWaitDurationMs);
        Assert.Equal(60, parsed.Interaction.ExecutionDurationMs);
        Assert.Equal(10, parsed.Interaction.QuiesceDurationMs);
        Assert.Equal(20, parsed.Interaction.SeekDurationMs);
        Assert.Equal(20, parsed.Interaction.DecoderOpenDurationMs);
        Assert.Equal(10, parsed.Interaction.RendererOpenDurationMs);
        Assert.True(parsed.Interaction.PacketCacheHit);
        Assert.True(parsed.Interaction.PacketCacheEnabled);
        Assert.Equal(120UL, parsed.Interaction.PacketCachePacketCount);
        Assert.Equal(524288UL, parsed.Interaction.PacketCacheBytes);
        Assert.Equal(150000000, parsed.Interaction.PacketCacheWindowDurationTicks);
        Assert.Equal(5009, parsed.Interaction.CueRenderDurationMs);
        Assert.Equal(24UL, parsed.Interaction.RenderedVideoFrameDelta);
        Assert.Equal(1UL, parsed.Interaction.SubtitleCueRenderCountDelta);
    }

    private static PlaybackQualityReport CreateSampleWindowReport(ulong renderedVideoFrames)
    {
        return new PlaybackQualityReport
        {
            RunId = "sample-window",
            Expected = new PlaybackQualityExpected
            {
                RequireValidatedConversion = false
            },
            Execution = new PlaybackQualityExecutionEvidence
            {
                Scenario = PlaybackQualityExecutionScenario.Playback,
                Status = PlaybackQualityExecutionStatus.Completed,
                RequestedSampleDurationMs = 5000,
                ObservedSampleWallClockDurationMs = 5000,
                PlaybackSampleObserved = true
            },
            Source = new PlaybackQualitySource
            {
                FrameRate = 60
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = renderedVideoFrames
            }
        };
    }
}
