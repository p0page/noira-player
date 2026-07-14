using System.Collections.Generic;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityRenderPhaseSummaryTests
{
    [Fact]
    public void Summarize_Reports_Consistent_Direction_And_Distribution_Across_All_Repeats()
    {
        var comparisons = new[]
        {
            Comparison("case-a", 1, 12, 2),
            Comparison("case-a", 2, 10, 1),
            Comparison("case-a", 3, 11, 1.5)
        };

        var summaries = PlaybackQualityRenderPhaseSummaryAggregator.Summarize(
            comparisons,
            new[] { "case-a" },
            expectedRepeatCount: 3);

        var caseSummary = Assert.Single(summaries);
        Assert.Equal("complete", caseSummary.Status);
        Assert.Equal(3, caseSummary.ComparableRepeatCount);
        var signal = Assert.Single(caseSummary.Signals);
        Assert.Equal("consistent-lower", signal.DirectionConsistency);
        Assert.Equal(3, signal.LowerCount);
        Assert.Equal(10, signal.Baseline.Minimum);
        Assert.Equal(11, signal.Baseline.Median);
        Assert.Equal(12, signal.Baseline.Maximum);
        Assert.Equal(1, signal.Candidate.Minimum);
        Assert.Equal(1.5, signal.Candidate.Median);
        Assert.Equal(2, signal.Candidate.Maximum);
        Assert.Equal(-9.5, signal.AbsoluteDelta.Median);
    }

    [Fact]
    public void Summarize_Preserves_Mixed_Directions_Without_A_Score()
    {
        var comparisons = new[]
        {
            Comparison("case-a", 1, 12, 2),
            Comparison("case-a", 2, 1, 2),
            Comparison("case-a", 3, 5, 5)
        };

        var summaries = PlaybackQualityRenderPhaseSummaryAggregator.Summarize(
            comparisons,
            new[] { "case-a" },
            expectedRepeatCount: 3);

        var signal = Assert.Single(Assert.Single(summaries).Signals);
        Assert.Equal("mixed", signal.DirectionConsistency);
        Assert.Equal(1, signal.LowerCount);
        Assert.Equal(1, signal.HigherCount);
        Assert.Equal(1, signal.UnchangedCount);
    }

    [Fact]
    public void Summarize_Reports_Missing_Repeat_And_Missing_Case_As_Insufficient()
    {
        var comparisons = new[]
        {
            Comparison("case-a", 1, 12, 2),
            Comparison("case-a", 2, 10, 1)
        };

        var summaries = PlaybackQualityRenderPhaseSummaryAggregator.Summarize(
            comparisons,
            new[] { "case-a", "case-b" },
            expectedRepeatCount: 3);

        Assert.Collection(
            summaries,
            caseA =>
            {
                Assert.Equal("case-a", caseA.CaseId);
                Assert.Equal("insufficient-evidence", caseA.Status);
                Assert.Equal(2, caseA.ObservedRepeatCount);
                Assert.Equal("insufficient-evidence", Assert.Single(caseA.Signals).DirectionConsistency);
            },
            caseB =>
            {
                Assert.Equal("case-b", caseB.CaseId);
                Assert.Equal("insufficient-evidence", caseB.Status);
                Assert.Equal(0, caseB.ObservedRepeatCount);
                Assert.Empty(caseB.Signals);
            });
    }

    private static PlaybackQualityRenderPhaseComparison Comparison(
        string caseId,
        int repeatIndex,
        double baseline,
        double candidate)
    {
        var comparison = new PlaybackQualityRenderPhaseComparison
        {
            CaseId = caseId,
            RepeatIndex = repeatIndex,
            Status = "comparable",
            Result = candidate < baseline ? "improved" : candidate > baseline ? "regressed" : "unchanged"
        };
        comparison.Metrics.Add(new PlaybackQualityRenderPhaseMetricDelta
        {
            Signal = "timing.videoProcessorSetupCpuDurationMsP95",
            Baseline = baseline,
            Candidate = candidate,
            AbsoluteDelta = candidate - baseline,
            Direction = candidate < baseline ? "lower" : candidate > baseline ? "higher" : "unchanged"
        });
        return comparison;
    }
}
