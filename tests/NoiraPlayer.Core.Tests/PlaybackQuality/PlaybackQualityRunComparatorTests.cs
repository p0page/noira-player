using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityRunComparatorTests
{
    [Fact]
    public void Compare_Reports_Improved_When_Native_Execution_Recovers_From_Error_To_Pass()
    {
        var baseline = CreateReport(
            "baseline",
            Check("SourceCodec", "observed", "media-load", "source.codec", "h264", "h264"));
        baseline.Result = PlaybackQualityReportResult.Error;
        baseline.Execution.Status = PlaybackQualityExecutionStatus.Failed;
        baseline.Execution.PlaybackSampleObserved = false;
        baseline.Error.Code = "native-headless.helper-failed";

        var candidate = CreateReport(
            "candidate",
            Check("SourceCodec", "observed", "media-load", "source.codec", "h264", "h264"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("improved", comparison.Result);
        Assert.Equal("keep-candidate", comparison.Decision);
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "execution.outcome" &&
            delta.BaselineActual == "error/failed" &&
            delta.CandidateActual == "pass/completed");
    }

    [Fact]
    public void Compare_Reports_Regressed_When_Native_Execution_Falls_From_Pass_To_Error()
    {
        var baseline = CreateReport(
            "baseline",
            Check("SourceCodec", "observed", "media-load", "source.codec", "h264", "h264"));
        var candidate = CreateReport(
            "candidate",
            Check("SourceCodec", "observed", "media-load", "source.codec", "h264", "h264"));
        candidate.Result = PlaybackQualityReportResult.Error;
        candidate.Execution.Status = PlaybackQualityExecutionStatus.Failed;
        candidate.Execution.PlaybackSampleObserved = false;
        candidate.Error.Code = "native-headless.helper-failed";

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("regressed", comparison.Result);
        Assert.Equal("reject-candidate", comparison.Decision);
        Assert.Contains(comparison.Regressions, delta =>
            delta.Signal == "execution.outcome" &&
            delta.BaselineActual == "pass/completed" &&
            delta.CandidateActual == "error/failed");
    }

    [Fact]
    public void Compare_Reports_Does_Not_Rank_Unsupported_Against_Fail()
    {
        var baseline = CreateReport(
            "baseline",
            Check("SourceCodec", "observed", "media-load", "source.codec", "hevc", "hevc"));
        baseline.Result = PlaybackQualityReportResult.Unsupported;
        baseline.Execution.Status = PlaybackQualityExecutionStatus.Unsupported;

        var candidate = CreateReport(
            "candidate",
            Check("SourceCodec", "observed", "media-load", "source.codec", "hevc", "hevc"));
        candidate.Result = PlaybackQualityReportResult.Fail;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.DoesNotContain(comparison.Improvements, delta => delta.Signal == "execution.outcome");
        Assert.DoesNotContain(comparison.Regressions, delta => delta.Signal == "execution.outcome");
    }

    [Fact]
    public void Compare_Rejects_Different_Color_Expectation_Profiles()
    {
        var baseline = CreateReport("baseline", Check(
            "ColorExpectationProfile",
            "observed",
            "color-pipeline",
            "colorPipeline.expectationProfile",
            "environment-selected",
            "primary"));
        var candidate = CreateReport("candidate", Check(
            "ColorExpectationProfile",
            "observed",
            "color-pipeline",
            "colorPipeline.expectationProfile",
            "environment-selected",
            "sdr-display-fallback"));
        baseline.ColorPipeline.ExpectationProfile = "primary";
        candidate.ColorPipeline.ExpectationProfile = "sdr-display-fallback";

        var result = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("incompatible", result.Comparability.Status);
        Assert.Contains("colorPipeline.expectationProfile", result.Comparability.Signals);
        Assert.Equal("insufficient-evidence", result.Result);
    }

    [Fact]
    public void Compare_Reports_Improved_When_Failed_Numeric_Signal_Decreases()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal(1, comparison.SchemaVersion);
        Assert.Equal(PlaybackQualityRunResult.CurrentEvaluationVersion, comparison.EvaluationVersion);
        Assert.Equal("baseline", comparison.BaselineRunId);
        Assert.Equal("candidate", comparison.CandidateRunId);
        Assert.Equal("improved", comparison.Result);
        Assert.Equal("keep-candidate", comparison.Decision);
        Assert.Contains("Keep candidate playback Core change", comparison.SuggestedNextAction);
        Assert.Equal("accept-candidate", comparison.Optimization.Action);
        Assert.Equal("low", comparison.Optimization.Risk);
        Assert.Contains("strong comparison evidence supports candidate", comparison.Optimization.Reasons);
        Assert.Empty(comparison.Regressions);
        Assert.Contains("frame-pacing", comparison.PersistingFailureAreas);
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "timing.maxFrameGapMs" &&
            delta.FailureArea == "frame-pacing" &&
            delta.BaselineActual == "180.000" &&
            delta.CandidateActual == "120.000" &&
            delta.Direction == "decreased");
    }

    [Fact]
    public void Compare_Reports_StrongConfidence_When_All_Checks_Match()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("strong", comparison.Confidence.Level);
        Assert.Contains("all comparison checks matched", comparison.Confidence.Reasons);
        Assert.Empty(comparison.Confidence.Signals);
    }

    [Fact]
    public void Compare_Surfaces_Build_Identity_For_Model()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        baseline.Environment.PlayerCoreVersion = "native-core-base";
        baseline.Environment.SourceRevision = "base123";
        baseline.Environment.BuildConfiguration = "Debug";

        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Environment.PlayerCoreVersion = "native-core-candidate";
        candidate.Environment.SourceRevision = "candidate456";
        candidate.Environment.BuildConfiguration = "Debug";

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("different-build", comparison.Environment.Status);
        Assert.Equal("native-core-base", comparison.Environment.BaselinePlayerCoreVersion);
        Assert.Equal("native-core-candidate", comparison.Environment.CandidatePlayerCoreVersion);
        Assert.Equal("base123", comparison.Environment.BaselineSourceRevision);
        Assert.Equal("candidate456", comparison.Environment.CandidateSourceRevision);
        Assert.Equal("Debug", comparison.Environment.BaselineBuildConfiguration);
        Assert.Equal("Debug", comparison.Environment.CandidateBuildConfiguration);
        Assert.Contains("environment.playerCoreVersion", comparison.Environment.Signals);
        Assert.Contains("environment.sourceRevision", comparison.Environment.Signals);
        Assert.Contains("environment.buildConfiguration", comparison.Environment.Signals);
    }

    [Fact]
    public void Compare_Blocks_Candidate_Acceptance_When_Build_Identity_Is_Unchanged()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        baseline.Environment.PlayerCoreVersion = "native-core-v42";
        baseline.Environment.SourceRevision = "same123";

        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Environment.PlayerCoreVersion = "native-core-v42";
        candidate.Environment.SourceRevision = "same123";

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("same-build", comparison.Environment.Status);
        Assert.Equal("weak", comparison.Confidence.Level);
        Assert.Contains("candidate build identity matches baseline", comparison.Confidence.Reasons);
        Assert.Contains("environment.sourceRevision", comparison.Confidence.Signals);
        Assert.Equal("collect-comparable-evidence", comparison.Decision);
        Assert.Equal("collect-comparable-evidence", comparison.Optimization.Action);
        Assert.Equal("high", comparison.Optimization.Risk);
        Assert.Contains("comparison.environment-same-build", comparison.Optimization.Blockers);
        Assert.Contains("candidate build identity matches baseline", comparison.Optimization.Blockers);
        Assert.Contains("environment.sourceRevision", comparison.Optimization.Signals);
    }

    [Fact]
    public void Compare_Blocks_Candidate_Acceptance_When_Build_Identity_Is_Missing()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        baseline.Environment = new PlaybackQualityEnvironment();
        candidate.Environment = new PlaybackQualityEnvironment();

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("missing-evidence", comparison.Environment.Status);
        Assert.Equal("weak", comparison.Confidence.Level);
        Assert.Contains("comparison is missing baseline and candidate build identity", comparison.Confidence.Reasons);
        Assert.Contains("environment.identity", comparison.Confidence.Signals);
        Assert.Equal("collect-comparable-evidence", comparison.Decision);
        Assert.Equal("collect-comparable-evidence", comparison.Optimization.Action);
        Assert.Equal("high", comparison.Optimization.Risk);
        Assert.Contains("comparison.environment-evidence-missing", comparison.Optimization.Blockers);
        Assert.Contains("comparison is missing baseline and candidate build identity", comparison.Optimization.Blockers);
        Assert.Contains("environment.identity", comparison.Optimization.Signals);
    }

    [Fact]
    public void Compare_Blocks_Candidate_Acceptance_When_Build_Identity_Is_Partial()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Environment = new PlaybackQualityEnvironment();

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("partial", comparison.Environment.Status);
        Assert.Equal("weak", comparison.Confidence.Level);
        Assert.Contains("comparison is missing complete baseline and candidate build identity", comparison.Confidence.Reasons);
        Assert.Contains("environment.identity", comparison.Confidence.Signals);
        Assert.Equal("collect-comparable-evidence", comparison.Decision);
        Assert.Equal("collect-comparable-evidence", comparison.Optimization.Action);
        Assert.Equal("high", comparison.Optimization.Risk);
        Assert.Contains("comparison.environment-evidence-missing", comparison.Optimization.Blockers);
        Assert.Contains("comparison is missing complete baseline and candidate build identity", comparison.Optimization.Blockers);
        Assert.Contains("environment.identity", comparison.Optimization.Signals);
        Assert.Contains("environment.sourceRevision", comparison.Optimization.Signals);
    }

    [Fact]
    public void Compare_Reports_Regressed_When_Passing_Signal_Starts_Failing()
    {
        var baseline = CreateReport(
            "baseline",
            Check("AudioVideoDriftMsP95", "pass", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "25.000"));
        var candidate = CreateReport(
            "candidate",
            Check("AudioVideoDriftMsP95", "fail", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "55.000"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("regressed", comparison.Result);
        Assert.Equal("reject-candidate", comparison.Decision);
        Assert.Contains("Reject or revert candidate playback Core change", comparison.SuggestedNextAction);
        Assert.Empty(comparison.Improvements);
        Assert.Contains("av-sync", comparison.NewFailureAreas);
        Assert.Contains(comparison.Regressions, delta =>
            delta.Signal == "sync.audioVideoDriftMsP95" &&
            delta.BaselineStatus == "pass" &&
            delta.CandidateStatus == "fail");
    }

    [Fact]
    public void Compare_Reports_Improved_When_Failed_Minimum_Signal_Increases()
    {
        var baseline = CreateReport(
            "baseline",
            Check("RenderedVideoFrames", "fail", "frame-pacing", "timing.renderedVideoFrames", "120", "24"));
        var candidate = CreateReport(
            "candidate",
            Check("RenderedVideoFrames", "fail", "frame-pacing", "timing.renderedVideoFrames", "120", "90"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("improved", comparison.Result);
        Assert.Empty(comparison.Regressions);
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "timing.renderedVideoFrames" &&
            delta.Direction == "increased" &&
            delta.NumericDelta == 66);
    }

    [Fact]
    public void Compare_Reports_Normalized_Frame_Pacing_Severity_Deltas()
    {
        var frameDurationMs = 1000.0 / 23.976;
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        baseline.Timing.RenderedVideoFrames = 240;
        baseline.Timing.DroppedVideoFrames = 6;
        baseline.Timing.ExpectedFrameDurationMs = frameDurationMs;
        baseline.Timing.MaxFrameGapMs = frameDurationMs * 4.0;

        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Timing.RenderedVideoFrames = 240;
        candidate.Timing.DroppedVideoFrames = 2;
        candidate.Timing.ExpectedFrameDurationMs = frameDurationMs;
        candidate.Timing.MaxFrameGapMs = frameDurationMs * 2.5;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Contains("framePacing.maxFrameGapFrameRatio", comparison.Coverage.MatchedSignals);
        Assert.Contains("framePacing.droppedVideoFramePercent", comparison.Coverage.MatchedSignals);
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "framePacing.maxFrameGapFrameRatio" &&
            delta.Direction == "decreased" &&
            delta.FailureArea == "frame-pacing" &&
            delta.NumericDelta < -1.49 &&
            delta.NumericDelta > -1.51);
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "framePacing.droppedVideoFramePercent" &&
            delta.Direction == "decreased" &&
            delta.FailureArea == "frame-pacing");
    }

    [Fact]
    public void Compare_Reports_Does_Not_Treat_Missing_Frame_Counts_As_Dropped_Frame_Percent_Evidence()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.DoesNotContain("framePacing.droppedVideoFramePercent", comparison.Coverage.MatchedSignals);
    }

    [Fact]
    public void Compare_Reports_Frame_Pacing_Policy_Changes_Without_Classifying_Them_As_Quality_Deltas()
    {
        var frameDurationMs = 1000.0 / 60.0;
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "50.000", "80.000"));
        baseline.Timing.ExpectedFrameDurationMs = frameDurationMs;
        baseline.Timing.LateFrameDropToleranceMs = frameDurationMs * 6.0;

        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "50.000", "80.000"));
        candidate.Timing.ExpectedFrameDurationMs = frameDurationMs;
        candidate.Timing.LateFrameDropToleranceMs = frameDurationMs * 2.5;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Contains("framePacing.lateFrameDropToleranceFrameRatio", comparison.Coverage.MatchedSignals);
        Assert.Contains(comparison.PolicyChanges, delta =>
            delta.Signal == "framePacing.lateFrameDropToleranceFrameRatio" &&
            delta.Direction == "decreased" &&
            delta.FailureArea == "frame-pacing" &&
            delta.NumericDelta < -3.49 &&
            delta.NumericDelta > -3.51);
        Assert.DoesNotContain(comparison.Improvements, delta =>
            delta.Signal == "framePacing.lateFrameDropToleranceFrameRatio");
        Assert.DoesNotContain(comparison.Regressions, delta =>
            delta.Signal == "framePacing.lateFrameDropToleranceFrameRatio");
    }

    [Fact]
    public void Compare_Reports_Mixed_When_One_Signal_Improves_And_Another_Regresses()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"),
            Check("ActualHdrOutput", "pass", "color-pipeline", "colorPipeline.actualHdrOutput", "Hdr10", "Hdr10"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"),
            Check("ActualHdrOutput", "fail", "color-pipeline", "colorPipeline.actualHdrOutput", "Hdr10", "Sdr"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("mixed", comparison.Result);
        Assert.Equal("split-candidate", comparison.Decision);
        Assert.Contains("Split candidate change or isolate regressions", comparison.SuggestedNextAction);
        Assert.Contains(comparison.Improvements, delta => delta.Signal == "timing.maxFrameGapMs");
        Assert.Contains(comparison.Regressions, delta => delta.Signal == "colorPipeline.actualHdrOutput");
        Assert.Contains("frame-pacing", comparison.PersistingFailureAreas);
        Assert.Contains("color-pipeline", comparison.NewFailureAreas);
    }

    [Fact]
    public void Compare_Reports_Coverage_For_Matched_And_Unmatched_Checks()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"),
            Check("AudioVideoDriftMsP95", "pass", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "25.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"),
            Check("ActualHdrOutput", "pass", "color-pipeline", "colorPipeline.actualHdrOutput", "Hdr10", "Hdr10"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal(2, comparison.Coverage.BaselineCheckCount);
        Assert.Equal(2, comparison.Coverage.CandidateCheckCount);
        Assert.Equal(1, comparison.Coverage.MatchedCheckCount);
        Assert.Contains("timing.maxFrameGapMs", comparison.Coverage.MatchedSignals);
        Assert.Contains("sync.audioVideoDriftMsP95", comparison.Coverage.UnmatchedBaselineSignals);
        Assert.Contains("colorPipeline.actualHdrOutput", comparison.Coverage.UnmatchedCandidateSignals);
    }

    [Fact]
    public void Compare_Reports_Includes_Track_And_Subtitle_Evidence_Signals()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "pass", "frame-pacing", "timing.maxFrameGapMs", "105.000", "80.000"));
        AddTrackEvidence(baseline);

        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "pass", "frame-pacing", "timing.maxFrameGapMs", "105.000", "80.000"));
        AddTrackEvidence(candidate);

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Contains("tracks.videoTrackCount", comparison.Coverage.MatchedSignals);
        Assert.Contains("tracks.audioTrackCount", comparison.Coverage.MatchedSignals);
        Assert.Contains("tracks.subtitleTrackCount", comparison.Coverage.MatchedSignals);
        Assert.Contains("tracks.selectedAudioStreamIndex", comparison.Coverage.MatchedSignals);
        Assert.Contains("tracks.isSubtitleDisabled", comparison.Coverage.MatchedSignals);
        Assert.Contains("tracks.audio.codec", comparison.Coverage.MatchedSignals);
        Assert.Contains("tracks.audio.channels", comparison.Coverage.MatchedSignals);
        Assert.Contains("tracks.subtitles.codec", comparison.Coverage.MatchedSignals);
        Assert.Contains("tracks.subtitles.language", comparison.Coverage.MatchedSignals);
        Assert.Empty(comparison.Regressions);
    }

    [Fact]
    public void Compare_Reports_Improves_When_Seek_Position_Error_Decreases()
    {
        var baseline = CreateReport(
            "baseline",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        baseline.Position.SeekTargetPositionTicks = 0;
        baseline.Position.ActualPositionTicks = 15000000;
        baseline.Position.SeekPositionErrorMs = 1500;

        var candidate = CreateReport(
            "candidate",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        candidate.Position.SeekTargetPositionTicks = 0;
        candidate.Position.ActualPositionTicks = 0;
        candidate.Position.SeekPositionErrorMs = 0;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("improved", comparison.Result);
        Assert.Contains("position.seekPositionErrorMs", comparison.Coverage.MatchedSignals);
        Assert.Contains("timeline", comparison.Optimization.FailureAreas);
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "position.seekPositionErrorMs" &&
            delta.FailureArea == "timeline" &&
            delta.Direction == "decreased" &&
            delta.BaselineActual == "1500.000" &&
            delta.CandidateActual == "0.000");
    }

    [Fact]
    public void Compare_Reports_Includes_Timing_Buffering_And_Sync_Evidence_Signals()
    {
        var baseline = CreateReport(
            "baseline",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        AddRuntimePlaybackEvidence(baseline);

        var candidate = CreateReport(
            "candidate",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        AddRuntimePlaybackEvidence(candidate);

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Contains("timing.renderIntervalMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.renderIntervalMsP99", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.presentDurationMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.presentDurationMsMax", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitDurationMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitDurationMsMax", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitTargetMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitTargetMsMax", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitOversleepMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitOversleepMsMax", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitFinalDeltaAbsMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitFinalDeltaAbsMsMax", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitPassDurationMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitPassDurationMsMax", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitPassTargetMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitPassTargetMsMax", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitPassOversleepMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitPassOversleepMsMax", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.renderIntervalAfterAudioAheadWaitSampleCount", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.renderIntervalAfterAudioAheadWaitMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.renderIntervalAfterAudioAheadWaitMsMax", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitEndToPresentSampleCount", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitEndToPresentMsP50", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitEndToPresentMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitEndToPresentMsP99", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitEndToPresentMsMax", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.renderIntervalAfterNonAudioWaitSampleCount", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.renderIntervalAfterNonAudioWaitMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.renderIntervalAfterNonAudioWaitMsMax", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.maxFrameGapMs", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.videoAheadWaitCount", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitCount", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.videoClockWaitCount", comparison.Coverage.MatchedSignals);
        Assert.Contains("sync.audioClockTicks", comparison.Coverage.MatchedSignals);
        Assert.Contains("sync.videoPositionTicks", comparison.Coverage.MatchedSignals);
        Assert.Contains("sync.audioVideoDriftMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("buffers.submittedAudioFrames", comparison.Coverage.MatchedSignals);
        Assert.Contains("buffers.queuedAudioBuffers", comparison.Coverage.MatchedSignals);
        Assert.Contains("buffers.videoStarvedPasses", comparison.Coverage.MatchedSignals);
        Assert.Contains("buffers.audioStarvedPasses", comparison.Coverage.MatchedSignals);
        Assert.Contains("runtimeMetrics.processWallClockMs", comparison.Coverage.MatchedSignals);
        Assert.Contains("runtimeMetrics.processCpuTimeMs", comparison.Coverage.MatchedSignals);
        Assert.Contains("runtimeMetrics.processCpuUtilizationRatio", comparison.Coverage.MatchedSignals);
        Assert.Empty(comparison.Regressions);
    }

    [Fact]
    public void Compare_Reports_Improves_When_Runtime_Frame_Pacing_Approaches_Expected_Duration()
    {
        var baseline = CreateReport(
            "baseline",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        baseline.Timing.ExpectedFrameDurationMs = 41.708;
        baseline.Timing.RenderIntervalMsP95 = 48.098;
        baseline.Timing.RenderIntervalMsP99 = 48.111;
        baseline.Timing.MaxFrameGapMs = 48.111;

        var candidate = CreateReport(
            "candidate",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        candidate.Timing.ExpectedFrameDurationMs = 41.708;
        candidate.Timing.RenderIntervalMsP95 = 42.098;
        candidate.Timing.RenderIntervalMsP99 = 42.101;
        candidate.Timing.MaxFrameGapMs = 42.101;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("improved", comparison.Result);
        Assert.Equal("keep-candidate", comparison.Decision);
        Assert.Contains("frame-pacing", comparison.Optimization.FailureAreas);
        Assert.Contains("timing.expectedFrameDurationMs", comparison.Coverage.MatchedSignals);
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "framePacing.renderIntervalP95ExpectedErrorMs" &&
            delta.FailureArea == "frame-pacing" &&
            delta.Direction == "decreased" &&
            delta.BaselineActual == "6.39" &&
            delta.CandidateActual == "0.39");
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "framePacing.renderIntervalP99ExpectedErrorMs" &&
            delta.Direction == "decreased");
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "framePacing.maxFrameGapExpectedErrorMs" &&
            delta.Direction == "decreased");
        Assert.Empty(comparison.Regressions);
    }

    [Fact]
    public void Compare_Reports_Regresses_When_Audio_Ahead_Oversleep_Increases_Materially()
    {
        var baseline = CreateReport(
            "baseline",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        baseline.Timing.AudioAheadWaitOversleepMsP95 = 7.9336;
        baseline.Timing.AudioAheadWaitOversleepMsP99 = 10.787;
        baseline.Timing.AudioAheadWaitFinalDeltaAbsMsP95 = 10.0;
        baseline.Timing.AudioAheadWaitFinalDeltaAbsMsP99 = 10.0;

        var candidate = CreateReport(
            "candidate",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        candidate.Timing.AudioAheadWaitOversleepMsP95 = 10.07;
        candidate.Timing.AudioAheadWaitOversleepMsP99 = 16.73;
        candidate.Timing.AudioAheadWaitFinalDeltaAbsMsP95 = 13.0;
        candidate.Timing.AudioAheadWaitFinalDeltaAbsMsP99 = 16.0;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("regressed", comparison.Result);
        Assert.Equal("reject-candidate", comparison.Decision);
        Assert.Contains("frame-pacing", comparison.NewFailureAreas);
        Assert.Contains(comparison.Regressions, delta =>
            delta.Signal == "timing.audioAheadWaitOversleepMsP95" &&
            delta.FailureArea == "frame-pacing" &&
            delta.Direction == "increased" &&
            delta.BaselineActual == "7.9336" &&
            delta.CandidateActual == "10.07");
        Assert.Contains(comparison.Regressions, delta =>
            delta.Signal == "timing.audioAheadWaitOversleepMsP99" &&
            delta.Direction == "increased");
        Assert.Empty(comparison.Improvements);
    }

    [Fact]
    public void Compare_Does_Not_Reject_When_Audio_Ahead_Oversleep_Increases_But_Final_Delta_Is_Stable()
    {
        var baseline = CreateReport(
            "baseline",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        baseline.Timing.AudioAheadWaitOversleepMsP95 = 7.9336;
        baseline.Timing.AudioAheadWaitOversleepMsP99 = 10.787;
        baseline.Timing.AudioAheadWaitFinalDeltaAbsMsP95 = 10.0;
        baseline.Timing.AudioAheadWaitFinalDeltaAbsMsP99 = 10.0;

        var candidate = CreateReport(
            "candidate",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        candidate.Timing.AudioAheadWaitOversleepMsP95 = 10.07;
        candidate.Timing.AudioAheadWaitOversleepMsP99 = 16.73;
        candidate.Timing.AudioAheadWaitFinalDeltaAbsMsP95 = 10.0;
        candidate.Timing.AudioAheadWaitFinalDeltaAbsMsP99 = 10.0;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("unchanged", comparison.Result);
        Assert.Equal("no-change", comparison.Decision);
        Assert.Contains("timing.audioAheadWaitOversleepMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitFinalDeltaAbsMsP95", comparison.Coverage.MatchedSignals);
        Assert.Empty(comparison.Regressions);
        Assert.Empty(comparison.Improvements);
    }

    [Fact]
    public void Compare_Does_Not_Reject_When_Only_Audio_Ahead_Oversleep_P99_Increases()
    {
        var baseline = CreateReport(
            "baseline",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        baseline.Timing.AudioAheadWaitOversleepMsP95 = 7.5337;
        baseline.Timing.AudioAheadWaitOversleepMsP99 = 7.7653;

        var candidate = CreateReport(
            "candidate",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        candidate.Timing.AudioAheadWaitOversleepMsP95 = 7.9336;
        candidate.Timing.AudioAheadWaitOversleepMsP99 = 10.787;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("unchanged", comparison.Result);
        Assert.Equal("no-change", comparison.Decision);
        Assert.Contains("timing.audioAheadWaitOversleepMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains("timing.audioAheadWaitOversleepMsP99", comparison.Coverage.MatchedSignals);
        Assert.Empty(comparison.Regressions);
    }

    [Fact]
    public void Compare_Does_Not_Compare_Audio_Ahead_Oversleep_Across_Semantics()
    {
        var baseline = CreateReport(
            "baseline",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        baseline.Timing.AudioAheadWaitOversleepSemantics = "episode-wall-minus-first-target-v1";
        baseline.Timing.AudioAheadWaitTargetMsP95 = 10.0;
        baseline.Timing.AudioAheadWaitTargetMsP99 = 10.0;
        baseline.Timing.AudioAheadWaitOversleepMsP95 = 12.0;
        baseline.Timing.AudioAheadWaitOversleepMsP99 = 15.0;
        baseline.Timing.AudioAheadWaitFinalDeltaAbsMsP95 = 10.0;
        baseline.Timing.AudioAheadWaitFinalDeltaAbsMsP99 = 10.0;

        var candidate = CreateReport(
            "candidate",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        candidate.Timing.AudioAheadWaitOversleepSemantics = "sum-positive-pass-oversleep-v2";
        candidate.Timing.AudioAheadWaitTargetMsP95 = 25.0;
        candidate.Timing.AudioAheadWaitTargetMsP99 = 30.0;
        candidate.Timing.AudioAheadWaitOversleepMsP95 = 3.0;
        candidate.Timing.AudioAheadWaitOversleepMsP99 = 5.0;
        candidate.Timing.AudioAheadWaitFinalDeltaAbsMsP95 = 7.0;
        candidate.Timing.AudioAheadWaitFinalDeltaAbsMsP99 = 7.0;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("unchanged", comparison.Result);
        Assert.Equal("no-change", comparison.Decision);
        Assert.Equal("partial", comparison.Confidence.Level);
        Assert.DoesNotContain(comparison.Improvements, delta =>
            delta.Signal.StartsWith("timing.audioAheadWaitOversleepMs"));
        Assert.DoesNotContain(comparison.Regressions, delta =>
            delta.Signal.StartsWith("timing.audioAheadWaitOversleepMs"));
        Assert.DoesNotContain("timing.audioAheadWaitTargetMsP95", comparison.Coverage.MatchedSignals);
        Assert.DoesNotContain("timing.audioAheadWaitOversleepMsP95", comparison.Coverage.MatchedSignals);
        Assert.Contains(
            "timing.audioAheadWaitTargetMs@episode-wall-minus-first-target-v1",
            comparison.Coverage.UnmatchedBaselineSignals);
        Assert.Contains(
            "timing.audioAheadWaitTargetMs@sum-positive-pass-oversleep-v2",
            comparison.Coverage.UnmatchedCandidateSignals);
        Assert.Contains(
            "timing.audioAheadWaitOversleepMs@episode-wall-minus-first-target-v1",
            comparison.Coverage.UnmatchedBaselineSignals);
        Assert.Contains(
            "timing.audioAheadWaitOversleepMs@sum-positive-pass-oversleep-v2",
            comparison.Coverage.UnmatchedCandidateSignals);
        Assert.Contains(comparison.Limitations, limitation =>
            limitation.Contains("audio-ahead episode metric semantics differ"));
    }

    [Fact]
    public void Compare_Ignores_Oversleep_Semantics_When_Neither_Report_Has_Oversleep_Evidence()
    {
        var baseline = CreateReport(
            "baseline",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        baseline.Timing.AudioAheadWaitOversleepSemantics = "episode-wall-minus-first-target-v1";
        baseline.Timing.RenderedVideoFrames = 46;

        var candidate = CreateReport(
            "candidate",
            Check("RenderedVideoFrames", "pass", "frame-pacing", "timing.renderedVideoFrames", "1", "46"));
        candidate.Timing.AudioAheadWaitOversleepSemantics = "sum-positive-pass-oversleep-v2";
        candidate.Timing.RenderedVideoFrames = 46;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("strong", comparison.Confidence.Level);
        Assert.Empty(comparison.Coverage.UnmatchedBaselineSignals);
        Assert.Empty(comparison.Coverage.UnmatchedCandidateSignals);
        Assert.DoesNotContain(comparison.Limitations, limitation =>
            limitation.Contains("audio-ahead episode metric semantics differ"));
    }

    [Fact]
    public void Compare_Reports_PartialConfidence_When_Signals_Are_Unmatched()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"),
            Check("AudioVideoDriftMsP95", "pass", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "25.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"),
            Check("ActualHdrOutput", "pass", "color-pipeline", "colorPipeline.actualHdrOutput", "Hdr10", "Hdr10"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("partial", comparison.Confidence.Level);
        Assert.Contains("unmatched comparison signals are present", comparison.Confidence.Reasons);
        Assert.Contains("sync.audioVideoDriftMsP95", comparison.Confidence.Signals);
        Assert.Contains("colorPipeline.actualHdrOutput", comparison.Confidence.Signals);
        Assert.Equal("review-unmatched-signals", comparison.Optimization.Action);
        Assert.Equal("medium", comparison.Optimization.Risk);
        Assert.Contains("partial comparison evidence requires unmatched signal review", comparison.Optimization.Reasons);
        Assert.Contains("sync.audioVideoDriftMsP95", comparison.Optimization.Signals);
        Assert.Contains("colorPipeline.actualHdrOutput", comparison.Optimization.Signals);
    }

    [Fact]
    public void Compare_Reports_ChangesOptimizationStrategy_When_Same_FailureArea_Stalls()
    {
        var previousBaseline = CreateReport(
            "previous-baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var previousCandidate = CreateReport(
            "previous-candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var previousComparison = PlaybackQualityRunComparator.Compare(previousBaseline, previousCandidate);
        var context = new PlaybackQualityComparisonContext
        {
            StallComparisonCountThreshold = 2
        };
        context.PreviousComparisons.Add(previousComparison);
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate, context);

        Assert.Equal("unchanged", comparison.Result);
        Assert.Equal("change-optimization-strategy", comparison.Optimization.Action);
        Assert.Equal("high", comparison.Optimization.Risk);
        Assert.Contains("repeated unchanged comparisons indicate optimization stall", comparison.Optimization.Reasons);
        Assert.Contains("iteration.stalled", comparison.Optimization.Blockers);
        Assert.Contains("frame-pacing", comparison.Optimization.FailureAreas);
        Assert.Contains("timing.maxFrameGapMs", comparison.Optimization.Signals);
        Assert.Contains("Change optimization strategy", comparison.SuggestedNextAction);
        var nextAction = Assert.Single(comparison.NextActions);
        Assert.Equal("change-optimization-strategy", nextAction.Action);
        Assert.Equal("frame-pacing", nextAction.FailureArea);
        Assert.Contains("iteration.stalled", nextAction.Blockers);
        Assert.Contains(
            nextAction.Reasons,
            reason => reason.Contains("Change optimization strategy"));
    }

    [Fact]
    public void Compare_Reports_Regressed_When_Candidate_Has_Unmatched_New_Failing_Signal()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "pass", "frame-pacing", "timing.maxFrameGapMs", "105.000", "80.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "pass", "frame-pacing", "timing.maxFrameGapMs", "105.000", "82.000"),
            Check("ActualHdrOutput", "fail", "color-pipeline", "colorPipeline.actualHdrOutput", "Hdr10", "Sdr"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("regressed", comparison.Result);
        Assert.Contains("color-pipeline", comparison.NewFailureAreas);
        Assert.Contains(comparison.Regressions, delta =>
            delta.Signal == "colorPipeline.actualHdrOutput" &&
            delta.Direction == "candidate-only-failure" &&
            delta.BaselineStatus == "" &&
            delta.CandidateStatus == "fail");
    }

    [Fact]
    public void Compare_Reports_Insufficient_Evidence_When_Checks_Are_Missing()
    {
        var baseline = CreateReport("baseline");
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Equal("collect-comparable-evidence", comparison.Decision);
        Assert.Contains("Collect comparable baseline and candidate checks", comparison.SuggestedNextAction);
        Assert.Contains("comparison requires baseline and candidate checks", comparison.Limitations);
        Assert.Contains("comparison.missing-checks", comparison.Optimization.Blockers);
    }

    [Fact]
    public void Compare_Reports_Insufficient_Evidence_When_No_Checks_Can_Be_Matched()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("ActualHdrOutput", "fail", "color-pipeline", "colorPipeline.actualHdrOutput", "Hdr10", "Sdr"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Contains("comparison requires at least one matching check signal", comparison.Limitations);
        Assert.Contains("comparison.no-matched-signals", comparison.Optimization.Blockers);
    }

    [Fact]
    public void Compare_Reports_Insufficient_Evidence_When_Media_Source_Differs()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        baseline.Source.ItemId = "item-1";
        baseline.Source.MediaSourceId = "source-a";
        baseline.Source.FrameRate = 23.976;
        baseline.Source.HdrKind = "Hdr10";

        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Source.ItemId = "item-1";
        candidate.Source.MediaSourceId = "source-b";
        candidate.Source.FrameRate = 23.976;
        candidate.Source.HdrKind = "Hdr10";

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Equal("collect-comparable-evidence", comparison.Decision);
        Assert.Equal("incompatible", comparison.Comparability.Status);
        Assert.Contains("source.mediaSourceId mismatch", comparison.Comparability.Reasons);
        Assert.Contains("source.mediaSourceId", comparison.Comparability.Signals);
        Assert.Contains("comparison requires matching source.mediaSourceId", comparison.Limitations);
        Assert.Equal("weak", comparison.Confidence.Level);
        Assert.Contains("comparison inputs are incompatible", comparison.Confidence.Reasons);
        Assert.Contains("source.mediaSourceId", comparison.Confidence.Signals);
        Assert.Equal("collect-comparable-evidence", comparison.Optimization.Action);
        Assert.Equal("high", comparison.Optimization.Risk);
        Assert.Contains("weak comparison confidence blocks playback Core optimization", comparison.Optimization.Reasons);
        Assert.Contains("comparison.incompatible-inputs", comparison.Optimization.Blockers);
        Assert.Contains("comparison inputs are incompatible", comparison.Optimization.Blockers);
        Assert.Contains("source.mediaSourceId", comparison.Optimization.Signals);
    }

    [Fact]
    public void Compare_Reports_Comparable_When_Source_Identity_And_Media_Properties_Match()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        baseline.Source.ItemId = "item-1";
        baseline.Source.MediaSourceId = "source-a";
        baseline.Source.FrameRate = 23.976;
        baseline.Source.HdrKind = "Hdr10";

        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Source.ItemId = "item-1";
        candidate.Source.MediaSourceId = "source-a";
        candidate.Source.FrameRate = 23.976;
        candidate.Source.HdrKind = "Hdr10";

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("improved", comparison.Result);
        Assert.Equal("comparable", comparison.Comparability.Status);
        Assert.Empty(comparison.Comparability.Reasons);
    }

    [Fact]
    public void Compare_Reports_Improves_When_Seek_Recovery_Duration_Decreases()
    {
        var baseline = CreateReport(
            "baseline",
            Check("SeekRecoveryDurationMs", "fail", "timeline", "position.seekRecoveryDurationMs", "2000", "5000"));
        baseline.Position.SeekOperationDurationMs = 4800;
        baseline.Position.SeekRecoveryDurationMs = 5000;
        baseline.Position.SeekDemuxTargetTicks = 10_000_000;
        baseline.Position.PostSeekPositionTicks = 30_000_000;
        baseline.Position.SeekPacketCacheEnabled = false;
        baseline.Position.SeekPacketCacheHit = false;
        baseline.Position.SeekPacketCachePacketCount = 0;
        baseline.Position.SeekPacketCacheBytes = 0;
        baseline.Position.SeekPacketCacheWindowDurationTicks = 0;
        baseline.Position.SeekFallbackReason = "disabled";

        var candidate = CreateReport(
            "candidate",
            Check("SeekRecoveryDurationMs", "pass", "timeline", "position.seekRecoveryDurationMs", "2000", "900"));
        candidate.Position.SeekOperationDurationMs = 800;
        candidate.Position.SeekRecoveryDurationMs = 900;
        candidate.Position.SeekDemuxTargetTicks = -1;
        candidate.Position.PostSeekPositionTicks = 31_000_000;
        candidate.Position.SeekPacketCacheEnabled = true;
        candidate.Position.SeekPacketCacheHit = true;
        candidate.Position.SeekPacketCachePacketCount = 128;
        candidate.Position.SeekPacketCacheBytes = 1048576;
        candidate.Position.SeekPacketCacheWindowDurationTicks = 80000000;
        candidate.Position.SeekFallbackReason = "none";

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Contains("position.seekOperationDurationMs", comparison.Coverage.MatchedSignals);
        Assert.Contains("position.seekRecoveryDurationMs", comparison.Coverage.MatchedSignals);
        Assert.Contains("position.seekPacketCacheEnabled", comparison.Coverage.MatchedSignals);
        Assert.Contains("position.seekPacketCacheHit", comparison.Coverage.MatchedSignals);
        Assert.Contains("position.seekPacketCachePacketCount", comparison.Coverage.MatchedSignals);
        Assert.Contains("position.seekPacketCacheBytes", comparison.Coverage.MatchedSignals);
        Assert.Contains("position.seekPacketCacheWindowDurationTicks", comparison.Coverage.MatchedSignals);
        Assert.Contains("position.seekFallbackReason", comparison.Coverage.MatchedSignals);
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "position.seekRecoveryDurationMs" &&
            delta.FailureArea == "timeline" &&
            delta.Direction == "resolved");
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "position.seekOperationDurationMs" &&
            delta.Direction == "decreased");
        Assert.Empty(comparison.Regressions);
        Assert.Equal("improved", comparison.Result);
    }

    [Fact]
    public void Compare_Reports_Insufficient_When_Execution_Evidence_Levels_Differ()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        baseline.Execution.EvidenceLevel = PlaybackQualityEvidenceLevel.Orchestration;
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Empty(comparison.Improvements);
        Assert.Empty(comparison.Regressions);
        Assert.Contains("execution.evidenceLevel", comparison.Comparability.Signals);
    }

    [Fact]
    public void Compare_Reports_Insufficient_When_Execution_Runners_Differ()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Execution.Runner = "alternate-native-runner";

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Empty(comparison.Improvements);
        Assert.Empty(comparison.Regressions);
        Assert.Contains("execution.runner", comparison.Comparability.Signals);
    }

    [Fact]
    public void Compare_Reports_Insufficient_When_Execution_Scenarios_Differ()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        baseline.Execution.Scenario = PlaybackQualityExecutionScenario.AudioSwitch;
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Execution.Scenario = PlaybackQualityExecutionScenario.SubtitleSwitch;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Empty(comparison.Improvements);
        Assert.Empty(comparison.Regressions);
        Assert.Contains("execution.scenario", comparison.Comparability.Signals);
    }

    [Fact]
    public void Compare_Reports_Insufficient_When_Requested_Sample_Durations_Differ()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        baseline.Execution.RequestedSampleDurationMs = 30000;
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "pass", "frame-pacing", "timing.maxFrameGapMs", "105.000", "80.000"));
        candidate.Execution.RequestedSampleDurationMs = 5000;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Empty(comparison.Improvements);
        Assert.Empty(comparison.Regressions);
        Assert.Contains("execution.requestedSampleDurationMs", comparison.Comparability.Signals);
    }

    [Fact]
    public void Compare_SubtitleSwitch_Uses_Scenario_Outcome_And_Ignores_Frame_Pacing_Noise()
    {
        var baseline = CreateReport(
            "baseline",
            Check("LifecycleOperation", "fail", "subtitles", "lifecycle.subtitle-switch", "completed", "failed"),
            Check("ConversionStatus", "pass", "color-pipeline", "colorPipeline.conversionStatus", "validated", "validated"));
        baseline.Execution.Scenario = PlaybackQualityExecutionScenario.SubtitleSwitch;
        baseline.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "subtitle-switch",
            Status = "failed"
        });
        baseline.Timing.ExpectedFrameDurationMs = 41.7;
        baseline.Timing.RenderIntervalMsP99 = 42.0;
        baseline.Timing.MaxFrameGapMs = 43.0;
        baseline.Timing.RenderedVideoFrames = 100;

        var candidate = CreateReport(
            "candidate",
            Check("ConversionStatus", "pass", "color-pipeline", "colorPipeline.conversionStatus", "validated", "validated"));
        candidate.Execution.Scenario = PlaybackQualityExecutionScenario.SubtitleSwitch;
        candidate.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "subtitle-switch",
            Status = "completed"
        });
        candidate.Timing.ExpectedFrameDurationMs = 41.7;
        candidate.Timing.RenderIntervalMsP99 = 70.0;
        candidate.Timing.MaxFrameGapMs = 80.0;
        candidate.Timing.RenderedVideoFrames = 100;

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("improved", comparison.Result);
        Assert.Contains("subtitles", comparison.ResolvedFailureAreas);
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "lifecycle.subtitle-switch" &&
            delta.Direction == "resolved");
        Assert.DoesNotContain(comparison.Regressions, delta =>
            delta.Signal.StartsWith("framePacing.", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_Reports_Insufficient_When_Source_Locator_Hashes_Differ()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Execution.SourceLocatorHash = "sha256:" + new string('b', 64);

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Empty(comparison.Improvements);
        Assert.Contains("execution.sourceLocatorHash", comparison.Comparability.Signals);
    }

    [Fact]
    public void Compare_Reports_Insufficient_When_Completed_Execution_Is_Incomplete()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Execution.OpenedSourceHash = "";

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Empty(comparison.Improvements);
        Assert.Contains("execution.openedSourceHash", comparison.Comparability.Signals);
    }

    [Fact]
    public void Compare_Reports_Insufficient_When_Opened_Source_Hashes_Differ()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Execution.OpenedSourceHash = "sha256:" + new string('c', 64);

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Empty(comparison.Improvements);
        Assert.Contains("execution.openedSourceHash", comparison.Comparability.Signals);
    }

    [Fact]
    public void Compare_Reports_Insufficient_When_Opened_Source_Hash_Kind_Is_Missing()
    {
        var baseline = CreateReport("baseline");
        var candidate = CreateReport("candidate");
        candidate.Execution.OpenedSourceHashKind = "";

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Contains("execution.openedSourceHashKind", comparison.Comparability.Signals);
    }

    [Fact]
    public void Compare_Reports_Insufficient_When_Opened_Source_Hash_Kinds_Differ()
    {
        var baseline = CreateReport("baseline");
        var candidate = CreateReport("candidate");
        candidate.Execution.OpenedSourceHashKind = "legacy-locator-hash";

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Contains("execution.openedSourceHashKind", comparison.Comparability.Signals);
    }

    [Fact]
    public void Serializer_Writes_Run_Comparison_With_CamelCase_Field_Names()
    {
        var baseline = CreateReport(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        var json = PlaybackQualityReportSerializer.Serialize(comparison);

        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"baselineRunId\"", json);
        Assert.Contains("\"candidateRunId\"", json);
        Assert.Contains("\"decision\"", json);
        Assert.Contains("\"suggestedNextAction\"", json);
        Assert.Contains("\"comparability\"", json);
        Assert.Contains("\"confidence\"", json);
        Assert.Contains("\"optimization\"", json);
        Assert.Contains("\"coverage\"", json);
        Assert.Contains("\"improvements\"", json);
        Assert.Contains("\"policyChanges\"", json);
        Assert.Contains("\"timing.maxFrameGapMs\"", json);
    }

    [Fact]
    public void Serializer_Deserializes_Report_Checks_For_Cli_Comparison()
    {
        var json = """
        {
          "runId": "baseline",
          "checks": [
            {
              "name": "MaxFrameGapMs",
              "signal": "timing.maxFrameGapMs",
              "status": "fail",
              "failureArea": "frame-pacing",
              "expected": "105.000",
              "actual": "180.000"
            }
          ]
        }
        """;

        var report = PlaybackQualityReportSerializer.Deserialize(json);

        Assert.Single(report.Checks);
        Assert.Equal("timing.maxFrameGapMs", report.Checks[0].Signal);
    }

    private static PlaybackQualityReport CreateReport(
        string runId,
        params PlaybackQualityCheck[] checks)
    {
        var report = new PlaybackQualityReport { RunId = runId };
        report.Environment.PlayerCoreVersion = "core-" + runId;
        report.Environment.SourceRevision = "revision-" + runId;
        report.Environment.BuildConfiguration = "Debug";
        report.Execution = new PlaybackQualityExecutionEvidence
        {
            AttemptId = "attempt-" + runId,
            Runner = "native-headless",
            Scenario = PlaybackQualityExecutionScenario.Playback,
            EvidenceLevel = PlaybackQualityEvidenceLevel.NativePlayback,
            Status = PlaybackQualityExecutionStatus.Completed,
            SourceLocatorHash = "sha256:" + new string('a', 64),
            OpenedSourceHash = "sha256:" + new string('a', 64),
            OpenedSourceHashKind = PlaybackQualitySourceFingerprint.OpenedMediaSignatureKind,
            StartedAtUtc = "2026-07-11T00:00:00.0000000+00:00",
            DurationMs = 1000,
            RequestedSampleDurationMs = 5000,
            SourceOpenAttempted = true,
            SourceOpened = true,
            NativeGraphOpened = true,
            DemuxStarted = true,
            DecoderOpened = true,
            PlaybackSampleObserved = true
        };
        foreach (var check in checks)
        {
            report.Checks.Add(check);
        }

        report.Result = HasFailedCheck(report) ? "fail" : "pass";
        return report;
    }

    private static void AddTrackEvidence(PlaybackQualityReport report)
    {
        report.Tracks.VideoTrackCount = 1;
        report.Tracks.AudioTrackCount = 1;
        report.Tracks.SubtitleTrackCount = 1;
        report.Tracks.SelectedVideoStreamIndex = 0;
        report.Tracks.SelectedAudioStreamIndex = 1;
        report.Tracks.SelectedSubtitleStreamIndex = 2;
        report.Tracks.IsSubtitleDisabled = false;
        report.Tracks.Video.Add(new PlaybackQualityTrack
        {
            Index = 0,
            Kind = "Video",
            Codec = "hevc",
            IsDefault = true,
            IsForced = false
        });
        report.Tracks.Audio.Add(new PlaybackQualityTrack
        {
            Index = 1,
            Kind = "Audio",
            Codec = "aac",
            Language = "eng",
            ChannelLayout = "5.1",
            Channels = 6,
            IsDefault = true,
            IsForced = false
        });
        report.Tracks.Subtitles.Add(new PlaybackQualityTrack
        {
            Index = 2,
            Kind = "Subtitle",
            Codec = "mov_text",
            Language = "eng",
            IsExternal = false,
            IsDefault = false,
            IsForced = false
        });
    }

    private static void AddRuntimePlaybackEvidence(PlaybackQualityReport report)
    {
        report.Timing.RenderIntervalMsP95 = 47.0;
        report.Timing.RenderIntervalMsP99 = 48.0;
        report.Timing.PresentDurationMsP95 = 16.7;
        report.Timing.PresentDurationMsMax = 33.4;
        report.Timing.AudioAheadWaitDurationMsP95 = 15.0;
        report.Timing.AudioAheadWaitDurationMsMax = 25.0;
        report.Timing.AudioAheadWaitTargetMsP95 = 4.0;
        report.Timing.AudioAheadWaitTargetMsMax = 6.0;
        report.Timing.AudioAheadWaitOversleepMsP95 = 11.0;
        report.Timing.AudioAheadWaitOversleepMsMax = 19.0;
        report.Timing.AudioAheadWaitFinalDeltaAbsMsP95 = 105.0;
        report.Timing.AudioAheadWaitFinalDeltaAbsMsMax = 120.0;
        report.Timing.AudioAheadWaitPassDurationMsP95 = 12.0;
        report.Timing.AudioAheadWaitPassDurationMsMax = 17.0;
        report.Timing.AudioAheadWaitPassTargetMsP95 = 4.0;
        report.Timing.AudioAheadWaitPassTargetMsMax = 6.0;
        report.Timing.AudioAheadWaitPassOversleepMsP95 = 8.0;
        report.Timing.AudioAheadWaitPassOversleepMsMax = 11.0;
        report.Timing.RenderIntervalAfterAudioAheadWaitSampleCount = 2;
        report.Timing.RenderIntervalAfterAudioAheadWaitMsP95 = 43.0;
        report.Timing.RenderIntervalAfterAudioAheadWaitMsMax = 45.0;
        report.Timing.AudioAheadWaitEndToPresentSampleCount = 2;
        report.Timing.AudioAheadWaitEndToPresentMsP50 = 2.0;
        report.Timing.AudioAheadWaitEndToPresentMsP95 = 3.0;
        report.Timing.AudioAheadWaitEndToPresentMsP99 = 4.0;
        report.Timing.AudioAheadWaitEndToPresentMsMax = 5.0;
        report.Timing.RenderIntervalAfterNonAudioWaitSampleCount = 3;
        report.Timing.RenderIntervalAfterNonAudioWaitMsP95 = 34.0;
        report.Timing.RenderIntervalAfterNonAudioWaitMsMax = 36.0;
        report.Timing.MaxFrameGapMs = 48.0;
        report.Timing.VideoAheadWaitCount = 52;
        report.Timing.AudioAheadWaitCount = 50;
        report.Timing.VideoClockWaitCount = 2;
        report.Sync.AudioClockTicks = 15100000;
        report.Sync.VideoPositionTicks = 15000000;
        report.Sync.AudioVideoDriftMsP95 = 10.0;
        report.Buffers.SubmittedAudioFrames = 82;
        report.Buffers.QueuedAudioBuffers = 12;
        report.Buffers.VideoStarvedPasses = 0;
        report.Buffers.AudioStarvedPasses = 0;
        report.RuntimeMetrics.ProcessWallClockMs = 5123.4;
        report.RuntimeMetrics.ProcessCpuTimeMs = 245.6;
        report.RuntimeMetrics.ProcessCpuUtilizationRatio = 0.048;
    }

    private static PlaybackQualityCheck Check(
        string name,
        string status,
        string failureArea,
        string signal,
        string expected,
        string actual)
    {
        return new PlaybackQualityCheck
        {
            Name = name,
            Status = status,
            FailureArea = failureArea,
            Signal = signal,
            Expected = expected,
            Actual = actual
        };
    }

    private static bool HasFailedCheck(PlaybackQualityReport report)
    {
        foreach (var check in report.Checks)
        {
            if (check.Status == "fail")
            {
                return true;
            }
        }

        return false;
    }
}
