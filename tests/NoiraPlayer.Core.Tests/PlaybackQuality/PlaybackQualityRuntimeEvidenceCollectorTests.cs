using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Playback;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityRuntimeEvidenceCollectorTests
{
    [Fact]
    public void ComposeRunResult_Preserves_Explicit_Native_Execution_Evidence()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/execution-evidence",
            Uri = "https://example.invalid/execution-evidence.mp4"
        };
        referenceCase.Purpose.Add("sdr-smoke");
        var execution = CreateExecutionEvidence(
            referenceCase,
            PlaybackQualityEvidenceLevel.NativePlayback,
            PlaybackQualityExecutionStatus.Completed);

        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult(
            referenceCase,
            CreateDescriptor(),
            execution: execution);

        Assert.NotSame(execution, result.Report.Execution);
        Assert.Equal(execution.AttemptId, result.Report.Execution.AttemptId);
        Assert.Equal("native-headless", result.Report.Execution.Runner);
        Assert.Equal(PlaybackQualityEvidenceLevel.NativePlayback, result.Report.Execution.EvidenceLevel);
        Assert.True(result.Report.Execution.SourceOpenAttempted);
        Assert.True(result.Report.Execution.PlaybackSampleObserved);
    }

    [Fact]
    public void ComposeErrorAndSkipRunResult_Preserve_Explicit_Execution_Attempts()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/execution-outcomes",
            Uri = "https://example.invalid/execution-outcomes.mp4"
        };
        referenceCase.Purpose.Add("error-handling");
        var failedExecution = CreateExecutionEvidence(
            referenceCase,
            PlaybackQualityEvidenceLevel.NativePlayback,
            PlaybackQualityExecutionStatus.Failed);
        failedExecution.SourceOpened = false;
        failedExecution.NativeGraphOpened = false;
        failedExecution.DemuxStarted = false;
        failedExecution.DecoderOpened = false;
        failedExecution.PlaybackSampleObserved = false;
        failedExecution.OpenedSourceHash = "";

        var errorResult = PlaybackQualityRuntimeEvidenceCollector.ComposeErrorRunResult(
            referenceCase,
            new PlaybackQualityError
            {
                Code = "source.open.failed",
                Message = "Source open failed.",
                FailureClass = PlaybackQualityFailureClassification.EnvironmentIssue,
                FailureArea = "error-handling"
            },
            execution: failedExecution);
        var skippedExecution = CreateExecutionEvidence(
            referenceCase,
            PlaybackQualityEvidenceLevel.Orchestration,
            PlaybackQualityExecutionStatus.Skipped);
        skippedExecution.SourceOpenAttempted = false;
        skippedExecution.SourceOpened = false;
        skippedExecution.NativeGraphOpened = false;
        skippedExecution.DemuxStarted = false;
        skippedExecution.DecoderOpened = false;
        skippedExecution.PlaybackSampleObserved = false;
        skippedExecution.OpenedSourceHash = "";
        var skipResult = PlaybackQualityRuntimeEvidenceCollector.ComposeSkipRunResult(
            referenceCase,
            new PlaybackQualitySkip
            {
                Code = "runner.not-available",
                Reason = "Runner is unavailable.",
                FailureClass = PlaybackQualityFailureClassification.InsufficientInstrumentation,
                FailureArea = "evidence-collection"
            },
            execution: skippedExecution);

        Assert.Equal(PlaybackQualityExecutionStatus.Failed, errorResult.Report.Execution.Status);
        Assert.True(errorResult.Report.Execution.SourceOpenAttempted);
        Assert.Equal(PlaybackQualityExecutionStatus.Skipped, skipResult.Report.Execution.Status);
        Assert.Equal(PlaybackQualityEvidenceLevel.Orchestration, skipResult.Report.Execution.EvidenceLevel);
    }

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
                PlayerCoreVersion = "NoiraPlayer.Core.Tests",
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
                PlayerCoreVersion = "NoiraPlayer.Core.Tests",
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
        var startup = new PlaybackQualityStartup
        {
            CommandReceivedAt = "2026-07-07T00:00:00.000Z",
            PlaybackStartedAt = "2026-07-07T00:00:00.300Z",
            StartupDurationMs = 300
        };
        startup.Stages.Add(new PlaybackQualityStartupStage
        {
            Name = "native.open",
            DurationMs = 300
        });

        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult(
            referenceCase,
            descriptor,
            backend,
            backend,
            startup,
            new PlaybackQualityEnvironment
            {
                CollectorVersion = "runtime-evidence-test",
                PlayerCoreVersion = "NoiraPlayer.Core.Tests",
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
        var nativeOpen = Assert.Single(result.Report.Startup.Stages, stage => stage.Name == "native.open");
        Assert.Collection(
            nativeOpen.Components,
            component => Assert.Equal("ffmpeg.open-input", component.Name),
            component => Assert.Equal("ffmpeg.find-stream-info", component.Name),
            component => Assert.Equal("native.initialize-components", component.Name),
            component => Assert.Equal("native.startup-seek", component.Name),
            component => Assert.Equal("native.first-frame.demux-read", component.Name),
            component => Assert.Equal("native.first-frame.decode-control", component.Name),
            component => Assert.Equal("native.first-frame.present", component.Name),
            component => Assert.Equal("host.dispatch-overhead", component.Name));
        Assert.Contains(
            "startup.stage.native.open.component.ffmpeg.open-input.durationMs",
            result.ModelAnalysis.Startup.Signals);
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

    [Fact]
    public void ComposeRunResult_Preserves_AppHosted_Interaction_Evidence()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/app-audio-switch",
            ExecutionRequirement = new PlaybackQualityExecutionRequirement
            {
                Scenario = PlaybackQualityExecutionScenario.AudioSwitch
            },
            Expected = new PlaybackQualityExpected
            {
                MaxInteractionRecoveryDurationMs = 2000
            }
        };
        referenceCase.Purpose.Add("tracks");
        var interaction = new PlaybackQualityInteractionEvidence
        {
            Scenario = PlaybackQualityExecutionScenario.AudioSwitch,
            Attempted = true,
            OperationDurationMs = 40,
            LockWaitDurationMs = 0,
            ExecutionDurationMs = 38,
            QuiesceDurationMs = 0,
            SeekDurationMs = 0,
            DecoderOpenDurationMs = 0.1,
            RendererOpenDurationMs = 1,
            PacketCacheEnabled = true,
            PacketCacheHit = true,
            PacketCachePacketCount = 176,
            PacketCacheBytes = 180224,
            PacketCacheWindowDurationTicks = 18_660_000,
            RecoveryDurationMs = 150,
            PositionDeltaTicks = 2_000_000,
            SubmittedAudioFrameDelta = 7,
            RenderedVideoFrameDelta = 4,
            SubtitleCueRenderCountDelta = 0
        };

        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult(
            referenceCase,
            CreateDescriptor(),
            interaction: interaction);

        Assert.Equal(150, result.Report.Interaction.RecoveryDurationMs);
        Assert.True(result.Report.Interaction.PacketCacheHit);
        Assert.Equal(176UL, result.Report.Interaction.PacketCachePacketCount);
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

    private static PlaybackQualityExecutionEvidence CreateExecutionEvidence(
        PlaybackQualityReferenceCase referenceCase,
        string evidenceLevel,
        string status)
    {
        return new PlaybackQualityExecutionEvidence
        {
            AttemptId = "attempt-1",
            Runner = "native-headless",
            EvidenceLevel = evidenceLevel,
            Status = status,
            SourceLocatorHash = PlaybackQualitySourceFingerprint.Compute(referenceCase.Uri),
            OpenedSourceHash = PlaybackQualitySourceFingerprint.Compute(referenceCase.Uri),
            StartedAtUtc = "2026-07-11T00:00:00Z",
            DurationMs = 5000,
            SourceOpenAttempted = true,
            SourceOpened = true,
            NativeGraphOpened = true,
            DemuxStarted = true,
            DecoderOpened = true,
            PlaybackSampleObserved = true
        };
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
                NativeGraphOpenDurationMs = 280,
                FfmpegOpenInputDurationMs = 200,
                FfmpegStreamInfoDurationMs = 50,
                NativeStartupSeekDurationMs = 10,
                NativeFirstFrameDurationMs = 15,
                NativeFirstFrameDemuxReadDurationMs = 8,
                NativeFirstFramePresentDurationMs = 1,
                NativeFirstFrameDemuxPacketCount = 12,
                NativeFirstFrameDemuxBytes = 65_536,
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
