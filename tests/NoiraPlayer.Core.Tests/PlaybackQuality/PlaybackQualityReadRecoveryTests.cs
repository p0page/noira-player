using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityReadRecoveryTests
{
    [Fact]
    public void Mapper_Copies_Demux_Read_Recovery_Metrics()
    {
        var report = new PlaybackQualityReport();
        var metrics = new PlaybackQualityMetricsSnapshot
        {
            ReadErrorCount = 3,
            ReadRetryCount = 2,
            ReadRecoveryCount = 1,
            MaxConsecutiveReadErrors = 2,
            LastReadErrorCode = -5,
            FatalReadErrorCode = 0,
            LastReadRecoveryDurationMs = 37.5
        };

        PlaybackQualityReportMapper.ApplyMetrics(report, metrics);

        Assert.Equal(3UL, report.ReadRecovery.ReadErrorCount);
        Assert.Equal(2UL, report.ReadRecovery.ReadRetryCount);
        Assert.Equal(1UL, report.ReadRecovery.ReadRecoveryCount);
        Assert.Equal(2U, report.ReadRecovery.MaxConsecutiveReadErrors);
        Assert.Equal(-5, report.ReadRecovery.LastReadErrorCode);
        Assert.Equal(0, report.ReadRecovery.FatalReadErrorCode);
        Assert.Equal(37.5, report.ReadRecovery.LastReadRecoveryDurationMs);
    }

    [Fact]
    public void Evaluate_Passes_When_Required_Read_Error_Is_Recovered_Within_Budget()
    {
        var report = CreateRequiredReport();

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("pass", report.Result);
        Assert.Contains(report.Checks, check =>
            check.Signal == "readRecovery.readRecoveryCount" &&
            check.Status == "pass" &&
            check.FailureArea == "buffering");
        Assert.Contains(report.Checks, check =>
            check.Signal == "readRecovery.fatalReadErrorCode" &&
            check.Status == "pass");
    }

    [Fact]
    public void Evaluate_Fails_As_Insufficient_Instrumentation_When_Read_Recovery_Object_Is_Missing()
    {
        var report = CreateRequiredReport();
        report.ReadRecovery = null!;

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(report.Checks, check =>
            check.Signal == "readRecovery" &&
            check.Status == "fail" &&
            check.FailureArea == "evidence-collection" &&
            check.FailureClass == PlaybackQualityFailureClassification.InsufficientInstrumentation);
    }

    [Theory]
    [InlineData(0, 1, 1, 0, "readRecovery.readErrorCount")]
    [InlineData(1, 1, 0, 0, "readRecovery.readRecoveryCount")]
    [InlineData(1, 11, 1, 0, "readRecovery.readRetryCount")]
    [InlineData(1, 1, 0, -5, "readRecovery.fatalReadErrorCode")]
    public void Evaluate_Fails_When_Required_Read_Recovery_Evidence_Is_Invalid(
        ulong errors,
        ulong retries,
        ulong recoveries,
        int fatalCode,
        string expectedSignal)
    {
        var report = CreateRequiredReport();
        report.ReadRecovery.ReadErrorCount = errors;
        report.ReadRecovery.ReadRetryCount = retries;
        report.ReadRecovery.ReadRecoveryCount = recoveries;
        report.ReadRecovery.FatalReadErrorCode = fatalCode;

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        Assert.Contains(report.Checks, check =>
            check.Signal == expectedSignal &&
            check.Status == "fail" &&
            check.FailureArea == "buffering");
    }

    [Fact]
    public void Required_Signal_Policy_Requires_All_Read_Recovery_Fields()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            Expected = new PlaybackQualityExpected
            {
                RequireValidatedConversion = false,
                ReadRecovery = new PlaybackQualityReadRecoveryExpected
                {
                    Required = true,
                    MinReadErrors = 1,
                    MinRecoveries = 1,
                    MaxRetries = 10
                }
            }
        };

        var signals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("readRecovery.readErrorCount", signals);
        Assert.Contains("readRecovery.readRetryCount", signals);
        Assert.Contains("readRecovery.readRecoveryCount", signals);
        Assert.Contains("readRecovery.maxConsecutiveReadErrors", signals);
        Assert.Contains("readRecovery.lastReadErrorCode", signals);
        Assert.Contains("readRecovery.fatalReadErrorCode", signals);
        Assert.Contains("readRecovery.lastReadRecoveryDurationMs", signals);
    }

    [Fact]
    public void Required_Signal_Policy_Does_Not_Accept_Default_Value_When_Json_Field_Is_Missing()
    {
        var report = CreateRequiredReport();
        var presentSignals = new[]
        {
            "readRecovery.readErrorCount",
            "readRecovery.readRetryCount"
        };

        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(
            report,
            "readRecovery.readErrorCount",
            presentSignals));
        Assert.False(PlaybackQualityRequiredSignalPolicy.HasReportSignal(
            report,
            "readRecovery.readRecoveryCount",
            presentSignals));
        Assert.True(PlaybackQualityRequiredSignalPolicy.RequiresNativePlaybackEvidence(
            "readRecovery.readRecoveryCount"));
    }

    [Fact]
    public void Manifest_Validation_Rejects_Invalid_Read_Recovery_Thresholds()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        manifest.Cases.Add(new PlaybackQualityReferenceCase
        {
            CaseId = "invalid-read-recovery",
            Uri = "https://example.invalid/video.mp4",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 23.976,
                HdrKind = "Sdr",
                ReadRecovery = new PlaybackQualityReadRecoveryExpected
                {
                    Required = true,
                    MinReadErrors = 0,
                    MinRecoveries = 2,
                    MaxRetries = 11
                }
            }
        });

        var validation = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.Contains(validation.Errors, error =>
            error.Code == "case.expected.readRecovery.minReadErrors.invalid");
        Assert.Contains(validation.Errors, error =>
            error.Code == "case.expected.readRecovery.minRecoveries.invalid");
        Assert.Contains(validation.Errors, error =>
            error.Code == "case.expected.readRecovery.maxRetries.invalid");
    }

    [Fact]
    public void Model_Analysis_Exposes_Demux_Read_Recovery_Evidence()
    {
        var report = CreateRequiredReport();
        PlaybackQualityEvaluator.Evaluate(report);

        var analysis = PlaybackQualityReportAnalyzer.Analyze(report, new[]
        {
            "readRecovery.readErrorCount",
            "readRecovery.readRetryCount",
            "readRecovery.readRecoveryCount",
            "readRecovery.maxConsecutiveReadErrors",
            "readRecovery.lastReadErrorCode",
            "readRecovery.fatalReadErrorCode",
            "readRecovery.lastReadRecoveryDurationMs"
        });

        Assert.Equal(1UL, analysis.Buffering.ReadErrorCount);
        Assert.Equal(1UL, analysis.Buffering.ReadRecoveryCount);
        Assert.Equal(-5, analysis.Buffering.LastReadErrorCode);
        Assert.Contains("readRecovery.readRecoveryCount", analysis.EvidenceSignals);
        Assert.Contains(PlaybackQualitySignalCatalog.ReportSignals, signal =>
            signal.Signal == "readRecovery.fatalReadErrorCode");
    }

    [Fact]
    public void Comparator_Reports_Resolved_Demux_Read_Recovery_Failure()
    {
        var baseline = CreateRequiredReport("baseline");
        baseline.ReadRecovery.ReadRecoveryCount = 0;
        baseline.ReadRecovery.FatalReadErrorCode = -5;
        PlaybackQualityEvaluator.Evaluate(baseline);

        var candidate = CreateRequiredReport("candidate");
        PlaybackQualityEvaluator.Evaluate(candidate);

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate);

        Assert.Equal("improved", comparison.Result);
        Assert.Contains("buffering", comparison.ResolvedFailureAreas);
        Assert.Contains(comparison.Improvements, delta =>
            delta.Signal == "readRecovery.readRecoveryCount" ||
            delta.Signal == "readRecovery.fatalReadErrorCode");
    }

    [Fact]
    public void Serializer_RoundTrips_Read_Recovery_Evidence_At_V09()
    {
        var report = CreateRequiredReport();

        var json = PlaybackQualityReportSerializer.Serialize(report);
        var parsed = PlaybackQualityReportSerializer.Deserialize(json);

        Assert.Equal("playback-quality-v0.18", PlaybackQualityRunResult.CurrentEvaluationVersion);
        Assert.Contains("\"readRecovery\"", json);
        Assert.Contains("\"minReadErrors\": 1", json);
        Assert.Contains("\"readRecoveryCount\": 1", json);
        Assert.Equal(1UL, parsed.ReadRecovery.ReadRecoveryCount);
        Assert.Equal(1UL, parsed.Expected!.ReadRecovery!.MinReadErrors);
    }

    private static PlaybackQualityReport CreateRequiredReport(string runId = "demux-read-recovery")
    {
        var report = new PlaybackQualityReport
        {
            RunId = runId,
            Environment = new PlaybackQualityEnvironment
            {
                PlayerCoreVersion = "core-" + runId,
                SourceRevision = "revision-shared",
                BuildConfiguration = "Debug"
            },
            Execution = new PlaybackQualityExecutionEvidence
            {
                AttemptId = "attempt-" + runId,
                Runner = "native-headless",
                Scenario = PlaybackQualityExecutionScenario.PauseResume,
                EvidenceLevel = PlaybackQualityEvidenceLevel.NativePlayback,
                Status = PlaybackQualityExecutionStatus.Completed,
                SourceLocatorHash = "sha256:" + new string('a', 64),
                OpenedSourceHash = "sha256:" + new string('b', 64),
                OpenedSourceHashKind = PlaybackQualitySourceFingerprint.OpenedMediaSignatureKind,
                StartedAtUtc = "2026-07-13T00:00:00.0000000+00:00",
                DurationMs = 30000,
                RequestedSampleDurationMs = 5000,
                SourceOpenAttempted = true,
                SourceOpened = true,
                NativeGraphOpened = true,
                DemuxStarted = true,
                DecoderOpened = true,
                PlaybackSampleObserved = true
            },
            Expected = new PlaybackQualityExpected
            {
                RequireValidatedConversion = false,
                ReadRecovery = new PlaybackQualityReadRecoveryExpected
                {
                    Required = true,
                    MinReadErrors = 1,
                    MinRecoveries = 1,
                    MaxRetries = 10
                }
            },
            Source = new PlaybackQualitySource
            {
                VideoMetadataProvider = "native-playback",
                VideoMetadataStatus = "observed"
            },
            ReadRecovery = new PlaybackQualityReadRecovery
            {
                ReadErrorCount = 1,
                ReadRetryCount = 1,
                ReadRecoveryCount = 1,
                MaxConsecutiveReadErrors = 1,
                LastReadErrorCode = -5,
                FatalReadErrorCode = 0,
                LastReadRecoveryDurationMs = 37.5
            }
        };

        report.Execution.OpenedSourceHash =
            PlaybackQualitySourceFingerprint.ComputeOpenedMediaSignature(report);

        return report;
    }
}
