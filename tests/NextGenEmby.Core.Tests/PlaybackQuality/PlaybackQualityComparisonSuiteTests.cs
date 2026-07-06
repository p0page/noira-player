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

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { improved, regressed });

        Assert.Equal("reject-candidate", suite.Action);
        Assert.Equal("high", suite.Risk);
        Assert.Equal(1, suite.RegressedCount);
        Assert.Contains("suite.regression", suite.Blockers);
        Assert.Contains("sync.audioVideoDriftMsP95", suite.Signals);
        Assert.Contains("av-sync", suite.FailureAreas);
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

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { improved, weak });

        Assert.Equal("collect-comparable-evidence", suite.Action);
        Assert.Equal("high", suite.Risk);
        Assert.Equal(1, suite.InsufficientEvidenceCount);
        Assert.Equal(1, suite.WeakConfidenceCount);
        Assert.Contains("suite.weak-evidence", suite.Blockers);
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
