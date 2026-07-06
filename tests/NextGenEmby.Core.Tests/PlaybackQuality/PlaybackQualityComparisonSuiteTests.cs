using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityComparisonSuiteTests
{
    [Fact]
    public void Summarize_AcceptsCandidate_When_StrongImprovement_Has_No_Blocking_Comparisons()
    {
        var improved = Compare(
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"),
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        var unchanged = Compare(
            Check("AudioVideoDriftMsP95", "pass", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "25.000"),
            Check("AudioVideoDriftMsP95", "pass", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "26.000"));

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { improved, unchanged });

        Assert.Equal(2, suite.TotalComparisonCount);
        Assert.Equal(1, suite.ImprovedCount);
        Assert.Equal(1, suite.UnchangedCount);
        Assert.Equal("accept-candidate", suite.Action);
        Assert.Equal("low", suite.Risk);
        Assert.Contains("suite has strong improvement and no blocking comparisons", suite.Reasons);
        Assert.Contains("timing.maxFrameGapMs", suite.Signals);
    }

    [Fact]
    public void Summarize_RejectsCandidate_When_Any_Comparison_Regresses()
    {
        var improved = Compare(
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"),
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        var regressed = Compare(
            Check("AudioVideoDriftMsP95", "pass", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "25.000"),
            Check("AudioVideoDriftMsP95", "fail", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "55.000"));
        regressed.CaseId = "case-av-sync";

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { improved, regressed });

        Assert.Equal("reject-candidate", suite.Action);
        Assert.Equal("high", suite.Risk);
        Assert.Equal(1, suite.RegressedCount);
        Assert.Contains("suite.regression", suite.Blockers);
        Assert.Contains("sync.audioVideoDriftMsP95", suite.Signals);
        Assert.Contains("av-sync", suite.FailureAreas);
        Assert.Contains("av-sync", suite.TargetFailureAreas);
        Assert.Contains("case-av-sync", suite.TargetCaseIds);
    }

    [Fact]
    public void Summarize_CollectsEvidence_When_Any_Comparison_Is_Weak_And_None_Regress()
    {
        var improved = Compare(
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"),
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        var weak = PlaybackQualityRunComparator.Compare(
            new PlaybackQualityReport { RunId = "baseline-missing" },
            Report("candidate", Check("RenderedVideoFrames", "fail", "frame-pacing", "timing.renderedVideoFrames", "120", "24")));
        weak.CaseId = "evidence/missing-baseline";

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { improved, weak });

        Assert.Equal("collect-comparable-evidence", suite.Action);
        Assert.Equal("high", suite.Risk);
        Assert.Equal(1, suite.InsufficientEvidenceCount);
        Assert.Equal(1, suite.WeakConfidenceCount);
        Assert.Contains("suite.weak-evidence", suite.Blockers);
        Assert.Contains("evidence/missing-baseline", suite.TargetCaseIds);
    }

    [Fact]
    public void Summarize_Emits_Case_Summaries_For_Model_Localization()
    {
        var improved = Compare(
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"),
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        improved.CaseId = "hdr/case-a.json";

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { improved });

        var caseSummary = Assert.Single(suite.Cases);
        Assert.Equal("hdr/case-a.json", caseSummary.CaseId);
        Assert.Equal("baseline", caseSummary.BaselineRunId);
        Assert.Equal("candidate", caseSummary.CandidateRunId);
        Assert.Equal("improved", caseSummary.Result);
        Assert.Equal("keep-candidate", caseSummary.Decision);
        Assert.Equal("accept-candidate", caseSummary.Action);
        Assert.Equal("low", caseSummary.Risk);
        Assert.Equal("strong", caseSummary.Confidence);
        Assert.Contains("timing.maxFrameGapMs", caseSummary.Signals);
        Assert.Contains("frame-pacing", caseSummary.FailureAreas);
    }

    [Fact]
    public void Summarize_Emits_Persisting_FailureAreas_For_Unchanged_Cases()
    {
        var unchanged = Compare(
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"),
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        unchanged.CaseId = "cadence/frame-pacing.json";

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { unchanged });

        var caseSummary = Assert.Single(suite.Cases);
        Assert.Equal("continue-next-triage-step", caseSummary.Action);
        Assert.Contains("frame-pacing", caseSummary.FailureAreas);
        Assert.Contains("frame-pacing", suite.FailureAreas);
        Assert.Contains("frame-pacing", suite.TargetFailureAreas);
        Assert.Contains("cadence/frame-pacing.json", suite.TargetCaseIds);
        Assert.Contains("timing.maxFrameGapMs", caseSummary.Signals);
        Assert.Contains("timing.maxFrameGapMs", suite.Signals);
    }

    [Fact]
    public void Summarize_TargetFailureAreas_Uses_Playback_Failure_Priority()
    {
        var mixed = Compare(
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"),
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        mixed.CaseId = "case-color";
        mixed.Regressions.Add(new PlaybackQualitySignalDelta
        {
            Signal = "colorPipeline.actualHdrOutput",
            FailureArea = "color-pipeline",
            Direction = "candidate-only-failure",
            CandidateStatus = "fail"
        });
        mixed.NewFailureAreas.Add("color-pipeline");
        mixed.Result = "mixed";
        mixed.Optimization.Action = "split-candidate";
        mixed.Optimization.Risk = "high";
        var unchanged = Compare(
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"),
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        unchanged.CaseId = "case-frame";

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { mixed, unchanged });

        var target = Assert.Single(suite.TargetFailureAreas);
        Assert.Equal("color-pipeline", target);
        var targetCase = Assert.Single(suite.TargetCaseIds);
        Assert.Equal("case-color", targetCase);
    }

    private static PlaybackQualityRunComparison Compare(
        PlaybackQualityCheck baselineCheck,
        PlaybackQualityCheck candidateCheck)
    {
        return PlaybackQualityRunComparator.Compare(
            Report("baseline", baselineCheck),
            Report("candidate", candidateCheck));
    }

    private static PlaybackQualityReport Report(
        string runId,
        params PlaybackQualityCheck[] checks)
    {
        var report = new PlaybackQualityReport { RunId = runId };
        report.Source.ItemId = "item-1";
        report.Source.MediaSourceId = "source-1";
        report.Source.FrameRate = 23.976;
        report.Source.HdrKind = "Sdr";
        foreach (var check in checks)
        {
            report.Checks.Add(check);
        }

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
}
