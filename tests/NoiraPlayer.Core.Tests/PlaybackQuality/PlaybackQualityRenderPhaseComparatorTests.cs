using System;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityRenderPhaseComparatorTests
{
    [Fact]
    public void Compare_Reports_All_Render_Phase_Percentiles_Without_Claiming_Candidate_Acceptance()
    {
        var baseline = CreateReport("baseline", "base-revision", 12.5, 1.0);
        var candidate = CreateReport("candidate", "candidate-revision", 0.02, 0.5);

        var result = PlaybackQualityRenderPhaseComparator.Compare(
            baseline,
            candidate,
            "case-a",
            repeatIndex: 2);

        Assert.Equal("comparable", result.Status);
        Assert.Equal("improved", result.Result);
        Assert.Equal("video-render-phases", result.ComparisonScope);
        Assert.Equal("case-a", result.CaseId);
        Assert.Equal(2, result.RepeatIndex);
        Assert.Equal(24, result.Metrics.Count);
        var setupP95 = Assert.Single(result.Metrics, metric =>
            metric.Signal == "timing.videoProcessorSetupCpuDurationMsP95");
        Assert.Equal(12.5, setupP95.Baseline);
        Assert.Equal(0.02, setupP95.Candidate);
        Assert.Equal(-12.48, setupP95.AbsoluteDelta, 6);
        Assert.Equal(0.0016, setupP95.CandidateToBaselineRatio!.Value, 6);
        Assert.Equal(-99.84, setupP95.PercentChange!.Value, 6);
        Assert.Equal("lower", setupP95.Direction);
        Assert.Equal(120UL, result.Samples.BaselineVideoProcessorFrameCount);
        Assert.Equal(120UL, result.Samples.CandidateVideoProcessorFrameCount);
        Assert.DoesNotContain("accept", result.Result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compare_Reports_Preserves_Mixed_Per_Phase_Directions()
    {
        var baseline = CreateReport("baseline", "base-revision", 12.5, 1.0);
        var candidate = CreateReport("candidate", "candidate-revision", 0.02, 2.0);

        var result = PlaybackQualityRenderPhaseComparator.Compare(baseline, candidate, "case-a", 1);

        Assert.Equal("mixed", result.Result);
        Assert.Contains(result.Metrics, metric =>
            metric.Signal == "timing.videoProcessorSetupCpuDurationMsP95" &&
            metric.Direction == "lower");
        Assert.Contains(result.Metrics, metric =>
            metric.Signal == "timing.videoProcessorBltCpuDurationMsP95" &&
            metric.Direction == "higher");
    }

    [Fact]
    public void Compare_Reports_Rejects_Mismatched_Opened_Source_Identity()
    {
        var baseline = CreateReport("baseline", "base-revision", 12.5, 1.0);
        var candidate = CreateReport("candidate", "candidate-revision", 0.02, 0.5);
        candidate.Execution.OpenedSourceHash = "sha256:" + new string('b', 64);

        var result = PlaybackQualityRenderPhaseComparator.Compare(baseline, candidate, "case-a", 1);

        Assert.Equal("insufficient-evidence", result.Status);
        Assert.Contains("execution.openedSourceHash.mismatch", result.Blockers);
        Assert.Empty(result.Metrics);
    }

    [Fact]
    public void Compare_Reports_Rejects_Matching_But_Forged_Opened_Source_Identity()
    {
        var baseline = CreateReport("baseline", "base-revision", 12.5, 1.0);
        var candidate = CreateReport("candidate", "candidate-revision", 0.02, 0.5);
        var forgedHash = "sha256:" + new string('c', 64);
        baseline.Execution.OpenedSourceHash = forgedHash;
        candidate.Execution.OpenedSourceHash = forgedHash;

        var result = PlaybackQualityRenderPhaseComparator.Compare(baseline, candidate, "case-a", 1);

        Assert.Equal("insufficient-evidence", result.Status);
        Assert.Contains("execution.openedSourceHash.invalid", result.Blockers);
    }

    [Fact]
    public void Compare_Reports_Rejects_Same_Build_Identity()
    {
        var baseline = CreateReport("baseline", "same-revision", 12.5, 1.0);
        var candidate = CreateReport("candidate", "same-revision", 0.02, 0.5);

        var result = PlaybackQualityRenderPhaseComparator.Compare(baseline, candidate, "case-a", 1);

        Assert.Equal("insufficient-evidence", result.Status);
        Assert.Contains("environment.buildIdentity.same", result.Blockers);
    }

    [Fact]
    public void Compare_Reports_Rejects_Different_Color_Expectation()
    {
        var baseline = CreateReport("baseline", "base-revision", 12.5, 1.0);
        var candidate = CreateReport("candidate", "candidate-revision", 0.02, 0.5);
        candidate.ColorPipeline.ExpectationProfile = "sdr-display-fallback";

        var result = PlaybackQualityRenderPhaseComparator.Compare(baseline, candidate, "case-a", 1);

        Assert.Equal("insufficient-evidence", result.Status);
        Assert.Contains("colorPipeline.expectationProfile.mismatch", result.Blockers);
    }

    [Fact]
    public void Compare_Reports_Rejects_Too_Few_Processor_Samples()
    {
        var baseline = CreateReport("baseline", "base-revision", 12.5, 1.0, sampleCount: 29);
        var candidate = CreateReport("candidate", "candidate-revision", 0.02, 0.5, sampleCount: 29);

        var result = PlaybackQualityRenderPhaseComparator.Compare(baseline, candidate, "case-a", 1);

        Assert.Equal("insufficient-evidence", result.Status);
        Assert.Contains("timing.videoProcessorSetupCpuSampleCount.insufficient", result.Blockers);
        Assert.Equal(30UL, result.Samples.MinimumRequiredProcessorSamples);
    }

    [Fact]
    public void Compare_Reports_Records_Transport_Context_Without_Blocking_Phase_Diagnostics()
    {
        var baseline = CreateReport("baseline", "base-revision", 12.5, 1.0);
        var candidate = CreateReport("candidate", "candidate-revision", 0.02, 0.5);
        baseline.Buffers.PlaybackTransportReadWaitMs = 20;
        candidate.Buffers.PlaybackTransportReadWaitMs = 25_000;

        var result = PlaybackQualityRenderPhaseComparator.Compare(baseline, candidate, "case-a", 1);

        Assert.Equal("comparable", result.Status);
        Assert.Equal(20, result.Transport.BaselineReadWaitMs);
        Assert.Equal(25_000, result.Transport.CandidateReadWaitMs);
        Assert.Contains(result.Limitations, limitation =>
            limitation.Contains("transport", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compare_Reports_Rejects_Different_Build_Configuration_Or_Collector()
    {
        var baseline = CreateReport("baseline", "base-revision", 12.5, 1.0);
        var candidate = CreateReport("candidate", "candidate-revision", 0.02, 0.5);
        candidate.Environment.BuildConfiguration = "Debug-x64";
        candidate.Environment.CollectorVersion = "collector-v2";

        var result = PlaybackQualityRenderPhaseComparator.Compare(baseline, candidate, "case-a", 1);

        Assert.Equal("insufficient-evidence", result.Status);
        Assert.Contains("environment.buildConfiguration.mismatch", result.Blockers);
        Assert.Contains("environment.collectorVersion.mismatch", result.Blockers);
    }

    [Fact]
    public void Compare_Reports_Rejects_Different_Runner_Or_Incomplete_Native_Execution()
    {
        var baseline = CreateReport("baseline", "base-revision", 12.5, 1.0);
        var candidate = CreateReport("candidate", "candidate-revision", 0.02, 0.5);
        candidate.Execution.Runner = "app-hosted";
        candidate.Execution.PlaybackSampleObserved = false;

        var result = PlaybackQualityRenderPhaseComparator.Compare(baseline, candidate, "case-a", 1);

        Assert.Equal("insufficient-evidence", result.Status);
        Assert.Contains("execution.runner.mismatch", result.Blockers);
        Assert.Contains("execution.nativePlayback.incomplete", result.Blockers);
    }

    [Fact]
    public void Comparison_Serializes_With_Source_Generated_Contract()
    {
        var baseline = CreateReport("baseline", "base-revision", 12.5, 1.0);
        var candidate = CreateReport("candidate", "candidate-revision", 0.02, 0.5);
        var result = PlaybackQualityRenderPhaseComparator.Compare(baseline, candidate, "case-a", 1);

        var json = PlaybackQualityReportSerializer.Serialize(result);

        Assert.Contains("\"comparisonScope\": \"video-render-phases\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"decisionAuthority\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("timing.videoProcessorSetupCpuDurationMsP95", json, StringComparison.Ordinal);
    }

    private static PlaybackQualityReport CreateReport(
        string runId,
        string sourceRevision,
        double setupP95,
        double bltP95,
        ulong sampleCount = 120)
    {
        var report = new PlaybackQualityReport
        {
            RunId = runId,
            Environment = new PlaybackQualityEnvironment
            {
                CollectorVersion = "collector-v1",
                PlayerCoreVersion = "core-" + sourceRevision,
                SourceRevision = sourceRevision,
                BuildConfiguration = "Release-x64"
            },
            Execution = new PlaybackQualityExecutionEvidence
            {
                Runner = "native-headless",
                EvidenceLevel = PlaybackQualityEvidenceLevel.NativePlayback,
                Status = PlaybackQualityExecutionStatus.Completed,
                SourceOpened = true,
                NativeGraphOpened = true,
                DemuxStarted = true,
                DecoderOpened = true,
                PlaybackSampleObserved = true,
                OpenedSourceHash = "sha256:" + new string('a', 64),
                OpenedSourceHashKind = PlaybackQualitySourceFingerprint.OpenedMediaSignatureKind
            }
        };
        report.ColorPipeline.ExpectationProfile = "primary";
        report.Timing.VideoRenderVideoProcessorFrameCount = sampleCount;
        report.Timing.VideoRenderPostProcessFrameCount = sampleCount;
        report.Timing.VideoRenderDurationMsP50 = 1.0;
        report.Timing.VideoRenderDurationMsP95 = 2.0;
        report.Timing.VideoRenderDurationMsP99 = 3.0;
        report.Timing.VideoRenderDurationMsMax = 4.0;
        SetPhase(
            sampleCount,
            setupP95 / 2,
            setupP95,
            setupP95 * 1.2,
            setupP95 * 1.5,
            value => report.Timing.VideoProcessorSetupCpuSampleCount = value,
            (p50, p95, p99, max) =>
            {
                report.Timing.VideoProcessorSetupCpuDurationMsP50 = p50;
                report.Timing.VideoProcessorSetupCpuDurationMsP95 = p95;
                report.Timing.VideoProcessorSetupCpuDurationMsP99 = p99;
                report.Timing.VideoProcessorSetupCpuDurationMsMax = max;
            });
        SetPhase(sampleCount, 0.1, 0.2, 0.3, 0.4,
            value => report.Timing.VideoProcessorViewTargetCpuSampleCount = value,
            (p50, p95, p99, max) =>
            {
                report.Timing.VideoProcessorViewTargetCpuDurationMsP50 = p50;
                report.Timing.VideoProcessorViewTargetCpuDurationMsP95 = p95;
                report.Timing.VideoProcessorViewTargetCpuDurationMsP99 = p99;
                report.Timing.VideoProcessorViewTargetCpuDurationMsMax = max;
            });
        SetPhase(sampleCount, 0.1, 0.2, 0.3, 0.4,
            value => report.Timing.VideoProcessorClearCpuSampleCount = value,
            (p50, p95, p99, max) =>
            {
                report.Timing.VideoProcessorClearCpuDurationMsP50 = p50;
                report.Timing.VideoProcessorClearCpuDurationMsP95 = p95;
                report.Timing.VideoProcessorClearCpuDurationMsP99 = p99;
                report.Timing.VideoProcessorClearCpuDurationMsMax = max;
            });
        SetPhase(sampleCount, bltP95 / 2, bltP95, bltP95 * 1.2, bltP95 * 1.5,
            value => report.Timing.VideoProcessorBltCpuSampleCount = value,
            (p50, p95, p99, max) =>
            {
                report.Timing.VideoProcessorBltCpuDurationMsP50 = p50;
                report.Timing.VideoProcessorBltCpuDurationMsP95 = p95;
                report.Timing.VideoProcessorBltCpuDurationMsP99 = p99;
                report.Timing.VideoProcessorBltCpuDurationMsMax = max;
            });
        SetPhase(sampleCount, 0.1, 0.2, 0.3, 0.4,
            value => report.Timing.VideoProcessorPostProcessCpuSampleCount = value,
            (p50, p95, p99, max) =>
            {
                report.Timing.VideoProcessorPostProcessCpuDurationMsP50 = p50;
                report.Timing.VideoProcessorPostProcessCpuDurationMsP95 = p95;
                report.Timing.VideoProcessorPostProcessCpuDurationMsP99 = p99;
                report.Timing.VideoProcessorPostProcessCpuDurationMsMax = max;
            });
        report.Execution.OpenedSourceHash =
            PlaybackQualitySourceFingerprint.ComputeOpenedMediaSignature(report);
        return report;
    }

    private static void SetPhase(
        ulong sampleCount,
        double p50,
        double p95,
        double p99,
        double max,
        Action<ulong> setCount,
        Action<double, double, double, double> setValues)
    {
        setCount(sampleCount);
        setValues(p50, p95, p99, max);
    }
}
