using System.Collections.Generic;
using System.Linq;
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
            FailureClass = "player-core bug",
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
            FailureClass = "player-core bug",
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
        Assert.Equal(new[] { "player-core bug" }, analysis.FailureClasses);
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
            hint.Signals.Contains("colorPipeline.actualHdrOutput") &&
            hint.Signals.Contains("colorPipeline.isTenBitSwapChain"));
        Assert.Contains(analysis.InvestigationHints, hint =>
            hint.FailureArea == "frame-pacing" &&
            hint.CodeTargets.Contains("src/NextGenEmby.Native/Media/FramePacing.h") &&
            hint.CodeTargets.Contains("src/NextGenEmby.Native/Media/PlaybackGraph.cpp") &&
            hint.CodeTargets.Contains("src/NextGenEmby.Core/PlaybackQuality/PlaybackRefreshRatePolicy.cs") &&
            hint.Signals.Contains("timing.maxFrameGapMs"));
    }

    [Fact]
    public void Analyze_Classifies_Unclassified_Failed_Checks_For_Older_Raw_Reports()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "legacy-missing-evidence",
            Result = "fail",
            Analysis = new PlaybackQualityAnalysis
            {
                PrimaryFailureArea = "color-pipeline"
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "DxgiInput",
            Status = "fail",
            FailureArea = "color-pipeline",
            Signal = "colorPipeline.dxgiInput",
            Expected = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
            Actual = "",
            Message = "DxgiInput is missing for color-pipeline validation."
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal(new[] { "insufficient instrumentation" }, analysis.FailureClasses);
        Assert.Contains(analysis.FailedChecks, check =>
            check.Name == "DxgiInput" &&
            check.FailureClass == "insufficient instrumentation");
    }

    [Fact]
    public void Analyze_Adds_Hdr_Strategy_Signals_For_Unsupported_Source_Hint()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "unsupported-dv",
            Result = "fail",
            Analysis = new PlaybackQualityAnalysis
            {
                PrimaryFailureArea = "unsupported-source"
            },
            Source = new PlaybackQualitySource
            {
                Codec = "hevc",
                FrameRate = 23.976,
                HdrKind = "DolbyVisionUnsupported",
                HdrPlaybackStrategy = "Unsupported Dolby Vision profile",
                IsHdr = true,
                IsDirectPlayable = false,
                IsDolbyVision = true,
                DolbyVisionProfile = 5,
                HasHdr10BaseLayer = false,
                HasHlgBaseLayer = false
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "HdrKind",
            Status = "fail",
            FailureArea = "unsupported-source",
            Signal = "source.hdrKind",
            Expected = "Hdr10",
            Actual = "DolbyVisionUnsupported"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Contains(analysis.InvestigationHints, hint =>
            hint.FailureArea == "unsupported-source" &&
            hint.Signals.Contains("source.hdrKind") &&
            hint.Signals.Contains("source.hdrPlaybackStrategy") &&
            hint.Signals.Contains("source.isHdr") &&
            hint.Signals.Contains("source.isDirectPlayable") &&
            hint.Signals.Contains("source.isDolbyVision") &&
            hint.Signals.Contains("source.dolbyVisionProfile") &&
            hint.Signals.Contains("source.dolbyVisionCompatibilityId") &&
            hint.Signals.Contains("source.hasHdr10BaseLayer") &&
            hint.Signals.Contains("source.hasHlgBaseLayer"));
    }

    [Fact]
    public void Analyze_Summarizes_Track_And_Subtitle_Evidence_For_Model()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "tracks-ready",
            Result = "pass",
            Tracks = new PlaybackQualityTracks
            {
                VideoTrackCount = 1,
                AudioTrackCount = 2,
                SubtitleTrackCount = 1,
                SelectedVideoStreamIndex = 0,
                SelectedAudioStreamIndex = 2,
                SelectedSubtitleStreamIndex = 7,
                IsSubtitleDisabled = false
            }
        };
        report.Tracks.Video.Add(new PlaybackQualityTrack
        {
            Index = 0,
            Codec = "hevc",
            Language = "und"
        });
        report.Tracks.Audio.Add(new PlaybackQualityTrack
        {
            Index = 2,
            Codec = "truehd",
            Language = "eng",
            ChannelLayout = "7.1"
        });
        report.Tracks.Subtitles.Add(new PlaybackQualityTrack
        {
            Index = 7,
            Codec = "srt",
            Language = "zho",
            IsExternal = true
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("ready", analysis.Tracks.Status);
        Assert.Equal(1, analysis.Tracks.VideoTrackCount);
        Assert.Equal(2, analysis.Tracks.AudioTrackCount);
        Assert.Equal(1, analysis.Tracks.SubtitleTrackCount);
        Assert.Equal(0, analysis.Tracks.SelectedVideoStreamIndex);
        Assert.Equal(2, analysis.Tracks.SelectedAudioStreamIndex);
        Assert.Equal(7, analysis.Tracks.SelectedSubtitleStreamIndex);
        Assert.False(analysis.Tracks.IsSubtitleDisabled);
        Assert.Contains("tracks.videoTrackCount", analysis.Tracks.Signals);
        Assert.Contains("tracks.audioTrackCount", analysis.Tracks.Signals);
        Assert.Contains("tracks.subtitleTrackCount", analysis.Tracks.Signals);
        Assert.Contains("tracks.selectedAudioStreamIndex", analysis.EvidenceSignals);
        Assert.Contains("tracks.selectedSubtitleStreamIndex", analysis.EvidenceSignals);
        Assert.Contains("tracks.subtitles.isExternal", analysis.EvidenceSignals);
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
    public void Analyze_Summarizes_Hdr_Source_Strategy_For_Model()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "source-summary",
            Result = "pass",
            Source = new PlaybackQualitySource
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 23.976,
                HdrKind = "DolbyVisionWithHdr10Fallback",
                HdrPlaybackStrategy = "HDR10 fallback from Dolby Vision",
                IsHdr = true,
                IsDirectPlayable = true,
                IsDolbyVision = true,
                DolbyVisionProfile = 8,
                DolbyVisionCompatibilityId = 1,
                HasHdr10BaseLayer = true,
                HasHlgBaseLayer = false
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240
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

        Assert.Equal("matched", analysis.Source.Status);
        Assert.Equal("DolbyVisionWithHdr10Fallback", analysis.Source.HdrKind);
        Assert.Equal("HDR10 fallback from Dolby Vision", analysis.Source.HdrPlaybackStrategy);
        Assert.True(analysis.Source.IsDirectPlayable);
        Assert.True(analysis.Source.IsDolbyVision);
        Assert.Equal(8, analysis.Source.DolbyVisionProfile);
        Assert.Equal(1, analysis.Source.DolbyVisionCompatibilityId);
        Assert.True(analysis.Source.HasHdr10BaseLayer);
        Assert.False(analysis.Source.HasHlgBaseLayer);
        Assert.Contains("source.hdrPlaybackStrategy", analysis.Source.Signals);
        Assert.Contains("source.dolbyVisionProfile", analysis.Source.Signals);
    }

    [Fact]
    public void Analyze_Marks_Source_Mismatch_From_Unsupported_Source_Checks()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "source-mismatch",
            Result = "fail",
            Source = new PlaybackQualitySource
            {
                Codec = "hevc",
                FrameRate = 23.976,
                HdrKind = "DolbyVisionUnsupported",
                HdrPlaybackStrategy = "Dolby Vision unsupported",
                IsHdr = true,
                IsDirectPlayable = false,
                IsDolbyVision = true,
                DolbyVisionProfile = 5
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "ExpectedDolbyVisionProfile",
            Status = "fail",
            FailureArea = "unsupported-source",
            Signal = "source.dolbyVisionProfile",
            Expected = "8",
            Actual = "5"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("mismatch", analysis.Source.Status);
        Assert.Contains("source.dolbyVisionProfile", analysis.Source.MismatchedSignals);
        Assert.Contains("source.dolbyVisionProfile", analysis.Source.Signals);
        Assert.Contains("Source metadata did not match expected reference metadata.", analysis.Source.Reason);
    }

    [Fact]
    public void Analyze_Summarizes_Color_Pipeline_For_Model()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "color-summary",
            Result = "pass",
            Source = new PlaybackQualitySource
            {
                Codec = "hevc",
                FrameRate = 23.976,
                HdrKind = "Hdr10"
            },
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240
            },
            ColorPipeline = new PlaybackQualityColorPipeline
            {
                ActualHdrOutput = "Hdr10",
                SwapChainFormat = "R10G10B10A2_UNORM",
                SwapChainColorSpace = "RGB_FULL_G2084_NONE_P2020",
                IsTenBitSwapChain = true,
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
                ConversionStatus = "validated",
                IsVideoProcessorColorSpaceValidated = true
            },
            Display = new PlaybackQualityDisplay
            {
                HdrStatus = "On",
                RefreshRateHz = 59.94006
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("matched", analysis.ColorPipeline.Status);
        Assert.Equal("Hdr10", analysis.ColorPipeline.ActualHdrOutput);
        Assert.Equal("YCBCR_STUDIO_G2084_TOPLEFT_P2020", analysis.ColorPipeline.DxgiInput);
        Assert.Equal("RGB_FULL_G2084_NONE_P2020", analysis.ColorPipeline.DxgiOutput);
        Assert.Equal("validated", analysis.ColorPipeline.ConversionStatus);
        Assert.True(analysis.ColorPipeline.IsTenBitSwapChain);
        Assert.True(analysis.ColorPipeline.IsVideoProcessorColorSpaceValidated);
        Assert.Contains("colorPipeline.actualHdrOutput", analysis.ColorPipeline.Signals);
        Assert.Contains("colorPipeline.swapChainFormat", analysis.ColorPipeline.Signals);
        Assert.Contains("colorPipeline.swapChainColorSpace", analysis.ColorPipeline.Signals);
        Assert.Contains("colorPipeline.isTenBitSwapChain", analysis.ColorPipeline.Signals);
        Assert.Contains("colorPipeline.dxgiInput", analysis.ColorPipeline.Signals);
        Assert.Contains("colorPipeline.conversionStatus", analysis.ColorPipeline.Signals);
    }

    [Fact]
    public void Analyze_Marks_Color_Mismatch_From_Color_Pipeline_Checks()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "color-mismatch",
            Result = "fail",
            ColorPipeline = new PlaybackQualityColorPipeline
            {
                ActualHdrOutput = "Sdr",
                DxgiInput = "YCBCR_STUDIO_G22_LEFT_P709",
                DxgiOutput = "RGB_FULL_G22_NONE_P709"
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

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("mismatch", analysis.ColorPipeline.Status);
        Assert.Contains("colorPipeline.actualHdrOutput", analysis.ColorPipeline.MismatchedSignals);
        Assert.Contains("colorPipeline.actualHdrOutput", analysis.ColorPipeline.Signals);
        Assert.Contains("Color pipeline observations did not match expected output.", analysis.ColorPipeline.Reason);
    }

    [Fact]
    public void Analyze_Summarizes_Buffering_For_Model()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "buffering-summary",
            Result = "pass",
            Timing = new PlaybackQualityTiming
            {
                RenderedVideoFrames = 240
            },
            Buffers = new PlaybackQualityBuffers
            {
                SubmittedAudioFrames = 2048,
                QueuedAudioBuffers = 4,
                VideoStarvedPasses = 0,
                AudioStarvedPasses = 0
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("stable", analysis.Buffering.Status);
        Assert.Equal(2048UL, analysis.Buffering.SubmittedAudioFrames);
        Assert.Equal(4UL, analysis.Buffering.QueuedAudioBuffers);
        Assert.Equal(0UL, analysis.Buffering.VideoStarvedPasses);
        Assert.Equal(0UL, analysis.Buffering.AudioStarvedPasses);
        Assert.Contains("buffers.submittedAudioFrames", analysis.Buffering.Signals);
        Assert.Contains("buffers.queuedAudioBuffers", analysis.Buffering.Signals);
        Assert.Contains("buffers.videoStarvedPasses", analysis.Buffering.Signals);
        Assert.Contains("buffers.audioStarvedPasses", analysis.Buffering.Signals);
    }

    [Fact]
    public void Analyze_Treats_Explicit_Zero_Starvation_Counters_As_Buffering_Evidence_When_Signal_Presence_Is_Captured()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "zero-starvation",
            Result = "observed",
            Buffers = new PlaybackQualityBuffers
            {
                VideoStarvedPasses = 0,
                AudioStarvedPasses = 0
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(
            report,
            new[]
            {
                "buffers.videoStarvedPasses",
                "buffers.audioStarvedPasses"
            });

        Assert.Equal("stable", analysis.Buffering.Status);
        Assert.Contains("buffers.videoStarvedPasses", analysis.Buffering.Signals);
        Assert.Contains("buffers.audioStarvedPasses", analysis.Buffering.Signals);
        Assert.DoesNotContain("buffers.queuedAudioBuffers", analysis.MissingEvidence);
    }

    [Fact]
    public void Analyze_Marks_Buffering_Starved_From_Buffering_Checks()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "buffering-starved",
            Result = "fail",
            Buffers = new PlaybackQualityBuffers
            {
                SubmittedAudioFrames = 512,
                QueuedAudioBuffers = 0,
                VideoStarvedPasses = 7,
                AudioStarvedPasses = 3
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "VideoStarvedPasses",
            Status = "fail",
            FailureArea = "buffering",
            Signal = "buffers.videoStarvedPasses",
            Expected = "0",
            Actual = "7"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("starved", analysis.Buffering.Status);
        Assert.Contains("buffers.videoStarvedPasses", analysis.Buffering.FailedSignals);
        Assert.Contains("buffers.videoStarvedPasses", analysis.Buffering.Signals);
        Assert.Contains("Playback supply starvation failed expected buffering thresholds.", analysis.Buffering.Reason);
    }

    [Fact]
    public void Analyze_Summarizes_AvSync_For_Model()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "av-sync-summary",
            Result = "pass",
            Sync = new PlaybackQualitySync
            {
                AudioClockTicks = 2_000_000,
                VideoPositionTicks = 2_010_000,
                AudioVideoDriftMsP50 = 4,
                AudioVideoDriftMsP95 = 16,
                AudioVideoDriftMsP99 = 24,
                AudioVideoDriftMsMax = 32
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("synced", analysis.AvSync.Status);
        Assert.Equal(16, analysis.AvSync.AudioVideoDriftMsP95);
        Assert.Equal(32, analysis.AvSync.AudioVideoDriftMsMax);
        Assert.Contains("sync.audioVideoDriftMsP50", analysis.AvSync.Signals);
        Assert.Contains("sync.audioVideoDriftMsP95", analysis.AvSync.Signals);
        Assert.Contains("sync.audioVideoDriftMsMax", analysis.AvSync.Signals);
    }

    [Theory]
    [InlineData(2_000_000, 2_010_000, 10_000, 1.0, "video-ahead")]
    [InlineData(2_010_000, 2_000_000, -10_000, -1.0, "audio-ahead")]
    [InlineData(2_000_000, 2_000_000, 0, 0.0, "aligned")]
    public void Analyze_Derives_AvSync_Clock_Delta_Direction_For_Model(
        long audioClockTicks,
        long videoPositionTicks,
        long expectedDeltaTicks,
        double expectedDeltaMs,
        string expectedDirection)
    {
        var report = new PlaybackQualityReport
        {
            RunId = "av-sync-direction",
            Result = "pass",
            Sync = new PlaybackQualitySync
            {
                AudioClockTicks = audioClockTicks,
                VideoPositionTicks = videoPositionTicks,
                AudioVideoDriftMsP95 = expectedDeltaMs < 0 ? -expectedDeltaMs : expectedDeltaMs
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal(expectedDeltaTicks, analysis.AvSync.ClockDeltaTicks);
        Assert.Equal(expectedDeltaMs, analysis.AvSync.ClockDeltaMs);
        Assert.Equal(expectedDirection, analysis.AvSync.DriftDirection);
        Assert.Contains("sync.clockDeltaMs", analysis.AvSync.Signals);
        Assert.Contains("sync.driftDirection", analysis.AvSync.Signals);
        Assert.Contains("sync.clockDeltaMs", analysis.EvidenceSignals);
        Assert.Contains("sync.driftDirection", analysis.EvidenceSignals);
    }

    [Fact]
    public void Analyze_Marks_AvSync_Drift_From_AvSync_Checks()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "av-sync-drift",
            Result = "fail",
            Sync = new PlaybackQualitySync
            {
                AudioVideoDriftMsP95 = 55,
                AudioVideoDriftMsMax = 80
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "AudioVideoDriftMsP95",
            Status = "fail",
            FailureArea = "av-sync",
            Signal = "sync.audioVideoDriftMsP95",
            Expected = "40.000",
            Actual = "55.000"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("drift", analysis.AvSync.Status);
        Assert.Contains("sync.audioVideoDriftMsP95", analysis.AvSync.FailedSignals);
        Assert.Contains("sync.audioVideoDriftMsP95", analysis.AvSync.Signals);
        Assert.Contains("A/V sync drift failed expected thresholds.", analysis.AvSync.Reason);
    }

    [Fact]
    public void Analyze_Summarizes_Startup_For_Model()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "startup-summary",
            Result = "pass",
            Startup = new PlaybackQualityStartup
            {
                CommandReceivedAt = "2026-07-07T10:00:00Z",
                PlaybackStartedAt = "2026-07-07T10:00:01Z",
                StartupDurationMs = 1000
            }
        };

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("ready", analysis.Startup.Status);
        Assert.Equal("2026-07-07T10:00:00Z", analysis.Startup.CommandReceivedAt);
        Assert.Equal("2026-07-07T10:00:01Z", analysis.Startup.PlaybackStartedAt);
        Assert.Equal(1000, analysis.Startup.StartupDurationMs);
        Assert.Contains("startup.commandReceivedAt", analysis.Startup.Signals);
        Assert.Contains("startup.playbackStartedAt", analysis.Startup.Signals);
        Assert.Contains("startup.startupDurationMs", analysis.Startup.Signals);
    }

    [Fact]
    public void Analyze_Marks_Startup_Slow_From_Startup_Checks()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "startup-slow",
            Result = "fail",
            Startup = new PlaybackQualityStartup
            {
                StartupDurationMs = 3500
            }
        };
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "StartupDurationMs",
            Status = "fail",
            FailureArea = "startup",
            Signal = "startup.startupDurationMs",
            Expected = "2000.000",
            Actual = "3500.000"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("slow", analysis.Startup.Status);
        Assert.Contains("startup.startupDurationMs", analysis.Startup.FailedSignals);
        Assert.Contains("startup.startupDurationMs", analysis.Startup.Signals);
        Assert.Contains("Startup timing failed expected thresholds.", analysis.Startup.Reason);
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
    public void Analyze_Targets_Highest_Priority_Failure_Area_When_Frame_Pacing_Also_Fails()
    {
        var report = CreateOptimizationReadyFailure();
        report.Analysis.PrimaryFailureArea = "color-pipeline";
        report.ColorPipeline.ActualHdrOutput = "Sdr";
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

        Assert.True(analysis.OptimizationGate.CanOptimizePlaybackCore);
        Assert.Equal("ready", analysis.OptimizationGate.Status);
        Assert.Contains("color-pipeline", analysis.OptimizationGate.TargetFailureAreas);
        Assert.DoesNotContain("frame-pacing", analysis.OptimizationGate.TargetFailureAreas);
        Assert.Contains("colorPipeline.actualHdrOutput", analysis.OptimizationGate.BlockerSignals);
    }

    [Fact]
    public void Analyze_Blocks_Core_Optimization_When_Source_Metadata_Mismatches()
    {
        var report = CreateOptimizationReadyFailure();
        report.Analysis.PrimaryFailureArea = "unsupported-source";
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "ExpectedHdrKind",
            Status = "fail",
            FailureArea = "unsupported-source",
            Signal = "source.hdrKind",
            Expected = "Hdr10",
            Actual = "DolbyVisionUnsupported"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.False(analysis.OptimizationGate.CanOptimizePlaybackCore);
        Assert.Equal("blocked", analysis.OptimizationGate.Status);
        Assert.Contains("source.mismatch", analysis.OptimizationGate.Blockers);
        Assert.Contains("source.hdrKind", analysis.OptimizationGate.BlockerSignals);
        Assert.DoesNotContain("frame-pacing", analysis.OptimizationGate.TargetFailureAreas);
    }

    [Fact]
    public void Analyze_Classifies_Frame_Pacing_As_Isolated_Gap_When_Only_Max_Frame_Gap_Fails()
    {
        var report = CreateOptimizationReadyFailure();
        report.Display.RefreshRateHz = 23.976024;

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("isolated-gap", analysis.FramePacing.Pattern);
        Assert.Contains("timing.maxFrameGapMs", analysis.FramePacing.Signals);
        Assert.Contains("Single max frame gap failed without sustained render interval failures.", analysis.FramePacing.Reasons);
    }

    [Fact]
    public void Analyze_Classifies_Frame_Pacing_As_Fractional_Cadence_When_Pulldown_Display_Is_Selected()
    {
        var report = CreateOptimizationReadyFailure();

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("fractional-cadence", analysis.FramePacing.Pattern);
        Assert.Contains("cadence.isFractionalCadence", analysis.FramePacing.Signals);
        Assert.Contains("display.refreshRateHz", analysis.FramePacing.Signals);
        Assert.Contains("Fractional display cadence coincided with frame pacing failure.", analysis.FramePacing.Reasons);
    }

    [Fact]
    public void Analyze_Reports_Normalized_Frame_Pacing_Severity_For_Model_Diagnosis()
    {
        var report = CreateOptimizationReadyFailure();
        var frameDurationMs = 1000.0 / 23.976;
        report.Timing.RenderedVideoFrames = 240;
        report.Timing.DroppedVideoFrames = 6;
        report.Timing.ExpectedFrameDurationMs = frameDurationMs;
        report.Timing.RenderIntervalMsP95 = frameDurationMs * 1.5;
        report.Timing.RenderIntervalMsP99 = frameDurationMs * 2.5;
        report.Timing.MaxFrameGapMs = frameDurationMs * 4.0;
        report.Timing.FramePacingSourceFrameRate = 23.976;
        report.Timing.LateFrameDropToleranceMs = frameDurationMs * 2.5;

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal(frameDurationMs, analysis.FramePacing.ExpectedFrameDurationMs, precision: 3);
        Assert.Equal(1.5, analysis.FramePacing.RenderIntervalP95FrameRatio, precision: 3);
        Assert.Equal(2.5, analysis.FramePacing.RenderIntervalP99FrameRatio, precision: 3);
        Assert.Equal(4.0, analysis.FramePacing.MaxFrameGapFrameRatio, precision: 3);
        Assert.Equal(2.439, analysis.FramePacing.DroppedVideoFramePercent, precision: 3);
        Assert.Equal(frameDurationMs * 2.5, analysis.FramePacing.LateFrameDropToleranceMs, precision: 3);
        Assert.Equal(2.5, analysis.FramePacing.LateFrameDropToleranceFrameRatio, precision: 3);
        Assert.Contains("timing.framePacingSourceFrameRate", analysis.EvidenceSignals);
        Assert.Contains("timing.lateFrameDropToleranceMs", analysis.EvidenceSignals);
    }

    [Fact]
    public void Analyze_Reports_Missing_Frame_Pacing_Policy_When_Frame_Pacing_Fails()
    {
        var report = CreateOptimizationReadyFailure();
        report.Timing.FramePacingSourceFrameRate = 0;
        report.Timing.LateFrameDropToleranceMs = 0;

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Contains("timing.framePacingSourceFrameRate", analysis.MissingEvidence);
        Assert.Contains("timing.lateFrameDropToleranceMs", analysis.MissingEvidence);
        Assert.False(analysis.OptimizationGate.CanOptimizePlaybackCore);
        Assert.Contains("missingEvidence", analysis.OptimizationGate.Blockers);
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
    public void Analyze_Reports_Cadence_Match_For_Cinema_Source_On_5994Hz_Display()
    {
        var report = CreateOptimizationReadyFailure();

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("matched", analysis.Cadence.Status);
        Assert.Equal(23.976, analysis.Cadence.SourceFrameRate, precision: 3);
        Assert.Equal(59.94006, analysis.Cadence.DisplayRefreshRateHz, precision: 5);
        Assert.Equal(2.5, analysis.Cadence.BestMultiplier, precision: 3);
        Assert.Equal(59.94, analysis.Cadence.BestTargetRefreshRateHz, precision: 2);
        Assert.InRange(analysis.Cadence.RefreshDeltaHz, 0, 0.01);
        Assert.Equal(0.15, analysis.Cadence.ToleranceHz, precision: 3);
        Assert.Contains("source.frameRate", analysis.Cadence.Signals);
        Assert.Contains("display.refreshRateHz", analysis.Cadence.Signals);
    }

    [Fact]
    public void Analyze_Flags_Fractional_Cadence_For_Cinema_Source_On_Pulldown_Display()
    {
        var report = CreateOptimizationReadyFailure();

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.True(analysis.Cadence.IsFractionalCadence);
        Assert.Contains("cadence.isFractionalCadence", analysis.Cadence.Signals);
        Assert.Contains("fractional", analysis.Cadence.Reason);
    }

    [Fact]
    public void Analyze_Reports_Cadence_Clock_Speed_Adjustment_When_Display_Is_Whole_Number_Cinema_Mode()
    {
        var report = CreateOptimizationReadyFailure();
        report.Display.RefreshRateHz = 60.0;

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("matched", analysis.Cadence.Status);
        Assert.True(analysis.Cadence.IsClockSpeedAdjustmentRequired);
        Assert.Equal(1.001, analysis.Cadence.ClockSpeedMultiplier, precision: 3);
        Assert.Equal(0.1001, analysis.Cadence.ClockSpeedAdjustmentPercent, precision: 3);
        Assert.Contains("cadence.clockSpeedAdjustmentPercent", analysis.Cadence.Signals);
    }

    [Fact]
    public void Analyze_Reports_Cadence_Mismatch_With_Nearest_Target()
    {
        var report = CreateOptimizationReadyFailure();
        report.Display.RefreshRateHz = 50.0;

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("mismatch", analysis.Cadence.Status);
        Assert.Equal(2.0, analysis.Cadence.BestMultiplier, precision: 3);
        Assert.Equal(47.952, analysis.Cadence.BestTargetRefreshRateHz, precision: 3);
        Assert.Equal(2.048, analysis.Cadence.RefreshDeltaHz, precision: 3);
        Assert.Contains("Display refresh rate is outside cadence tolerance.", analysis.Cadence.Reason);
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
        Assert.Contains("display.hdrStatus", analysis.MissingEvidence);
        Assert.Contains("colorPipeline.swapChainFormat", analysis.MissingEvidence);
        Assert.Contains("colorPipeline.swapChainColorSpace", analysis.MissingEvidence);
        Assert.Contains("colorPipeline.isTenBitSwapChain", analysis.MissingEvidence);
        Assert.Contains("colorPipeline.dxgiInput", analysis.MissingEvidence);
        Assert.Contains("colorPipeline.dxgiOutput", analysis.MissingEvidence);
        Assert.Contains("colorPipeline.conversionStatus", analysis.MissingEvidence);
    }

    [Fact]
    public void Analyze_Does_Not_Request_Color_Pipeline_Evidence_For_Expected_Unsupported_Source()
    {
        var report = new PlaybackQualityReport
        {
            RunId = "dv-profile5-unsupported",
            Result = "unsupported",
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
        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);

        Assert.Equal("unsupported", report.Result);
        Assert.Equal("unsupported", analysis.Result);
        Assert.Equal("unsupported", analysis.Source.Status);
        Assert.Contains("source.isDirectPlayable", analysis.EvidenceSignals);
        Assert.DoesNotContain("colorPipeline.dxgiInput", analysis.MissingEvidence);
        Assert.DoesNotContain("colorPipeline.dxgiOutput", analysis.MissingEvidence);
        Assert.DoesNotContain("colorPipeline.conversionStatus", analysis.MissingEvidence);
        Assert.DoesNotContain(analysis.InvestigationHints, hint =>
            hint.FailureArea == "evidence-collection" &&
            hint.Signals.Contains("colorPipeline.conversionStatus"));
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
            FailureClass = "player-core bug",
            Signal = "sync.audioVideoDriftMsP95"
        });

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report);
        var json = PlaybackQualityReportSerializer.Serialize(analysis);

        Assert.Contains("\"runId\"", json);
        Assert.Contains("\"primaryFailureArea\"", json);
        Assert.Contains("\"source\"", json);
        Assert.Contains("\"colorPipeline\"", json);
        Assert.Contains("\"buffering\"", json);
        Assert.Contains("\"avSync\"", json);
        Assert.Contains("\"startup\"", json);
        Assert.Contains("\"sample\"", json);
        Assert.Contains("\"cadence\"", json);
        Assert.Contains("\"optimizationGate\"", json);
        Assert.Contains("\"framePacing\"", json);
        Assert.Contains("\"triageSteps\"", json);
        Assert.Contains("\"failureAreas\"", json);
        Assert.Contains("\"failureClasses\"", json);
        Assert.Contains("\"failureClass\"", json);
        Assert.Contains("\"player-core bug\"", json);
        Assert.Contains("\"investigationHints\"", json);
        Assert.Contains("\"evidenceSignals\"", json);
        Assert.Contains("\"missingEvidence\"", json);
        Assert.Contains("\"sync.audioVideoDriftMsP95\"", json);
    }

    [Fact]
    public void Analyze_Only_Emits_Known_Model_Facing_Signals()
    {
        var analyses = new[]
        {
            PlaybackQualityReportAnalyzer.Analyze(CreateOptimizationReadyFailure()),
            PlaybackQualityReportAnalyzer.Analyze(new PlaybackQualityReport
            {
                RunId = "missing-evidence",
                Result = "fail"
            }),
            PlaybackQualityReportAnalyzer.Analyze(new PlaybackQualityReport
            {
                RunId = "startup-evidence",
                Result = "pass",
                Startup = new PlaybackQualityStartup
                {
                    CommandReceivedAt = "2026-07-07T01:00:00Z",
                    PlaybackStartedAt = "2026-07-07T01:00:01Z",
                    StartupDurationMs = 1000
                }
            }),
            PlaybackQualityReportAnalyzer.Analyze(new PlaybackQualityReport
            {
                RunId = "sync-evidence",
                Result = "pass",
                Sync = new PlaybackQualitySync
                {
                    AudioClockTicks = 100,
                    VideoPositionTicks = 90,
                    AudioVideoDriftMsP50 = 4,
                    AudioVideoDriftMsP95 = 8,
                    AudioVideoDriftMsP99 = 12,
                    AudioVideoDriftMsMax = 18
                }
            })
        };
        var knownSignals = new HashSet<string>(
            PlaybackQualitySignalCatalog.KnownSignals.Select(signal => signal.Signal));
        var emittedSignals = new HashSet<string>();
        foreach (var analysis in analyses)
        {
            AddAnalysisSignals(emittedSignals, analysis);
        }

        Assert.All(emittedSignals, signal => Assert.Contains(signal, knownSignals));
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
                ExpectedFrameDurationMs = 1000.0 / 23.976,
                FramePacingSourceFrameRate = 23.976,
                LateFrameDropToleranceMs = (1000.0 / 23.976) * 2.5
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

    private static void AddAnalysisSignals(
        HashSet<string> signals,
        PlaybackQualityModelAnalysis analysis)
    {
        AddSignals(signals, analysis.EvidenceSignals);
        AddSignals(signals, analysis.MissingEvidence);
        AddSignals(signals, analysis.Startup.Signals);
        AddSignals(signals, analysis.Startup.FailedSignals);
        AddSignals(signals, analysis.Source.Signals);
        AddSignals(signals, analysis.Source.MismatchedSignals);
        AddSignals(signals, analysis.ColorPipeline.Signals);
        AddSignals(signals, analysis.ColorPipeline.MismatchedSignals);
        AddSignals(signals, analysis.Buffering.Signals);
        AddSignals(signals, analysis.Buffering.FailedSignals);
        AddSignals(signals, analysis.AvSync.Signals);
        AddSignals(signals, analysis.AvSync.FailedSignals);
        AddSignals(signals, analysis.Cadence.Signals);
        AddSignals(signals, analysis.OptimizationGate.BlockerSignals);
        AddSignals(signals, analysis.FramePacing.Signals);
        foreach (var hint in analysis.InvestigationHints)
        {
            AddSignals(signals, hint.Signals);
        }

        foreach (var step in analysis.TriageSteps)
        {
            AddSignals(signals, step.Signals);
        }
    }

    private static void AddSignals(
        HashSet<string> target,
        IEnumerable<string> signals)
    {
        foreach (var signal in signals)
        {
            target.Add(signal);
        }
    }
}
