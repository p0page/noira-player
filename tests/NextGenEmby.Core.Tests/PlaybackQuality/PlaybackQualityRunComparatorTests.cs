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

        Assert.Equal("improved", comparison.Result);
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
        Assert.Contains(comparison.Improvements, delta => delta.Signal == "timing.maxFrameGapMs");
        Assert.Contains(comparison.Regressions, delta => delta.Signal == "colorPipeline.actualHdrOutput");
        Assert.Contains("frame-pacing", comparison.PersistingFailureAreas);
        Assert.Contains("color-pipeline", comparison.NewFailureAreas);
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
        Assert.Contains("comparison requires baseline and candidate checks", comparison.Limitations);
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
