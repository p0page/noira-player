using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityReportAnalyzerTests
{
    [Fact]
    public void Analyze_Preserves_Secondary_Failure_Areas_For_Model_Triage()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "hdr-and-pacing",
            Result = "fail",
            Analysis = new PlaybackQualityAnalysis
            {
                PrimaryFailureArea = "color-pipeline",
                SuggestedNextAction = "Inspect HDR display switch and DXGI color-space mapping."
            }
        };
        report.FailureReasons.Add("ActualHdrOutput Sdr did not match expected Hdr10.");
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "ActualHdrOutput",
            Status = "fail",
            FailureArea = "color-pipeline",
            Signal = "colorPipeline.actualHdrOutput",
            Expected = "Hdr10",
            Actual = "Sdr",
            Message = "ActualHdrOutput Sdr did not match expected Hdr10."
        });
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "MaxFrameGapMs",
            Status = "fail",
            FailureArea = "frame-pacing",
            Signal = "timing.maxFrameGapMs",
            Expected = "105.000",
            Actual = "180.000",
            Message = "MaxFrameGapMs 180.000 exceeded MaxFrameGapMs 105.000."
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("hdr-and-pacing", analysis.RunId);
        Assert.Equal("fail", analysis.Result);
        Assert.Equal("color-pipeline", analysis.PrimaryFailureArea);
        Assert.Equal(new[] { "color-pipeline", "frame-pacing" }, analysis.FailureAreas);
        Assert.Contains("colorPipeline.actualHdrOutput", analysis.EvidenceSignals);
        Assert.Contains("timing.maxFrameGapMs", analysis.EvidenceSignals);
        Assert.Contains("ActualHdrOutput Sdr did not match expected Hdr10.", analysis.FailureReasons);
        Assert.Contains(analysis.FailedChecks, check =>
            check.Name == "MaxFrameGapMs" &&
            check.Expected == "105.000" &&
            check.Actual == "180.000");
        Assert.Equal("Inspect HDR display switch and DXGI color-space mapping.", analysis.SuggestedNextAction);
    }

    [Fact]
    public void Analyze_Reports_Missing_Evidence_For_Unset_Critical_Signals()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-evidence",
            Result = "observed"
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Contains("source.codec", analysis.MissingEvidence);
        Assert.Contains("source.frameRate", analysis.MissingEvidence);
        Assert.Contains("timing.renderedVideoFrames", analysis.MissingEvidence);
        Assert.Contains("sync.audioVideoDriftMsP95", analysis.MissingEvidence);
        Assert.Contains("buffers.queuedAudioBuffers", analysis.MissingEvidence);
        Assert.Contains("colorPipeline.dxgiInput", analysis.MissingEvidence);
        Assert.Contains("display.hdrStatus", analysis.MissingEvidence);
        Assert.Contains("display.refreshRateHz", analysis.MissingEvidence);
        Assert.Contains("software-only: does not verify actual HDMI InfoFrame output", analysis.Limitations);
    }

    [Fact]
    public void Analyze_Reports_Missing_Frame_Duration_When_Source_Frame_Rate_Is_Known()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-frame-duration",
            Result = "observed",
            Source = new PlaybackQualitySource
            {
                Codec = "hevc",
                FrameRate = 23.976
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 10
            },
            ColorPipeline = new PlaybackQualityColorPipeline
            {
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020"
            },
            Display = new PlaybackQualityDisplay
            {
                HdrStatus = "On",
                RefreshRateHz = 59.94006
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Contains("timing.expectedFrameDurationMs", analysis.MissingEvidence);
    }

    [Fact]
    public void Analyze_Reports_Missing_Startup_Duration_When_Startup_Threshold_Is_Expected()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-startup-duration",
            Result = "observed",
            Expected = new PlaybackQualityExpected
            {
                MaxStartupDurationMs = 2000
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Contains("startup.startupDurationMs", analysis.MissingEvidence);
    }

    [Fact]
    public void Analyze_Reports_Missing_Render_Intervals_When_Interval_Thresholds_Are_Expected()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-render-intervals",
            Result = "observed",
            Expected = new PlaybackQualityExpected
            {
                MaxRenderIntervalMsP95 = 50,
                MaxRenderIntervalMsP99 = 70
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Contains("timing.renderIntervalMsP95", analysis.MissingEvidence);
        Assert.Contains("timing.renderIntervalMsP99", analysis.MissingEvidence);
    }

    [Fact]
    public void Analyze_Reports_Missing_Max_Frame_Gap_When_Max_Frame_Gap_Threshold_Is_Expected()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-max-frame-gap",
            Result = "observed",
            Expected = new PlaybackQualityExpected
            {
                MaxFrameGapMs = 105
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Contains("timing.maxFrameGapMs", analysis.MissingEvidence);
    }

    [Fact]
    public void Analyze_Reports_Missing_Audio_Video_Drift_When_Drift_Threshold_Is_Expected()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-av-drift",
            Result = "observed",
            Expected = new PlaybackQualityExpected
            {
                MaxAudioVideoDriftMsP95 = 40
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Contains("sync.audioVideoDriftMsP95", analysis.MissingEvidence);
    }

    [Fact]
    public void Analyze_Reports_Missing_Color_Pipeline_Evidence_When_Color_Expectations_Are_Set()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-color-pipeline",
            Result = "observed",
            Expected = new PlaybackQualityExpected
            {
                HdrOutput = "Hdr10",
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
                RequireValidatedConversion = true
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Contains("colorPipeline.actualHdrOutput", analysis.MissingEvidence);
        Assert.Contains("colorPipeline.dxgiInput", analysis.MissingEvidence);
        Assert.Contains("colorPipeline.dxgiOutput", analysis.MissingEvidence);
        Assert.Contains("colorPipeline.conversionStatus", analysis.MissingEvidence);
    }

    [Fact]
    public void Analyze_Adds_Expected_Frame_Duration_As_Frame_Pacing_Evidence()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "pacing-evidence",
            Result = "fail",
            Timing = new PlaybackQualityTiming
            {
                ExpectedFrameDurationMs = 41.708,
                MaxFrameGapMs = 125
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "MaxFrameGapMs",
            Status = "fail",
            FailureArea = "frame-pacing",
            Signal = "timing.maxFrameGapMs"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Contains("timing.maxFrameGapMs", analysis.EvidenceSignals);
        Assert.Contains("timing.expectedFrameDurationMs", analysis.EvidenceSignals);
    }

    [Fact]
    public void Serializer_Writes_Model_Analysis_With_CamelCase_Field_Names()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "json-analysis",
            Result = "fail"
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Status = "fail",
            FailureArea = "av-sync",
            Signal = "sync.audioVideoDriftMsP95"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);
        var json = PlaybackQualityReportSerializer.Serialize(analysis);

        Assert.Contains("\"runId\"", json);
        Assert.Contains("\"primaryFailureArea\"", json);
        Assert.Contains("\"failureAreas\"", json);
        Assert.Contains("\"evidenceSignals\"", json);
        Assert.Contains("\"missingEvidence\"", json);
        Assert.Contains("\"sync.audioVideoDriftMsP95\"", json);
    }
}
