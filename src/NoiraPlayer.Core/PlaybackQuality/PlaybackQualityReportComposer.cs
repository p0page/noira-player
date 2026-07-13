using System;
using NoiraPlayer.Core.Playback;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityReportRequest
    {
        public string RunId { get; set; } = "";

        public PlaybackQualityCaseMetadata? CaseMetadata { get; set; }

        public PlaybackDescriptor? Descriptor { get; set; }

        public PlaybackDisplayStatus? DisplayStatus { get; set; }

        public PlaybackQualityMetricsSnapshot? Metrics { get; set; }

        public PlaybackQualityRuntimeMetrics? RuntimeMetrics { get; set; }

        public PlaybackQualityPosition? Position { get; set; }

        public PlaybackQualitySourceTimeline? SourceTimeline { get; set; }

        public PlaybackQualityStartup? Startup { get; set; }

        public PlaybackQualityLifecycle? Lifecycle { get; set; }

        public PlaybackQualityInteractionEvidence? Interaction { get; set; }

        public PlaybackQualityEnvironment? Environment { get; set; }

        public PlaybackQualityExecutionEvidence? Execution { get; set; }

        public PlaybackQualityExpected? Expected { get; set; }

        public bool ForceSdrOutput { get; set; }

        public bool UseDefaultExpectedWhenMissing { get; set; }
    }

    public sealed class PlaybackQualityCaseMetadata
    {
        public string CaseId { get; set; } = "";

        public string Category { get; set; } = "stable";

        public string Severity { get; set; } = "medium";

        public string Stability { get; set; } = "stable";
    }

    public sealed class PlaybackQualityRunResult
    {
        public const string CurrentEvaluationVersion = "playback-quality-v0.12";

        public PlaybackQualityRunResult(
            PlaybackQualityReport report,
            PlaybackQualityModelAnalysis modelAnalysis,
            PlaybackQualityCaseMetadata? caseMetadata = null)
        {
            Report = report ?? throw new ArgumentNullException(nameof(report));
            ModelAnalysis = modelAnalysis ?? throw new ArgumentNullException(nameof(modelAnalysis));
            CaseMetadata = caseMetadata ?? new PlaybackQualityCaseMetadata
            {
                CaseId = report.RunId
            };
        }

        public int SchemaVersion { get; set; } = 1;

        public string EvaluationVersion { get; set; } = CurrentEvaluationVersion;

        public PlaybackQualityCaseMetadata CaseMetadata { get; }

        public PlaybackQualityReport Report { get; }

        public PlaybackQualityModelAnalysis ModelAnalysis { get; }
    }

    public static class PlaybackQualityReportComposer
    {
        private const string CollectorVersionEnvironmentVariable =
            "NOIRAPLAYER_PLAYBACK_QUALITY_COLLECTOR_VERSION";
        private const string PlayerCoreVersionEnvironmentVariable =
            "NOIRAPLAYER_PLAYER_CORE_VERSION";
        private const string SourceRevisionEnvironmentVariable =
            "NOIRAPLAYER_SOURCE_REVISION";
        private const string BuildConfigurationEnvironmentVariable =
            "NOIRAPLAYER_BUILD_CONFIGURATION";

        public static PlaybackQualityRunResult Compose(PlaybackQualityReportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var expected = request.Expected ??
                (request.UseDefaultExpectedWhenMissing && request.Descriptor != null
                    ? PlaybackQualityExpectedFactory.CreateDefault(request.Descriptor)
                    : null);
            var report = new PlaybackQualityReport
            {
                RunId = request.RunId ?? "",
                Expected = expected,
                Execution = PlaybackQualityExecutionEvidenceFactory.Clone(request.Execution)
            };

            if (request.Descriptor != null)
            {
                PlaybackQualityReportMapper.ApplySource(report, request.Descriptor);
            }

            if (request.SourceTimeline != null)
            {
                report.Source.ContainerStartTimeTicks = request.SourceTimeline.ContainerStartTimeTicks;
                report.Source.VideoStreamStartTimeTicks = request.SourceTimeline.VideoStreamStartTimeTicks;
            }

            if (request.DisplayStatus != null)
            {
                PlaybackQualityReportMapper.ApplyDisplayStatus(report, request.DisplayStatus);
            }

            if (request.Metrics != null)
            {
                PlaybackQualityReportMapper.ApplyMetrics(report, request.Metrics);
            }

            report.ColorPipeline.ForceSdrOutput = request.ForceSdrOutput;

            if (request.RuntimeMetrics != null)
            {
                report.RuntimeMetrics = CloneRuntimeMetrics(request.RuntimeMetrics);
            }
            else if (request.Metrics != null)
            {
                report.RuntimeMetrics =
                    PlaybackQualityRuntimeMetricsFactory.FromSnapshot(
                        request.Metrics,
                        "not-applicable");
            }

            if (request.Position != null)
            {
                report.Position = ClonePosition(request.Position);
            }

            if (request.Startup != null)
            {
                report.Startup = request.Startup;
            }

            if (request.Lifecycle != null)
            {
                report.Lifecycle = CloneLifecycle(request.Lifecycle);
            }

            if (request.Interaction != null)
            {
                report.Interaction = new PlaybackQualityInteractionEvidence
                {
                    Scenario = request.Interaction.Scenario,
                    Attempted = request.Interaction.Attempted,
                    OperationDurationMs = request.Interaction.OperationDurationMs,
                    LockWaitDurationMs = request.Interaction.LockWaitDurationMs,
                    ExecutionDurationMs = request.Interaction.ExecutionDurationMs,
                    QuiesceDurationMs = request.Interaction.QuiesceDurationMs,
                    SeekDurationMs = request.Interaction.SeekDurationMs,
                    DecoderOpenDurationMs = request.Interaction.DecoderOpenDurationMs,
                    RendererOpenDurationMs = request.Interaction.RendererOpenDurationMs,
                    PacketCacheHit = request.Interaction.PacketCacheHit,
                    PacketCacheEnabled = request.Interaction.PacketCacheEnabled,
                    PacketCachePacketCount = request.Interaction.PacketCachePacketCount,
                    PacketCacheBytes = request.Interaction.PacketCacheBytes,
                    PacketCacheWindowDurationTicks = request.Interaction.PacketCacheWindowDurationTicks,
                    RecoveryDurationMs = request.Interaction.RecoveryDurationMs,
                    CueRenderDurationMs = request.Interaction.CueRenderDurationMs,
                    PositionDeltaTicks = request.Interaction.PositionDeltaTicks,
                    SubmittedAudioFrameDelta = request.Interaction.SubmittedAudioFrameDelta,
                    RenderedVideoFrameDelta = request.Interaction.RenderedVideoFrameDelta,
                    SubtitleCueRenderCountDelta = request.Interaction.SubtitleCueRenderCountDelta
                };
            }

            if (request.Environment != null)
            {
                report.Environment = MergeEnvironment(request.Environment);
            }
            else
            {
                report.Environment = MergeEnvironment(null);
            }

            PlaybackQualityEvaluator.Evaluate(report);
            return new PlaybackQualityRunResult(
                report,
                PlaybackQualityReportAnalyzer.Analyze(report),
                CloneCaseMetadata(request.CaseMetadata, report.RunId));
        }

        private static PlaybackQualityCaseMetadata CloneCaseMetadata(
            PlaybackQualityCaseMetadata? source,
            string runId)
        {
            if (source == null)
            {
                return new PlaybackQualityCaseMetadata
                {
                    CaseId = runId ?? ""
                };
            }

            return new PlaybackQualityCaseMetadata
            {
                CaseId = string.IsNullOrWhiteSpace(source.CaseId) ? runId ?? "" : source.CaseId,
                Category = string.IsNullOrWhiteSpace(source.Category) ? "stable" : source.Category,
                Severity = string.IsNullOrWhiteSpace(source.Severity) ? "medium" : source.Severity,
                Stability = string.IsNullOrWhiteSpace(source.Stability) ? "stable" : source.Stability
            };
        }

        private static PlaybackQualityRuntimeMetrics CloneRuntimeMetrics(
            PlaybackQualityRuntimeMetrics source)
        {
            return new PlaybackQualityRuntimeMetrics
            {
                Status = source.Status,
                ProviderStatus = source.ProviderStatus,
                Reason = source.Reason,
                HasSnapshot = source.HasSnapshot,
                HasPlaybackSample = source.HasPlaybackSample,
                ProcessWallClockMs = source.ProcessWallClockMs,
                ProcessCpuTimeMs = source.ProcessCpuTimeMs,
                ProcessCpuUtilizationRatio = source.ProcessCpuUtilizationRatio
            };
        }

        private static PlaybackQualityPosition ClonePosition(
            PlaybackQualityPosition source)
        {
            return new PlaybackQualityPosition
            {
                RequestedStartPositionTicks = source.RequestedStartPositionTicks,
                SeekTargetPositionTicks = source.SeekTargetPositionTicks,
                SeekDemuxTargetTicks = source.SeekDemuxTargetTicks,
                ActualPositionTicks = source.ActualPositionTicks,
                FirstPresentedPositionTicks = source.FirstPresentedPositionTicks,
                PostSeekPositionTicks = source.PostSeekPositionTicks,
                PostSeekAdvanced = source.PostSeekAdvanced,
                SeekResetRuntimeMetrics = source.SeekResetRuntimeMetrics,
                PreSeekRenderedVideoFrames = source.PreSeekRenderedVideoFrames,
                PreSeekDroppedVideoFrames = source.PreSeekDroppedVideoFrames,
                SeekPositionErrorMs = source.SeekPositionErrorMs,
                SeekOperationDurationMs = source.SeekOperationDurationMs,
                SeekRecoveryDurationMs = source.SeekRecoveryDurationMs,
                SeekPacketCacheEnabled = source.SeekPacketCacheEnabled,
                SeekPacketCacheHit = source.SeekPacketCacheHit,
                SeekPacketCachePacketCount = source.SeekPacketCachePacketCount,
                SeekPacketCacheBytes = source.SeekPacketCacheBytes,
                SeekPacketCacheWindowDurationTicks = source.SeekPacketCacheWindowDurationTicks,
                SeekFallbackReason = source.SeekFallbackReason
            };
        }

        private static PlaybackQualityLifecycle CloneLifecycle(
            PlaybackQualityLifecycle source)
        {
            var lifecycle = new PlaybackQualityLifecycle();
            foreach (var item in source.Events)
            {
                if (item == null)
                {
                    continue;
                }

                lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
                {
                    Operation = item.Operation,
                    Status = item.Status,
                    State = item.State,
                    PositionTicks = item.PositionTicks,
                    Message = item.Message
                });
            }

            return lifecycle;
        }

        private static PlaybackQualityEnvironment MergeEnvironment(
            PlaybackQualityEnvironment? requested)
        {
            return new PlaybackQualityEnvironment
            {
                CollectorVersion = ValueOrEnvironment(
                    requested == null ? "" : requested.CollectorVersion,
                    CollectorVersionEnvironmentVariable),
                PlayerCoreVersion = ValueOrEnvironment(
                    requested == null ? "" : requested.PlayerCoreVersion,
                    PlayerCoreVersionEnvironmentVariable),
                SourceRevision = ValueOrEnvironment(
                    requested == null ? "" : requested.SourceRevision,
                    SourceRevisionEnvironmentVariable),
                BuildConfiguration = ValueOrEnvironment(
                    requested == null ? "" : requested.BuildConfiguration,
                    BuildConfigurationEnvironmentVariable)
            };
        }

        private static string ValueOrEnvironment(string value, string environmentVariable)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return Environment.GetEnvironmentVariable(environmentVariable) ?? "";
        }
    }
}
