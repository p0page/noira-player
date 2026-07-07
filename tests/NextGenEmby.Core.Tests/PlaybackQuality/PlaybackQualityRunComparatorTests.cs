using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityRunComparatorTests
{
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
        Assert.Contains("comparison is missing baseline and candidate build identity", comparison.Optimization.Blockers);
        Assert.Contains("environment.identity", comparison.Optimization.Signals);
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
        var baseline = new PlaybackQualityReport { RunId = "baseline" };
        var candidate = CreateReport(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("insufficient-evidence", comparison.Result);
        Assert.Equal("collect-comparable-evidence", comparison.Decision);
        Assert.Contains("Collect comparable baseline and candidate checks", comparison.SuggestedNextAction);
        Assert.Contains("comparison requires baseline and candidate checks", comparison.Limitations);
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
        foreach (var check in checks)
        {
            report.Checks.Add(check);
        }

        report.Result = HasFailedCheck(report) ? "fail" : "pass";
        return report;
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
