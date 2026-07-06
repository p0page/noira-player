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
    public void Analyze_Adds_Investigation_Hints_For_Each_Failure_Area()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "model-hints",
            Result = "fail",
            Analysis = new PlaybackQualityAnalysis
            {
                PrimaryFailureArea = "color-pipeline"
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "ActualHdrOutput",
            Status = "fail",
            FailureArea = "color-pipeline",
            Signal = "colorPipeline.actualHdrOutput",
            Expected = "Hdr10",
            Actual = "Sdr"
        });
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "MaxFrameGapMs",
            Status = "fail",
            FailureArea = "frame-pacing",
            Signal = "timing.maxFrameGapMs",
            Expected = "105.000",
            Actual = "180.000"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("color-pipeline", analysis.InvestigationHints[0].FailureArea);
        Assert.Contains(analysis.InvestigationHints, hint =>
            hint.FailureArea == "color-pipeline" &&
            hint.CodeTargets.Contains("src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.cpp") &&
            hint.CodeTargets.Contains("src/NextGenEmby.Native/DxDeviceResources.cpp") &&
            hint.Signals.Contains("colorPipeline.actualHdrOutput"));
        Assert.Contains(analysis.InvestigationHints, hint =>
            hint.FailureArea == "frame-pacing" &&
            hint.CodeTargets.Contains("src/NextGenEmby.Native/Media/FramePacing.h") &&
            hint.CodeTargets.Contains("src/NextGenEmby.Native/Media/PlaybackGraph.cpp") &&
            hint.CodeTargets.Contains("src/NextGenEmby.Core/PlaybackQuality/PlaybackRefreshRatePolicy.cs") &&
            hint.Signals.Contains("timing.maxFrameGapMs"));
    }

    [Fact]
    public void Analyze_Adds_Evidence_Collection_Hint_When_Failed_Report_Has_Missing_Evidence()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "missing-while-failed",
            Result = "fail",
            Analysis = new PlaybackQualityAnalysis
            {
                PrimaryFailureArea = "frame-pacing"
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240
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

        Assert.Contains(analysis.InvestigationHints, hint =>
            hint.FailureArea == "frame-pacing");
        Assert.Contains(analysis.InvestigationHints, hint =>
            hint.FailureArea == "evidence-collection" &&
            hint.CodeTargets.Contains("src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportMapper.cs") &&
            hint.Signals.Contains("colorPipeline.dxgiInput"));
    }

    [Fact]
    public void Analyze_Orders_Triage_Steps_By_Failure_Priority_For_Multi_Failure_Report()
    {
        var report = CreateOptimizationReadyFailure();
        report.Analysis.PrimaryFailureArea = "color-pipeline";
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "ActualHdrOutput",
            Status = "fail",
            FailureArea = "color-pipeline",
            Signal = "colorPipeline.actualHdrOutput",
            Expected = "Hdr10",
            Actual = "Sdr"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal(2, analysis.TriageSteps.Count);
        Assert.Equal(1, analysis.TriageSteps[0].Rank);
        Assert.Equal("failure", analysis.TriageSteps[0].Kind);
        Assert.Equal("color-pipeline", analysis.TriageSteps[0].FailureArea);
        Assert.Contains("colorPipeline.actualHdrOutput", analysis.TriageSteps[0].Signals);
        Assert.Contains("src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.cpp", analysis.TriageSteps[0].CodeTargets);
        Assert.Equal(2, analysis.TriageSteps[1].Rank);
        Assert.Equal("frame-pacing", analysis.TriageSteps[1].FailureArea);
    }

    [Fact]
    public void Analyze_Puts_Evidence_Collection_First_When_Optimization_Is_Blocked_By_Missing_Evidence()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "blocked-triage",
            Result = "fail",
            Analysis = new PlaybackQualityAnalysis
            {
                PrimaryFailureArea = "frame-pacing"
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240,
                MaxFrameGapMs = 180
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "MaxFrameGapMs",
            Status = "fail",
            FailureArea = "frame-pacing",
            Signal = "timing.maxFrameGapMs",
            Expected = "105.000",
            Actual = "180.000"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("blocked", analysis.OptimizationGate.Status);
        Assert.NotEmpty(analysis.TriageSteps);
        Assert.Equal(1, analysis.TriageSteps[0].Rank);
        Assert.Equal("blocker", analysis.TriageSteps[0].Kind);
        Assert.Equal("evidence-collection", analysis.TriageSteps[0].FailureArea);
        Assert.Contains("colorPipeline.dxgiInput", analysis.TriageSteps[0].Signals);
        Assert.Contains("Collect missing telemetry", analysis.TriageSteps[0].SuggestedAction);
        Assert.Equal(2, analysis.TriageSteps[1].Rank);
        Assert.Equal("failure", analysis.TriageSteps[1].Kind);
        Assert.Equal("frame-pacing", analysis.TriageSteps[1].FailureArea);
    }

    [Fact]
    public void Analyze_Marks_Sample_Insufficient_When_Rendered_Frames_Are_Below_Minimum()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "short-sample",
            Result = "fail",
            Expected = new PlaybackQualityExpected
            {
                MinRenderedVideoFrames = 120
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 24
            },
            Source = new PlaybackQualitySource
            {
                FrameRate = 23.976
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "RenderedVideoFrames",
            Status = "fail",
            FailureArea = "frame-pacing",
            Signal = "timing.renderedVideoFrames",
            Expected = "120",
            Actual = "24"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("insufficient", analysis.Sample.Status);
        Assert.Equal(24UL, analysis.Sample.RenderedVideoFrames);
        Assert.Equal(120, analysis.Sample.MinRenderedVideoFrames);
        Assert.Equal(96, analysis.Sample.AdditionalRenderedFramesRequired);
        Assert.Equal(1001.001, analysis.Sample.ObservedSampleDurationMs, 3);
        Assert.Equal(5005.005, analysis.Sample.MinimumSampleDurationMs, 3);
        Assert.Equal(
            "Rendered frame sample is below the expected minimum; do not tune frame pacing from this run alone.",
            analysis.Sample.Reason);
    }

    [Fact]
    public void Analyze_Marks_Sample_Sufficient_When_Rendered_Frames_Meet_Minimum()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "long-sample",
            Result = "pass",
            Expected = new PlaybackQualityExpected
            {
                MinRenderedVideoFrames = 120
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("sufficient", analysis.Sample.Status);
        Assert.Equal(240UL, analysis.Sample.RenderedVideoFrames);
        Assert.Equal(120, analysis.Sample.MinRenderedVideoFrames);
        Assert.Equal(0, analysis.Sample.AdditionalRenderedFramesRequired);
    }

    [Fact]
    public void Analyze_Blocks_Playback_Core_Optimization_When_Sample_Is_Insufficient()
    {
        var report = CreateOptimizationReadyFailure();
        report.Expected!.MinRenderedVideoFrames = 120;
        report.Timing.RenderedVideoFrames = 24;

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.False(analysis.OptimizationGate.CanOptimizePlaybackCore);
        Assert.Equal("blocked", analysis.OptimizationGate.Status);
        Assert.Contains("sample.insufficient", analysis.OptimizationGate.Blockers);
        Assert.DoesNotContain("frame-pacing", analysis.OptimizationGate.TargetFailureAreas);
    }

    [Fact]
    public void Analyze_Blocks_Playback_Core_Optimization_When_Required_Evidence_Is_Missing()
    {
        var report = CreateOptimizationReadyFailure();
        report.ColorPipeline.DxgiInput = "";

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.False(analysis.OptimizationGate.CanOptimizePlaybackCore);
        Assert.Equal("blocked", analysis.OptimizationGate.Status);
        Assert.Contains("missingEvidence", analysis.OptimizationGate.Blockers);
        Assert.Contains("colorPipeline.dxgiInput", analysis.OptimizationGate.BlockerSignals);
    }

    [Fact]
    public void Analyze_Allows_Playback_Core_Optimization_When_Failing_Run_Has_Sufficient_Sample_And_Evidence()
    {
        var report = CreateOptimizationReadyFailure();

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.True(analysis.OptimizationGate.CanOptimizePlaybackCore);
        Assert.Equal("ready", analysis.OptimizationGate.Status);
        Assert.Empty(analysis.OptimizationGate.Blockers);
        Assert.Contains("frame-pacing", analysis.OptimizationGate.TargetFailureAreas);
    }

    [Fact]
    public void Analyze_Classifies_Frame_Pacing_As_Isolated_Gap_When_Only_Max_Frame_Gap_Fails()
    {
        var report = CreateOptimizationReadyFailure();

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("isolated-gap", analysis.FramePacing.Pattern);
        Assert.Contains("timing.maxFrameGapMs", analysis.FramePacing.Signals);
        Assert.Contains("Single max frame gap failed without sustained render interval failures.", analysis.FramePacing.Reasons);
    }

    [Fact]
    public void Analyze_Classifies_Frame_Pacing_As_Sustained_Jitter_When_P95_Render_Interval_Fails()
    {
        var report = CreateOptimizationReadyFailure();
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "RenderIntervalMsP95",
            Status = "fail",
            FailureArea = "frame-pacing",
            Signal = "timing.renderIntervalMsP95",
            Expected = "52.000",
            Actual = "75.000"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("sustained-jitter", analysis.FramePacing.Pattern);
        Assert.Contains("timing.renderIntervalMsP95", analysis.FramePacing.Signals);
    }

    [Fact]
    public void Analyze_Classifies_Frame_Pacing_As_Refresh_Mismatch_When_Display_Refresh_Fails()
    {
        var report = CreateOptimizationReadyFailure();
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "DisplayRefreshRateHz",
            Status = "fail",
            FailureArea = "frame-pacing",
            Signal = "display.refreshRateHz",
            Expected = "matched to source.frameRate 23.976",
            Actual = "50.000"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("refresh-mismatch", analysis.FramePacing.Pattern);
        Assert.Contains("display.refreshRateHz", analysis.FramePacing.Signals);
    }

    [Fact]
    public void Analyze_Classifies_Frame_Pacing_As_Starvation_Driven_When_Buffering_Fails_With_Frame_Pacing()
    {
        var report = CreateOptimizationReadyFailure();
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "VideoStarvedPasses",
            Status = "fail",
            FailureArea = "buffering",
            Signal = "buffers.videoStarvedPasses",
            Expected = "0",
            Actual = "3"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("starvation-driven", analysis.FramePacing.Pattern);
        Assert.Contains("buffers.videoStarvedPasses", analysis.FramePacing.Signals);
        Assert.Contains("Playback starvation coincided with frame pacing failure.", analysis.FramePacing.Reasons);
    }

    [Fact]
    public void Analyze_Uses_Starvation_Driven_Frame_Pacing_Hint_When_Buffering_Fails_With_Frame_Pacing()
    {
        var report = CreateOptimizationReadyFailure();
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "VideoStarvedPasses",
            Status = "fail",
            FailureArea = "buffering",
            Signal = "buffers.videoStarvedPasses",
            Expected = "0",
            Actual = "3"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Contains(analysis.InvestigationHints, hint =>
            hint.FailureArea == "frame-pacing" &&
            hint.SuggestedAction.Contains("Inspect demux, decode, network supply") &&
            hint.CodeTargets.Contains("src/NextGenEmby.Native/Media/VideoDecoder.cpp") &&
            hint.CodeTargets.Contains("src/NextGenEmby.Native/Media/AudioDecoder.cpp") &&
            hint.CodeTargets.Contains("src/NextGenEmby.Native/Media/AudioRenderer.cpp") &&
            hint.Signals.Contains("buffers.videoStarvedPasses") &&
            hint.Signals.Contains("buffers.audioStarvedPasses") &&
            hint.Signals.Contains("buffers.queuedAudioBuffers"));
    }

    [Fact]
    public void Analyze_Classifies_Frame_Pacing_As_Sample_Insufficient_When_Rendered_Frame_Sample_Is_Too_Short()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "short-frame-sample",
            Result = "fail",
            Analysis = new PlaybackQualityAnalysis
            {
                PrimaryFailureArea = "frame-pacing"
            },
            Expected = new PlaybackQualityExpected
            {
                MinRenderedVideoFrames = 120
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 24
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "RenderedVideoFrames",
            Status = "fail",
            FailureArea = "frame-pacing",
            Signal = "timing.renderedVideoFrames",
            Expected = "120",
            Actual = "24"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("sample-insufficient", analysis.FramePacing.Pattern);
        Assert.Contains("timing.renderedVideoFrames", analysis.FramePacing.Signals);
        Assert.Contains("Frame pacing sample is too short to diagnose timing thresholds.", analysis.FramePacing.Reasons);
        Assert.Contains(analysis.InvestigationHints, hint =>
            hint.FailureArea == "frame-pacing" &&
            hint.SuggestedAction.Contains("Collect a longer rendered-frame sample") &&
            hint.CodeTargets.Contains("src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportComposer.cs") &&
            hint.CodeTargets.Contains("src/NextGenEmby.Native/NativePlaybackQualityMetrics.cpp") &&
            hint.CodeTargets.Contains("src/NextGenEmby.Native/Media/PlaybackGraph.cpp") &&
            hint.Signals.Contains("timing.renderedVideoFrames") &&
            hint.Signals.Contains("sample.status"));
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
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                ConversionStatus = "validated"
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
        Assert.Contains("\"sample\"", json);
        Assert.Contains("\"optimizationGate\"", json);
        Assert.Contains("\"framePacing\"", json);
        Assert.Contains("\"triageSteps\"", json);
        Assert.Contains("\"failureAreas\"", json);
        Assert.Contains("\"investigationHints\"", json);
        Assert.Contains("\"evidenceSignals\"", json);
        Assert.Contains("\"missingEvidence\"", json);
        Assert.Contains("\"sync.audioVideoDriftMsP95\"", json);
    }

    private static PlaybackQualityReport CreateOptimizationReadyFailure()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "optimization-ready",
            Result = "fail",
            Expected = new PlaybackQualityExpected
            {
                MinRenderedVideoFrames = 120
            },
            Source = new PlaybackQualitySource
            {
                Codec = "hevc",
                FrameRate = 23.976,
                HdrKind = "Hdr10"
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240,
                ExpectedFrameDurationMs = 1000.0 / 23.976
            },
            ColorPipeline = new PlaybackQualityColorPipeline
            {
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                ConversionStatus = "validated"
            },
            Display = new PlaybackQualityDisplay
            {
                HdrStatus = "On",
                RefreshRateHz = 59.94006
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "MaxFrameGapMs",
            Status = "fail",
            FailureArea = "frame-pacing",
            Signal = "timing.maxFrameGapMs",
            Expected = "105.000",
            Actual = "180.000"
        });

        return report;
    }
}
