using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityRuntimeEvidenceCollectorTests
{
    [Fact]
    public void ComposeSkipRunResult_Reports_Structured_Skip_Without_Playback_Telemetry_Noise()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/subtitle-render-skip",
            Category = "challenge",
            Severity = "medium",
            Stability = "stable",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 1920,
                Height = 1080,
                FrameRate = 23.976,
                HdrKind = "Sdr",
                MaxStartupDurationMs = 2000,
                MinRenderedVideoFrames = 120
            }
        };
        referenceCase.Purpose.Add("subtitles");

        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeSkipRunResult(
            referenceCase,
            new PlaybackQualitySkip
            {
                Code = "capability.subtitle-render.not-supported",
                Reason = "Subtitle visual render verification is outside v0.1 software evidence.",
                Operation = "subtitle-render-validation",
                FailureClass = PlaybackQualityFailureClassification.UnsupportedByCurrentMvp,
                FailureArea = "evidence-collection",
                IsExpected = true,
                IsRetriable = false
            },
            new PlaybackQualityEnvironment
            {
                CollectorVersion = "runtime-skip-test",
                PlayerCoreVersion = "NextGenEmby.Core.Tests",
                SourceRevision = "test-revision",
                BuildConfiguration = "Debug"
            });

        Assert.Equal("local/subtitle-render-skip", result.CaseMetadata.CaseId);
        Assert.Equal("challenge", result.CaseMetadata.Category);
        Assert.Equal("skip", result.Report.Result);
        Assert.Equal("capability.subtitle-render.not-supported", result.Report.Skip.Code);
        Assert.Equal("Subtitle visual render verification is outside v0.1 software evidence.", result.Report.Skip.Reason);
        Assert.Equal("subtitle-render-validation", result.Report.Skip.Operation);
        Assert.Equal(PlaybackQualityFailureClassification.UnsupportedByCurrentMvp, result.Report.Skip.FailureClass);
        Assert.Equal("evidence-collection", result.Report.Skip.FailureArea);
        Assert.True(result.Report.Skip.IsExpected);
        Assert.False(result.Report.Skip.IsRetriable);
        Assert.Equal("evidence-collection", result.Report.Analysis.PrimaryFailureArea);
        Assert.Contains("skip.code", result.Report.Analysis.RelevantSignals);
        Assert.Contains("skip.reason", result.Report.Analysis.RelevantSignals);
        Assert.Contains("skip.failureClass", result.Report.Analysis.RelevantSignals);
        Assert.Contains("skip.failureArea", result.Report.Analysis.RelevantSignals);
        Assert.Contains(result.Report.Checks, check =>
            check.Name == "PlaybackQualitySkipped" &&
            check.Signal == "skip.reason" &&
            check.Status == "skip" &&
            check.FailureArea == "evidence-collection" &&
            check.FailureClass == PlaybackQualityFailureClassification.UnsupportedByCurrentMvp);

        Assert.Equal("skip", result.ModelAnalysis.Result);
        Assert.Equal("evidence-collection", result.ModelAnalysis.PrimaryFailureArea);
        Assert.Equal(PlaybackQualityFailureClassification.UnsupportedByCurrentMvp, result.ModelAnalysis.PrimaryFailureClass);
        Assert.Equal("capability.subtitle-render.not-supported", result.ModelAnalysis.Skip.Code);
        Assert.Equal("evidence-collection", result.ModelAnalysis.Skip.FailureArea);
        Assert.Contains(PlaybackQualityFailureClassification.UnsupportedByCurrentMvp, result.ModelAnalysis.FailureClasses);
        Assert.Contains("evidence-collection", result.ModelAnalysis.FailureAreas);
        Assert.Contains("skip.code", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("skip.reason", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("skip.operation", result.ModelAnalysis.EvidenceSignals);
        Assert.DoesNotContain("source.codec", result.ModelAnalysis.MissingEvidence);
        Assert.DoesNotContain("timing.renderedVideoFrames", result.ModelAnalysis.MissingEvidence);
        Assert.DoesNotContain("startup.startupDurationMs", result.ModelAnalysis.MissingEvidence);
        Assert.Contains("explicitly skipped", result.ModelAnalysis.ExpectedBehavior);
        Assert.Contains("capability.subtitle-render.not-supported", result.ModelAnalysis.ActualBehavior);
    }

    [Fact]
    public void ComposeErrorRunResult_Reports_Runtime_Error_Without_Playback_Telemetry_Noise()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/missing-file",
            Category = "stable",
            Severity = "high",
            Stability = "stable",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 23.976,
                HdrKind = "Hdr10",
                HdrOutput = "Hdr10",
                MaxStartupDurationMs = 2000,
                MinRenderedVideoFrames = 120
            }
        };
        referenceCase.Purpose.Add("error-handling");

        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeErrorRunResult(
            referenceCase,
            new PlaybackQualityError
            {
                Code = "source.open.missing-file",
                Message = "The media file was not found.",
                Operation = "open",
                ExceptionType = "FileNotFoundException",
                FailureClass = "sample issue",
                FailureArea = "error-handling",
                IsTerminal = true,
                IsRetriable = false
            },
            new PlaybackQualityEnvironment
            {
                CollectorVersion = "runtime-error-test",
                PlayerCoreVersion = "NextGenEmby.Core.Tests",
                SourceRevision = "test-revision",
                BuildConfiguration = "Debug"
            });

        Assert.Equal("local/missing-file", result.CaseMetadata.CaseId);
        Assert.Equal("error", result.Report.Result);
        Assert.Equal("source.open.missing-file", result.Report.Error.Code);
        Assert.Equal("The media file was not found.", result.Report.Error.Message);
        Assert.Equal("open", result.Report.Error.Operation);
        Assert.Equal("FileNotFoundException", result.Report.Error.ExceptionType);
        Assert.Equal("sample issue", result.Report.Error.FailureClass);
        Assert.Equal("error-handling", result.Report.Error.FailureArea);
        Assert.True(result.Report.Error.IsTerminal);
        Assert.False(result.Report.Error.IsRetriable);
        Assert.Equal("error-handling", result.Report.Analysis.PrimaryFailureArea);
        Assert.Contains("error.code", result.Report.Analysis.RelevantSignals);
        Assert.Contains(result.Report.Checks, check =>
            check.Name == "PlaybackRuntimeError" &&
            check.Signal == "error.code" &&
            check.Status == "fail" &&
            check.FailureArea == "error-handling" &&
            check.FailureClass == "sample issue");

        Assert.Equal("error", result.ModelAnalysis.Result);
        Assert.Equal("error-handling", result.ModelAnalysis.PrimaryFailureArea);
        Assert.Equal("source.open.missing-file", result.ModelAnalysis.Error.Code);
        Assert.Equal("sample issue", result.ModelAnalysis.Error.FailureClass);
        Assert.Equal("error-handling", result.ModelAnalysis.Error.FailureArea);
        Assert.Contains("sample issue", result.ModelAnalysis.FailureClasses);
        Assert.Contains("error-handling", result.ModelAnalysis.FailureAreas);
        Assert.Contains("error.code", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("error.message", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("error.operation", result.ModelAnalysis.EvidenceSignals);
        Assert.DoesNotContain("source.codec", result.ModelAnalysis.MissingEvidence);
        Assert.DoesNotContain("timing.renderedVideoFrames", result.ModelAnalysis.MissingEvidence);
        Assert.DoesNotContain("startup.startupDurationMs", result.ModelAnalysis.MissingEvidence);
        Assert.Contains(result.ModelAnalysis.InvestigationHints, hint =>
            hint.FailureArea == "error-handling" &&
            hint.Signals.Contains("error.code"));
    }

    [Fact]
    public void ComposeRunResult_Uses_Backend_Display_And_Metrics_Evidence()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/runtime-evidence-hdr10",
            Category = "stable",
            Severity = "high",
            Stability = "stable",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 23.976,
                HdrKind = "Hdr10",
                HdrOutput = "Hdr10",
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
                MaxStartupDurationMs = 1500,
                MinRenderedVideoFrames = 120,
                MaxDroppedFrames = 1,
                MaxFrameGapMs = 105,
                MaxRenderIntervalMsP95 = 55,
                MaxRenderIntervalMsP99 = 80,
                MaxAudioVideoDriftMsP95 = 40,
                MaxVideoStarvedPasses = 0,
                MaxAudioStarvedPasses = 0,
                RequireValidatedConversion = true,
                RequireMatchedDisplayRefreshRate = true
            }
        };
        referenceCase.Purpose.Add("hdr-output");
        referenceCase.Purpose.Add("frame-pacing");
        referenceCase.Purpose.Add("av-sync");
        referenceCase.Purpose.Add("buffering");

        var descriptor = CreateDescriptor();
        var backend = new RuntimeEvidenceBackend();

        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult(
            referenceCase,
            descriptor,
            backend,
            backend,
            new PlaybackQualityStartup
            {
                CommandReceivedAt = "2026-07-07T00:00:00.000Z",
                PlaybackStartedAt = "2026-07-07T00:00:00.300Z",
                StartupDurationMs = 300
            },
            new PlaybackQualityEnvironment
            {
                CollectorVersion = "runtime-evidence-test",
                PlayerCoreVersion = "NextGenEmby.Core.Tests",
                SourceRevision = "test-revision",
                BuildConfiguration = "Debug"
            });

        Assert.Equal("pass", result.Report.Result);
        Assert.Equal("Hdr10", result.Report.ColorPipeline.ActualHdrOutput);
        Assert.Equal("RGB_FULL_G2084_NONE_P2020", result.Report.ColorPipeline.SwapChainColorSpace);
        Assert.Equal("validated", result.Report.ColorPipeline.ConversionStatus);
        Assert.Equal(59.94006, result.Report.Display.RefreshRateHz);
        Assert.Equal(240UL, result.Report.Timing.RenderedVideoFrames);
        Assert.Equal(40.0, result.Report.Timing.MaxFrameGapMs);
        Assert.Equal(1_200_000_000, result.Report.Position.ActualPositionTicks);
        Assert.Equal(1_199_700_000, result.Report.Sync.AudioClockTicks);
        Assert.Equal(12.0, result.Report.Sync.AudioVideoDriftMsP95);
        Assert.Equal(0UL, result.Report.Buffers.VideoStarvedPasses);
        Assert.Equal(0UL, result.Report.Buffers.AudioStarvedPasses);
        Assert.Equal("pass", result.ModelAnalysis.Result);
        Assert.Equal("native-winrt:returned-snapshot", result.Report.RuntimeMetrics.ProviderStatus);
        Assert.Equal("native-winrt:returned-snapshot", result.ModelAnalysis.RuntimeMetrics.ProviderStatus);
        Assert.Contains("timing.renderedVideoFrames", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("sync.audioVideoDriftMsP95", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("buffers.videoStarvedPasses", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("colorPipeline.actualHdrOutput", result.ModelAnalysis.EvidenceSignals);
    }

    [Fact]
    public void ComposeRunResult_Reports_Runtime_Metrics_Unavailable_When_Provider_Returns_False()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/runtime-metrics-unavailable",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 23.976,
                HdrKind = "Hdr10",
                MinRenderedVideoFrames = 120
            }
        };
        referenceCase.Purpose.Add("frame-pacing");

        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult(
            referenceCase,
            CreateDescriptor(),
            metricsProvider: new NoRuntimeMetricsProvider());

        Assert.Equal("unavailable", result.Report.RuntimeMetrics.Status);
        Assert.Equal("returned-false", result.Report.RuntimeMetrics.ProviderStatus);
        Assert.False(result.Report.RuntimeMetrics.HasSnapshot);
        Assert.False(result.Report.RuntimeMetrics.HasPlaybackSample);
        Assert.Equal("unavailable", result.ModelAnalysis.RuntimeMetrics.Status);
        Assert.Equal("returned-false", result.ModelAnalysis.RuntimeMetrics.ProviderStatus);
        Assert.Contains("runtimeMetrics.status", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("runtimeMetrics.reason", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("timing.renderedVideoFrames", result.ModelAnalysis.MissingEvidence);
    }

    [Fact]
    public void ComposeRunResult_Includes_Captured_Lifecycle_Evidence()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/lifecycle-capture",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 23.976,
                HdrKind = "Hdr10",
                MinRenderedVideoFrames = 120
            }
        };
        referenceCase.Purpose.Add("timeline");

        var lifecycle = new PlaybackQualityLifecycle();
        lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "load",
            Status = "success",
            State = "Playing",
            PositionTicks = 0
        });
        lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "play",
            Status = "success",
            State = "Playing",
            PositionTicks = 120_000_000
        });

        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult(
            referenceCase,
            CreateDescriptor(),
            lifecycle: lifecycle);

        Assert.Contains(result.Report.Lifecycle.Events, e =>
            e.Operation == "load" &&
            e.Status == "success" &&
            e.State == "Playing");
        Assert.Contains(result.Report.Lifecycle.Events, e =>
            e.Operation == "play" &&
            e.PositionTicks == 120_000_000);
        Assert.Contains("lifecycle.load", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("lifecycle.play", result.ModelAnalysis.EvidenceSignals);
    }

    [Fact]
    public void ComposeRunResult_Includes_Captured_Position_Evidence()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/position-capture",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 23.976,
                HdrKind = "Hdr10",
                MaxSeekPositionErrorMs = 500
            }
        };
        referenceCase.Purpose.Add("timeline");

        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult(
            referenceCase,
            CreateDescriptor(),
            position: new PlaybackQualityPosition
            {
                RequestedStartPositionTicks = 0,
                SeekTargetPositionTicks = 500_000_000,
                ActualPositionTicks = 502_000_000
            });

        Assert.Equal(500_000_000, result.Report.Position.SeekTargetPositionTicks);
        Assert.Equal(502_000_000, result.Report.Position.ActualPositionTicks);
        Assert.Equal(200, result.Report.Position.SeekPositionErrorMs);
        Assert.Contains("position.seekPositionErrorMs", result.ModelAnalysis.EvidenceSignals);
        Assert.DoesNotContain("position.seekPositionErrorMs", result.ModelAnalysis.MissingEvidence);
    }

    private static PlaybackDescriptor CreateDescriptor()
    {
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            DirectStreamUrl = "https://example.invalid/hdr10.mp4",
            Width = 3840,
            Height = 2160,
            VideoFrameRate = 23.976,
            HdrProfile = new HdrPlaybackProfile
            {
                Kind = HdrPlaybackKind.Hdr10,
                Codec = "hevc"
            }
        };
        source.Streams.Add(new EmbyMediaStream
        {
            Index = 0,
            Kind = EmbyStreamKind.Video,
            Codec = "hevc",
            RealFrameRate = 23.976,
            AverageFrameRate = 23.976
        });
        source.Streams.Add(new EmbyMediaStream
        {
            Index = 1,
            Kind = EmbyStreamKind.Audio,
            Codec = "eac3",
            Language = "eng",
            ChannelLayout = "5.1"
        });

        return new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 0,
            audioStreamIndex: 1);
    }

    private sealed class RuntimeEvidenceBackend :
        IPlaybackBackendDiagnostics,
        IPlaybackQualityMetricsProvider,
        IPlaybackQualityMetricsProviderIdentity
    {
        public string PlaybackQualityMetricsProviderId => "native-winrt";

        public PlaybackBackendCapabilities Capabilities { get; } =
            new PlaybackBackendCapabilities(
                PlaybackBackendFeature.DirectPlayHttp |
                PlaybackBackendFeature.Hevc |
                PlaybackBackendFeature.HevcMain10 |
                PlaybackBackendFeature.Hdr10);

        public PlaybackDisplayStatus DisplayStatus { get; } =
            new PlaybackDisplayStatus(
                HdrOutputStatus.On,
                isHdrDisplayAvailable: true,
                isHdrOutputActive: true,
                message: "HDR active",
                swapChainFormat: "R10G10B10A2_UNORM",
                swapChainColorSpace: "RGB_FULL_G2084_NONE_P2020",
                isTenBitSwapChain: true,
                isVideoProcessorColorSpaceValidated: true,
                videoProcessorInputColorSpace: "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                videoProcessorOutputColorSpace: "RGB_FULL_G2084_NONE_P2020",
                videoProcessorConversionStatus: "validated",
                refreshRateHz: 59.94006);

        public bool TryGetQualityMetrics(out PlaybackQualityMetricsSnapshot metrics)
        {
            metrics = new PlaybackQualityMetricsSnapshot
            {
                RenderPasses = 240,
                DecodedVideoFrames = 240,
                RenderedVideoFrames = 240,
                SubmittedAudioFrames = 240,
                DroppedVideoFrames = 1,
                SeekPrerollDroppedFrames = 0,
                VideoAheadWaitCount = 0,
                VideoStarvedPasses = 0,
                AudioStarvedPasses = 0,
                QueuedAudioBuffers = 4,
                AudioClockTicks = 1_199_700_000,
                VideoPositionTicks = 1_200_000_000,
                RenderIntervalMsP50 = 41.708,
                RenderIntervalMsP95 = 42.2,
                RenderIntervalMsP99 = 48.0,
                MaxFrameGapMs = 40.0,
                FramePacingSourceFrameRate = 23.976,
                LateFrameDropToleranceMs = 104.27,
                AudioVideoDriftMsP50 = 4.0,
                AudioVideoDriftMsP95 = 12.0,
                AudioVideoDriftMsP99 = 18.0,
                AudioVideoDriftMsMax = 24.0
            };
            return true;
        }
    }

    private sealed class NoRuntimeMetricsProvider : IPlaybackQualityMetricsProvider
    {
        public bool TryGetQualityMetrics(out PlaybackQualityMetricsSnapshot metrics)
        {
            metrics = new PlaybackQualityMetricsSnapshot();
            return false;
        }
    }
}
