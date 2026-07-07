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
    public void Summarize_Emits_SignalSummaries_For_Cross_Case_Trend_Diagnosis()
    {
        var improvedA = Compare(
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"),
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        improvedA.CaseId = "case-frame-a";
        var improvedB = Compare(
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "160.000"),
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "130.000"));
        improvedB.CaseId = "case-frame-b";
        var regressed = Compare(
            Check("AudioVideoDriftMsP95", "pass", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "25.000"),
            Check("AudioVideoDriftMsP95", "fail", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "55.000"));
        regressed.CaseId = "case-av-sync";

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(
            new[] { improvedA, improvedB, regressed });

        Assert.Contains(suite.SignalSummaries, summary =>
            summary.Signal == "timing.maxFrameGapMs" &&
            summary.FailureArea == "frame-pacing" &&
            summary.Outcome == "improved" &&
            summary.ImprovementCount == 2 &&
            summary.RegressionCount == 0 &&
            summary.CaseIds.Contains("case-frame-a") &&
            summary.CaseIds.Contains("case-frame-b") &&
            summary.Directions.Contains("decreased"));
        Assert.Contains(suite.SignalSummaries, summary =>
            summary.Signal == "sync.audioVideoDriftMsP95" &&
            summary.FailureArea == "av-sync" &&
            summary.Outcome == "regressed" &&
            summary.ImprovementCount == 0 &&
            summary.RegressionCount == 1 &&
            summary.CaseIds.Contains("case-av-sync"));
    }

    [Fact]
    public void Summarize_Surfaces_Policy_Changes_As_Neutral_Model_Context()
    {
        var frameDurationMs = 1000.0 / 60.0;
        var baseline = Report(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "50.000", "80.000"));
        baseline.Timing.ExpectedFrameDurationMs = frameDurationMs;
        baseline.Timing.LateFrameDropToleranceMs = frameDurationMs * 6.0;

        var candidate = Report(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "50.000", "80.000"));
        candidate.Timing.ExpectedFrameDurationMs = frameDurationMs;
        candidate.Timing.LateFrameDropToleranceMs = frameDurationMs * 2.5;
        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);
        comparison.CaseId = "case-policy-change";

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { comparison });

        Assert.Equal("continue-next-triage-step", suite.Action);
        Assert.Equal(0, suite.ImprovedCount);
        Assert.Equal(0, suite.RegressedCount);
        Assert.Equal(1, suite.PolicyChangeCount);
        Assert.Contains("framePacing.lateFrameDropToleranceFrameRatio", suite.Signals);
        var caseSummary = Assert.Single(suite.Cases);
        Assert.Equal(1, caseSummary.PolicyChangeCount);
        Assert.Contains("framePacing.lateFrameDropToleranceFrameRatio", caseSummary.Signals);
        Assert.Contains("candidate changed Core policy signal without quality delta", caseSummary.Reasons);
        Assert.Contains(suite.SignalSummaries, summary =>
            summary.Signal == "framePacing.lateFrameDropToleranceFrameRatio" &&
            summary.FailureArea == "frame-pacing" &&
            summary.Outcome == "policy-changed" &&
            summary.PolicyChangeCount == 1 &&
            summary.ImprovementCount == 0 &&
            summary.RegressionCount == 0 &&
            summary.CaseIds.Contains("case-policy-change") &&
            summary.Directions.Contains("decreased"));
        var json = PlaybackQualityReportSerializer.Serialize(suite);
        Assert.Contains("\"policyChangeCount\": 1", json);
        Assert.Contains("\"outcome\": \"policy-changed\"", json);
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
        Assert.Contains("comparison.missing-checks", suite.Blockers);
        Assert.Contains("evidence/missing-baseline", suite.TargetCaseIds);
        Assert.Contains("src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportMapper.cs", suite.CodeTargets);
        Assert.Contains("src/NextGenEmby.Native/NativePlaybackQualityMetrics.cpp", suite.CodeTargets);
    }

    [Fact]
    public void Summarize_CollectsEvidence_When_Build_Identity_Is_Missing()
    {
        var baseline = Report(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        var candidate = Report(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        baseline.Environment = new PlaybackQualityEnvironment();
        candidate.Environment = new PlaybackQualityEnvironment();

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);
        comparison.CaseId = "missing-env/case.json";

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { comparison });

        Assert.Equal("collect-comparable-evidence", suite.Action);
        Assert.Equal("high", suite.Risk);
        Assert.Equal(1, suite.Environment.MissingEvidenceCount);
        Assert.Contains("suite.environment-evidence-missing", suite.Blockers);
        Assert.Contains("environment.identity", suite.Environment.Signals);
        Assert.Contains("missing-env/case.json", suite.TargetCaseIds);
        var nextAction = Assert.Single(suite.NextActions);
        Assert.Contains("missing-env/case.json", nextAction.CaseIds);
        Assert.Contains("environment.identity", nextAction.Signals);
    }

    [Fact]
    public void Summarize_Emits_NextAction_For_Evidence_Blockers()
    {
        var weak = PlaybackQualityRunComparator.Compare(
            new PlaybackQualityReport { RunId = "baseline-missing" },
            Report("candidate", Check("RenderedVideoFrames", "fail", "frame-pacing", "timing.renderedVideoFrames", "120", "24")));
        weak.CaseId = "evidence/missing-baseline";

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { weak });

        var nextAction = Assert.Single(suite.NextActions);
        Assert.Equal(1, nextAction.Rank);
        Assert.Equal("collect-comparable-evidence", nextAction.Action);
        Assert.Equal("high", nextAction.Risk);
        Assert.Contains("suite.weak-evidence", nextAction.Blockers);
        Assert.Contains("comparison.missing-checks", nextAction.Blockers);
        Assert.Contains("evidence/missing-baseline", nextAction.CaseIds);
        Assert.Contains("src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportMapper.cs", nextAction.CodeTargets);
        Assert.Contains("suite contains weak or insufficient comparison evidence", nextAction.Reasons);
    }

    [Fact]
    public void Summarize_Emits_NextAction_For_Candidate_Regressions()
    {
        var regressed = Compare(
            Check("AudioVideoDriftMsP95", "pass", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "25.000"),
            Check("AudioVideoDriftMsP95", "fail", "av-sync", "sync.audioVideoDriftMsP95", "40.000", "55.000"));
        regressed.CaseId = "case-av-sync";

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { regressed });

        var nextAction = Assert.Single(suite.NextActions);
        Assert.Equal(1, nextAction.Rank);
        Assert.Equal("reject-candidate", nextAction.Action);
        Assert.Equal("av-sync", nextAction.FailureArea);
        Assert.Contains("case-av-sync", nextAction.CaseIds);
        Assert.Contains("sync.audioVideoDriftMsP95", nextAction.Signals);
        Assert.Contains("src/NextGenEmby.Native/Media/AudioRenderer.cpp", nextAction.CodeTargets);
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
        Assert.Contains("Keep candidate playback Core change", caseSummary.SuggestedNextAction);
        Assert.Contains("strong comparison evidence supports candidate", caseSummary.Reasons);
        Assert.Contains("all comparison checks matched", caseSummary.Reasons);
        Assert.Contains("timing.maxFrameGapMs", caseSummary.Signals);
        Assert.Contains("frame-pacing", caseSummary.FailureAreas);
        Assert.Contains("src/NextGenEmby.Native/Media/FramePacing.h", caseSummary.CodeTargets);
        Assert.Contains("src/NextGenEmby.Native/Media/PlaybackGraph.cpp", caseSummary.CodeTargets);
    }

    [Fact]
    public void Summarize_Surfaces_Environment_Evidence_For_Model_Gating()
    {
        var baseline = Report(
            "baseline",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "180.000"));
        baseline.Environment.PlayerCoreVersion = "native-core-v42";
        baseline.Environment.SourceRevision = "same123";

        var candidate = Report(
            "candidate",
            Check("MaxFrameGapMs", "fail", "frame-pacing", "timing.maxFrameGapMs", "105.000", "120.000"));
        candidate.Environment.PlayerCoreVersion = "native-core-v42";
        candidate.Environment.SourceRevision = "same123";

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);
        comparison.CaseId = "same-build/case.json";

        var suite = PlaybackQualityComparisonSuiteAggregator.Summarize(new[] { comparison });

        Assert.Equal("collect-comparable-evidence", suite.Action);
        Assert.Equal("high", suite.Risk);
        Assert.Equal(1, suite.Environment.SameBuildCount);
        Assert.Equal(0, suite.Environment.DifferentBuildCount);
        Assert.Contains("same-build/case.json", suite.Environment.SameBuildCaseIds);
        Assert.Contains("suite.environment-same-build", suite.Blockers);
        Assert.Contains("environment.playerCoreVersion", suite.Environment.Signals);
        Assert.Contains("environment.sourceRevision", suite.Environment.Signals);
        var caseSummary = Assert.Single(suite.Cases);
        Assert.Equal("same-build", caseSummary.EnvironmentStatus);
        Assert.Contains("environment.sourceRevision", caseSummary.EnvironmentSignals);
        var nextAction = Assert.Single(suite.NextActions);
        Assert.Contains("suite.environment-same-build", nextAction.Blockers);
        Assert.Contains("same-build/case.json", nextAction.CaseIds);
        var json = PlaybackQualityReportSerializer.Serialize(suite);
        Assert.Contains("\"sameBuildCount\": 1", json);
        Assert.Contains("\"environmentStatus\": \"same-build\"", json);
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
        Assert.Contains("src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.cpp", suite.CodeTargets);
        Assert.Contains("src/NextGenEmby.Native/DxDeviceResources.cpp", suite.CodeTargets);
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
        report.Environment.PlayerCoreVersion = "core-" + runId;
        report.Environment.SourceRevision = "revision-" + runId;
        report.Environment.BuildConfiguration = "Debug";
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
