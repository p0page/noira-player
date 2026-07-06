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
