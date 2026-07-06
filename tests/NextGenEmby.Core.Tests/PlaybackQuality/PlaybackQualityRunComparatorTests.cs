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
        Assert.Contains("\"coverage\"", json);
        Assert.Contains("\"improvements\"", json);
        Assert.Contains("\"timing.maxFrameGapMs\"", json);
    }

    private static PlaybackQualityReport CreateReport(
        string runId,
        params PlaybackQualityCheck[] checks)
    {
        var report = new PlaybackQualityReport { RunId = runId };
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
