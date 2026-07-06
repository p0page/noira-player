using System.Collections.Generic;

namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityModelAnalysis
    {
        public string RunId { get; set; } = "";
        public string Result { get; set; } = "";
        public string PrimaryFailureArea { get; set; } = "none";
        public string SuggestedNextAction { get; set; } = "";
        public PlaybackQualityStartupAssessment Startup { get; set; } = new PlaybackQualityStartupAssessment();
        public PlaybackQualitySourceAssessment Source { get; set; } = new PlaybackQualitySourceAssessment();
        public PlaybackQualityColorPipelineAssessment ColorPipeline { get; set; } = new PlaybackQualityColorPipelineAssessment();
        public PlaybackQualityBufferingAssessment Buffering { get; set; } = new PlaybackQualityBufferingAssessment();
        public PlaybackQualityAvSyncAssessment AvSync { get; set; } = new PlaybackQualityAvSyncAssessment();
        public PlaybackQualitySampleAssessment Sample { get; set; } = new PlaybackQualitySampleAssessment();
        public PlaybackQualityCadenceAssessment Cadence { get; set; } = new PlaybackQualityCadenceAssessment();
        public PlaybackQualityOptimizationGate OptimizationGate { get; set; } = new PlaybackQualityOptimizationGate();
        public PlaybackQualityFramePacingClassification FramePacing { get; set; } = new PlaybackQualityFramePacingClassification();
        public List<PlaybackQualityTriageStep> TriageSteps { get; } = new List<PlaybackQualityTriageStep>();
        public List<string> FailureReasons { get; } = new List<string>();
        public List<PlaybackQualityCheck> FailedChecks { get; } = new List<PlaybackQualityCheck>();
        public List<string> FailureAreas { get; } = new List<string>();
        public List<PlaybackQualityInvestigationHint> InvestigationHints { get; } = new List<PlaybackQualityInvestigationHint>();
        public List<string> EvidenceSignals { get; } = new List<string>();
        public List<string> MissingEvidence { get; } = new List<string>();
        public List<string> Limitations { get; } = new List<string>();
    }

    public sealed class PlaybackQualityStartupAssessment
    {
        public string Status { get; set; } = "unknown";
        public string Reason { get; set; } = "";
        public string CommandReceivedAt { get; set; } = "";
        public string PlaybackStartedAt { get; set; } = "";
        public double StartupDurationMs { get; set; }
        public List<string> Signals { get; } = new List<string>();
        public List<string> FailedSignals { get; } = new List<string>();
    }

    public sealed class PlaybackQualitySourceAssessment
    {
        public string Status { get; set; } = "unknown";
        public string Reason { get; set; } = "";
        public string Codec { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public double FrameRate { get; set; }
        public string HdrKind { get; set; } = "";
        public string HdrPlaybackStrategy { get; set; } = "";
        public bool IsHdr { get; set; }
        public bool IsDirectPlayable { get; set; }
        public bool IsDolbyVision { get; set; }
        public int? DolbyVisionProfile { get; set; }
        public int? DolbyVisionCompatibilityId { get; set; }
        public bool HasHdr10BaseLayer { get; set; }
        public bool HasHlgBaseLayer { get; set; }
        public List<string> Signals { get; } = new List<string>();
        public List<string> MismatchedSignals { get; } = new List<string>();
    }

    public sealed class PlaybackQualityColorPipelineAssessment
    {
        public string Status { get; set; } = "unknown";
        public string Reason { get; set; } = "";
        public string ActualHdrOutput { get; set; } = "";
        public string SwapChainFormat { get; set; } = "";
        public string SwapChainColorSpace { get; set; } = "";
        public bool IsTenBitSwapChain { get; set; }
        public string DxgiInput { get; set; } = "";
        public string DxgiOutput { get; set; } = "";
        public string ConversionStatus { get; set; } = "";
        public bool IsVideoProcessorColorSpaceValidated { get; set; }
        public bool ForceSdrOutput { get; set; }
        public List<string> Signals { get; } = new List<string>();
        public List<string> MismatchedSignals { get; } = new List<string>();
    }

    public sealed class PlaybackQualityBufferingAssessment
    {
        public string Status { get; set; } = "unknown";
        public string Reason { get; set; } = "";
        public ulong SubmittedAudioFrames { get; set; }
        public ulong QueuedAudioBuffers { get; set; }
        public ulong VideoStarvedPasses { get; set; }
        public ulong AudioStarvedPasses { get; set; }
        public List<string> Signals { get; } = new List<string>();
        public List<string> FailedSignals { get; } = new List<string>();
    }

    public sealed class PlaybackQualityAvSyncAssessment
    {
        public string Status { get; set; } = "unknown";
        public string Reason { get; set; } = "";
        public long AudioClockTicks { get; set; }
        public long VideoPositionTicks { get; set; }
        public double AudioVideoDriftMsP50 { get; set; }
        public double AudioVideoDriftMsP95 { get; set; }
        public double AudioVideoDriftMsP99 { get; set; }
        public double AudioVideoDriftMsMax { get; set; }
        public List<string> Signals { get; } = new List<string>();
        public List<string> FailedSignals { get; } = new List<string>();
    }

    public sealed class PlaybackQualityInvestigationHint
    {
        public string FailureArea { get; set; } = "";
        public string SuggestedAction { get; set; } = "";
        public List<string> CodeTargets { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
    }

    public sealed class PlaybackQualityTriageStep
    {
        public int Rank { get; set; }
        public string Kind { get; set; } = "";
        public string FailureArea { get; set; } = "";
        public string SuggestedAction { get; set; } = "";
        public List<string> Signals { get; } = new List<string>();
        public List<string> CodeTargets { get; } = new List<string>();
    }

    public sealed class PlaybackQualitySampleAssessment
    {
        public string Status { get; set; } = "unknown";
        public ulong RenderedVideoFrames { get; set; }
        public long? MinRenderedVideoFrames { get; set; }
        public long AdditionalRenderedFramesRequired { get; set; }
        public double ObservedSampleDurationMs { get; set; }
        public double MinimumSampleDurationMs { get; set; }
        public string Reason { get; set; } = "";
    }

    public sealed class PlaybackQualityCadenceAssessment
    {
        public string Status { get; set; } = "missing-evidence";
        public double SourceFrameRate { get; set; }
        public double DisplayRefreshRateHz { get; set; }
        public double BestMultiplier { get; set; }
        public double BestTargetRefreshRateHz { get; set; }
        public double RefreshDeltaHz { get; set; }
        public double ToleranceHz { get; set; }
        public double ClockSpeedMultiplier { get; set; } = 1.0;
        public double ClockSpeedAdjustmentPercent { get; set; }
        public bool IsClockSpeedAdjustmentRequired { get; set; }
        public string Reason { get; set; } = "";
        public List<string> Signals { get; } = new List<string>();
    }

    public sealed class PlaybackQualityOptimizationGate
    {
        public string Status { get; set; } = "not-needed";
        public bool CanOptimizePlaybackCore { get; set; }
        public List<string> Blockers { get; } = new List<string>();
        public List<string> BlockerSignals { get; } = new List<string>();
        public List<string> TargetFailureAreas { get; } = new List<string>();
    }

    public sealed class PlaybackQualityFramePacingClassification
    {
        public string Pattern { get; set; } = "not-applicable";
        public double ExpectedFrameDurationMs { get; set; }
        public double RenderIntervalP95FrameRatio { get; set; }
        public double RenderIntervalP99FrameRatio { get; set; }
        public double MaxFrameGapFrameRatio { get; set; }
        public double DroppedVideoFramePercent { get; set; }
        public List<string> Reasons { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
    }

    public static class PlaybackQualityReportAnalyzer
    {
        public static PlaybackQualityModelAnalysis Analyze(PlaybackQualityReport report)
        {
            var analysis = new PlaybackQualityModelAnalysis
            {
                RunId = report.RunId,
                Result = report.Result,
                PrimaryFailureArea = string.IsNullOrWhiteSpace(report.Analysis.PrimaryFailureArea)
                    ? "none"
                    : report.Analysis.PrimaryFailureArea,
                SuggestedNextAction = report.Analysis.SuggestedNextAction
            };

            foreach (var check in report.Checks)
            {
                if (!string.IsNullOrWhiteSpace(check.Signal))
                {
                    AddUnique(analysis.EvidenceSignals, check.Signal);
                }

                if (check.Status == "fail" && !string.IsNullOrWhiteSpace(check.FailureArea))
                {
                    AddUnique(analysis.FailureAreas, check.FailureArea);
                    analysis.FailedChecks.Add(CloneCheck(check));
                }
            }

            foreach (var reason in report.FailureReasons)
            {
                AddUnique(analysis.FailureReasons, reason);
            }

            foreach (var signal in report.Analysis.RelevantSignals)
            {
                AddUnique(analysis.EvidenceSignals, signal);
            }

            foreach (var limitation in report.Limitations)
            {
                AddUnique(analysis.Limitations, limitation);
            }

            analysis.Sample = AssessSample(report);
            analysis.Startup = AssessStartup(report);
            analysis.Source = AssessSource(report);
            analysis.ColorPipeline = AssessColorPipeline(report);
            analysis.Buffering = AssessBuffering(report);
            analysis.AvSync = AssessAvSync(report);
            analysis.Cadence = AssessCadence(report);
            AddDerivedEvidence(analysis, report);
            AddMissingEvidence(analysis, report);
            analysis.OptimizationGate = AssessOptimizationGate(analysis);
            analysis.FramePacing = ClassifyFramePacing(analysis, report);
            AddInvestigationHints(analysis);
            AddTriageSteps(analysis);

            if (string.IsNullOrWhiteSpace(analysis.SuggestedNextAction))
            {
                analysis.SuggestedNextAction = analysis.MissingEvidence.Count == 0
                    ? "Inspect report checks and raw metrics."
                    : "Collect missing evidence before optimizing playback core.";
            }

            return analysis;
        }

        private static PlaybackQualityStartupAssessment AssessStartup(
            PlaybackQualityReport report)
        {
            var startup = new PlaybackQualityStartupAssessment
            {
                CommandReceivedAt = report.Startup.CommandReceivedAt,
                PlaybackStartedAt = report.Startup.PlaybackStartedAt,
                StartupDurationMs = report.Startup.StartupDurationMs
            };

            if (!string.IsNullOrWhiteSpace(startup.CommandReceivedAt))
            {
                AddUnique(startup.Signals, "startup.commandReceivedAt");
            }

            if (!string.IsNullOrWhiteSpace(startup.PlaybackStartedAt))
            {
                AddUnique(startup.Signals, "startup.playbackStartedAt");
            }

            if (startup.StartupDurationMs > 0)
            {
                AddUnique(startup.Signals, "startup.startupDurationMs");
            }

            foreach (var check in report.Checks)
            {
                if (check.Status == "fail" &&
                    check.FailureArea == "startup" &&
                    !string.IsNullOrWhiteSpace(check.Signal))
                {
                    AddUnique(startup.FailedSignals, check.Signal);
                    AddUnique(startup.Signals, check.Signal);
                }
            }

            if (startup.FailedSignals.Count > 0)
            {
                startup.Status = "slow";
                startup.Reason = "Startup timing failed expected thresholds.";
                return startup;
            }

            if (startup.Signals.Count == 0)
            {
                startup.Status = "missing-evidence";
                startup.Reason = "Startup timing telemetry is missing.";
                return startup;
            }

            startup.Status = "ready";
            startup.Reason = "Startup telemetry is available and no startup threshold failed.";
            return startup;
        }

        private static PlaybackQualityBufferingAssessment AssessBuffering(
            PlaybackQualityReport report)
        {
            var buffering = new PlaybackQualityBufferingAssessment
            {
                SubmittedAudioFrames = report.Buffers.SubmittedAudioFrames,
                QueuedAudioBuffers = report.Buffers.QueuedAudioBuffers,
                VideoStarvedPasses = report.Buffers.VideoStarvedPasses,
                AudioStarvedPasses = report.Buffers.AudioStarvedPasses
            };

            var hasBufferEvidence =
                buffering.SubmittedAudioFrames > 0 ||
                buffering.QueuedAudioBuffers > 0 ||
                buffering.VideoStarvedPasses > 0 ||
                buffering.AudioStarvedPasses > 0;
            if (hasBufferEvidence)
            {
                AddUnique(buffering.Signals, "buffers.submittedAudioFrames");
                AddUnique(buffering.Signals, "buffers.queuedAudioBuffers");
                AddUnique(buffering.Signals, "buffers.videoStarvedPasses");
                AddUnique(buffering.Signals, "buffers.audioStarvedPasses");
            }

            foreach (var check in report.Checks)
            {
                if (check.Status == "fail" &&
                    check.FailureArea == "buffering" &&
                    !string.IsNullOrWhiteSpace(check.Signal))
                {
                    AddUnique(buffering.FailedSignals, check.Signal);
                    AddUnique(buffering.Signals, check.Signal);
                }
            }

            if (buffering.FailedSignals.Count > 0)
            {
                buffering.Status = "starved";
                buffering.Reason = "Playback supply starvation failed expected buffering thresholds.";
                return buffering;
            }

            if (!hasBufferEvidence)
            {
                buffering.Status = "missing-evidence";
                buffering.Reason = "Buffering and starvation telemetry is missing.";
                return buffering;
            }

            if (buffering.VideoStarvedPasses > 0 || buffering.AudioStarvedPasses > 0)
            {
                buffering.Status = "observed-starvation";
                buffering.Reason = "Playback starvation was observed but no buffering threshold failed.";
                return buffering;
            }

            buffering.Status = "stable";
            buffering.Reason = "Buffering telemetry is available and no starvation failures were reported.";
            return buffering;
        }

        private static PlaybackQualityAvSyncAssessment AssessAvSync(
            PlaybackQualityReport report)
        {
            var sync = new PlaybackQualityAvSyncAssessment
            {
                AudioClockTicks = report.Sync.AudioClockTicks,
                VideoPositionTicks = report.Sync.VideoPositionTicks,
                AudioVideoDriftMsP50 = report.Sync.AudioVideoDriftMsP50,
                AudioVideoDriftMsP95 = report.Sync.AudioVideoDriftMsP95,
                AudioVideoDriftMsP99 = report.Sync.AudioVideoDriftMsP99,
                AudioVideoDriftMsMax = report.Sync.AudioVideoDriftMsMax
            };

            var hasClockEvidence = sync.AudioClockTicks != 0 || sync.VideoPositionTicks != 0;
            var hasDriftEvidence =
                sync.AudioVideoDriftMsP50 > 0 ||
                sync.AudioVideoDriftMsP95 > 0 ||
                sync.AudioVideoDriftMsP99 > 0 ||
                sync.AudioVideoDriftMsMax > 0;
            if (hasClockEvidence)
            {
                AddUnique(sync.Signals, "sync.audioClockTicks");
                AddUnique(sync.Signals, "sync.videoPositionTicks");
            }

            if (hasDriftEvidence)
            {
                AddUnique(sync.Signals, "sync.audioVideoDriftMsP50");
                AddUnique(sync.Signals, "sync.audioVideoDriftMsP95");
                AddUnique(sync.Signals, "sync.audioVideoDriftMsP99");
                AddUnique(sync.Signals, "sync.audioVideoDriftMsMax");
            }

            foreach (var check in report.Checks)
            {
                if (check.Status == "fail" &&
                    check.FailureArea == "av-sync" &&
                    !string.IsNullOrWhiteSpace(check.Signal))
                {
                    AddUnique(sync.FailedSignals, check.Signal);
                    AddUnique(sync.Signals, check.Signal);
                }
            }

            if (sync.FailedSignals.Count > 0)
            {
                sync.Status = "drift";
                sync.Reason = "A/V sync drift failed expected thresholds.";
                return sync;
            }

            if (!hasClockEvidence && !hasDriftEvidence)
            {
                sync.Status = "missing-evidence";
                sync.Reason = "A/V sync telemetry is missing.";
                return sync;
            }

            sync.Status = "synced";
            sync.Reason = "A/V sync telemetry is available and no sync threshold failed.";
            return sync;
        }

        private static PlaybackQualityColorPipelineAssessment AssessColorPipeline(
            PlaybackQualityReport report)
        {
            var color = new PlaybackQualityColorPipelineAssessment
            {
                ActualHdrOutput = report.ColorPipeline.ActualHdrOutput,
                SwapChainFormat = report.ColorPipeline.SwapChainFormat,
                SwapChainColorSpace = report.ColorPipeline.SwapChainColorSpace,
                IsTenBitSwapChain = report.ColorPipeline.IsTenBitSwapChain,
                DxgiInput = report.ColorPipeline.DxgiInput,
                DxgiOutput = report.ColorPipeline.DxgiOutput,
                ConversionStatus = report.ColorPipeline.ConversionStatus,
                IsVideoProcessorColorSpaceValidated = report.ColorPipeline.IsVideoProcessorColorSpaceValidated,
                ForceSdrOutput = report.ColorPipeline.ForceSdrOutput
            };

            AddColorPipelineSignals(color);
            foreach (var check in report.Checks)
            {
                if (check.Status == "fail" &&
                    check.FailureArea == "color-pipeline" &&
                    !string.IsNullOrWhiteSpace(check.Signal))
                {
                    AddUnique(color.MismatchedSignals, check.Signal);
                    AddUnique(color.Signals, check.Signal);
                }
            }

            if (color.MismatchedSignals.Count > 0)
            {
                color.Status = "mismatch";
                color.Reason = "Color pipeline observations did not match expected output.";
                return color;
            }

            if (color.Signals.Count == 0)
            {
                color.Status = "missing-evidence";
                color.Reason = "Color pipeline telemetry is missing.";
                return color;
            }

            color.Status = "matched";
            color.Reason = "Color pipeline telemetry is available and has no color-pipeline mismatches.";
            return color;
        }

        private static void AddColorPipelineSignals(PlaybackQualityColorPipelineAssessment color)
        {
            if (!string.IsNullOrWhiteSpace(color.ActualHdrOutput))
            {
                AddUnique(color.Signals, "colorPipeline.actualHdrOutput");
            }

            if (!string.IsNullOrWhiteSpace(color.SwapChainFormat))
            {
                AddUnique(color.Signals, "colorPipeline.swapChainFormat");
            }

            if (!string.IsNullOrWhiteSpace(color.SwapChainColorSpace))
            {
                AddUnique(color.Signals, "colorPipeline.swapChainColorSpace");
            }

            if (color.IsTenBitSwapChain)
            {
                AddUnique(color.Signals, "colorPipeline.isTenBitSwapChain");
            }

            if (!string.IsNullOrWhiteSpace(color.DxgiInput))
            {
                AddUnique(color.Signals, "colorPipeline.dxgiInput");
            }

            if (!string.IsNullOrWhiteSpace(color.DxgiOutput))
            {
                AddUnique(color.Signals, "colorPipeline.dxgiOutput");
            }

            if (!string.IsNullOrWhiteSpace(color.ConversionStatus))
            {
                AddUnique(color.Signals, "colorPipeline.conversionStatus");
            }

            if (color.IsVideoProcessorColorSpaceValidated)
            {
                AddUnique(color.Signals, "colorPipeline.isVideoProcessorColorSpaceValidated");
            }

            if (color.ForceSdrOutput)
            {
                AddUnique(color.Signals, "colorPipeline.forceSdrOutput");
            }
        }

        private static PlaybackQualitySourceAssessment AssessSource(PlaybackQualityReport report)
        {
            var source = new PlaybackQualitySourceAssessment
            {
                Codec = report.Source.Codec,
                Width = report.Source.Width,
                Height = report.Source.Height,
                FrameRate = report.Source.FrameRate,
                HdrKind = report.Source.HdrKind,
                HdrPlaybackStrategy = report.Source.HdrPlaybackStrategy,
                IsHdr = report.Source.IsHdr,
                IsDirectPlayable = report.Source.IsDirectPlayable,
                IsDolbyVision = report.Source.IsDolbyVision,
                DolbyVisionProfile = report.Source.DolbyVisionProfile,
                DolbyVisionCompatibilityId = report.Source.DolbyVisionCompatibilityId,
                HasHdr10BaseLayer = report.Source.HasHdr10BaseLayer,
                HasHlgBaseLayer = report.Source.HasHlgBaseLayer
            };

            AddSourceSignals(source);
            foreach (var check in report.Checks)
            {
                if (check.Status == "fail" &&
                    check.FailureArea == "unsupported-source" &&
                    !string.IsNullOrWhiteSpace(check.Signal))
                {
                    AddUnique(source.MismatchedSignals, check.Signal);
                    AddUnique(source.Signals, check.Signal);
                }
            }

            if (source.MismatchedSignals.Count > 0)
            {
                source.Status = "mismatch";
                source.Reason = "Source metadata did not match expected reference metadata.";
                return source;
            }

            if (string.IsNullOrWhiteSpace(source.Codec) &&
                source.FrameRate <= 0 &&
                string.IsNullOrWhiteSpace(source.HdrKind))
            {
                source.Status = "missing-evidence";
                source.Reason = "Source codec, frame rate, and HDR kind are missing.";
                return source;
            }

            if (!source.IsDirectPlayable &&
                (source.IsDolbyVision || source.HdrKind == "DolbyVisionUnsupported"))
            {
                source.Status = "unsupported";
                source.Reason = "Parsed source is not directly playable by the current Core HDR policy.";
                return source;
            }

            source.Status = "matched";
            source.Reason = "Parsed source metadata is available and has no unsupported-source mismatches.";
            return source;
        }

        private static void AddSourceSignals(PlaybackQualitySourceAssessment source)
        {
            var hasSourceEvidence =
                !string.IsNullOrWhiteSpace(source.Codec) ||
                source.Width > 0 ||
                source.Height > 0 ||
                source.FrameRate > 0 ||
                !string.IsNullOrWhiteSpace(source.HdrKind) ||
                !string.IsNullOrWhiteSpace(source.HdrPlaybackStrategy);

            if (!string.IsNullOrWhiteSpace(source.Codec))
            {
                AddUnique(source.Signals, "source.codec");
            }

            if (source.Width > 0)
            {
                AddUnique(source.Signals, "source.width");
            }

            if (source.Height > 0)
            {
                AddUnique(source.Signals, "source.height");
            }

            if (source.FrameRate > 0)
            {
                AddUnique(source.Signals, "source.frameRate");
            }

            if (!string.IsNullOrWhiteSpace(source.HdrKind))
            {
                AddUnique(source.Signals, "source.hdrKind");
            }

            if (!string.IsNullOrWhiteSpace(source.HdrPlaybackStrategy))
            {
                AddUnique(source.Signals, "source.hdrPlaybackStrategy");
            }

            if (hasSourceEvidence)
            {
                AddUnique(source.Signals, "source.isHdr");
                AddUnique(source.Signals, "source.isDirectPlayable");
                AddUnique(source.Signals, "source.isDolbyVision");
                AddUnique(source.Signals, "source.hasHdr10BaseLayer");
                AddUnique(source.Signals, "source.hasHlgBaseLayer");
            }

            if (source.DolbyVisionProfile.HasValue)
            {
                AddUnique(source.Signals, "source.dolbyVisionProfile");
            }

            if (source.DolbyVisionCompatibilityId.HasValue)
            {
                AddUnique(source.Signals, "source.dolbyVisionCompatibilityId");
            }
        }

        private static void AddTriageSteps(PlaybackQualityModelAnalysis analysis)
        {
            var rank = 1;
            if (analysis.OptimizationGate.Status == "blocked")
            {
                var evidenceHint = FindHint(analysis, "evidence-collection");
                if (evidenceHint != null)
                {
                    AddTriageStep(analysis, evidenceHint, rank++, "blocker");
                }
            }

            var priorityAreas = new[]
            {
                "unsupported-source",
                "color-pipeline",
                "startup",
                "buffering",
                "av-sync",
                "frame-pacing",
                "unknown"
            };

            foreach (var area in priorityAreas)
            {
                var hint = FindHint(analysis, area);
                if (hint == null)
                {
                    continue;
                }

                if (HasTriageStep(analysis, area))
                {
                    continue;
                }

                AddTriageStep(analysis, hint, rank++, "failure");
            }
        }

        private static PlaybackQualityInvestigationHint? FindHint(
            PlaybackQualityModelAnalysis analysis,
            string area)
        {
            foreach (var hint in analysis.InvestigationHints)
            {
                if (hint.FailureArea == area)
                {
                    return hint;
                }
            }

            return null;
        }

        private static bool HasTriageStep(
            PlaybackQualityModelAnalysis analysis,
            string area)
        {
            foreach (var step in analysis.TriageSteps)
            {
                if (step.FailureArea == area)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddTriageStep(
            PlaybackQualityModelAnalysis analysis,
            PlaybackQualityInvestigationHint hint,
            int rank,
            string kind)
        {
            var step = new PlaybackQualityTriageStep
            {
                Rank = rank,
                Kind = kind,
                FailureArea = hint.FailureArea,
                SuggestedAction = hint.SuggestedAction
            };

            foreach (var signal in hint.Signals)
            {
                AddUnique(step.Signals, signal);
            }

            foreach (var target in hint.CodeTargets)
            {
                AddUnique(step.CodeTargets, target);
            }

            analysis.TriageSteps.Add(step);
        }

        private static PlaybackQualityFramePacingClassification ClassifyFramePacing(
            PlaybackQualityModelAnalysis analysis,
            PlaybackQualityReport report)
        {
            var classification = new PlaybackQualityFramePacingClassification();
            AddFramePacingSeverity(classification, report);
            if (!analysis.FailureAreas.Contains("frame-pacing"))
            {
                return classification;
            }

            if (HasFailedSignal(analysis, "timing.renderedVideoFrames") ||
                analysis.Sample.Status == "insufficient")
            {
                classification.Pattern = "sample-insufficient";
                AddUnique(classification.Signals, "timing.renderedVideoFrames");
                AddUnique(classification.Signals, "sample.status");
                AddUnique(classification.Reasons, "Frame pacing sample is too short to diagnose timing thresholds.");
                return classification;
            }

            if (HasFailedSignal(analysis, "display.refreshRateHz"))
            {
                classification.Pattern = "refresh-mismatch";
                AddUnique(classification.Signals, "display.refreshRateHz");
                AddUnique(classification.Reasons, "Display refresh rate did not match source cadence.");
                return classification;
            }

            if (HasFailedSignal(analysis, "buffers.videoStarvedPasses") ||
                HasFailedSignal(analysis, "buffers.audioStarvedPasses"))
            {
                classification.Pattern = "starvation-driven";
                AddFailedSignalIfPresent(analysis, classification, "buffers.videoStarvedPasses");
                AddFailedSignalIfPresent(analysis, classification, "buffers.audioStarvedPasses");
                AddUnique(classification.Reasons, "Playback starvation coincided with frame pacing failure.");
                return classification;
            }

            if (HasFailedSignal(analysis, "timing.renderIntervalMsP95"))
            {
                classification.Pattern = "sustained-jitter";
                AddUnique(classification.Signals, "timing.renderIntervalMsP95");
                AddUnique(classification.Reasons, "Render interval p95 failed, indicating repeated frame pacing jitter.");
                return classification;
            }

            if (HasFailedSignal(analysis, "timing.renderIntervalMsP99"))
            {
                classification.Pattern = "tail-jitter";
                AddUnique(classification.Signals, "timing.renderIntervalMsP99");
                AddUnique(classification.Reasons, "Render interval p99 failed while p95 did not, indicating tail jitter.");
                return classification;
            }

            if (HasFailedSignal(analysis, "timing.droppedVideoFrames"))
            {
                classification.Pattern = "dropped-frames";
                AddUnique(classification.Signals, "timing.droppedVideoFrames");
                AddUnique(classification.Reasons, "Dropped video frames exceeded threshold.");
                return classification;
            }

            if (HasFailedSignal(analysis, "timing.maxFrameGapMs"))
            {
                classification.Pattern = "isolated-gap";
                AddUnique(classification.Signals, "timing.maxFrameGapMs");
                AddUnique(classification.Reasons, "Single max frame gap failed without sustained render interval failures.");
                return classification;
            }

            classification.Pattern = "unknown";
            AddUnique(classification.Reasons, "Frame pacing failed without a recognized timing signal.");
            return classification;
        }

        private static void AddFramePacingSeverity(
            PlaybackQualityFramePacingClassification classification,
            PlaybackQualityReport report)
        {
            classification.ExpectedFrameDurationMs = report.Timing.ExpectedFrameDurationMs;
            if (report.Timing.ExpectedFrameDurationMs > 0)
            {
                classification.RenderIntervalP95FrameRatio =
                    report.Timing.RenderIntervalMsP95 / report.Timing.ExpectedFrameDurationMs;
                classification.RenderIntervalP99FrameRatio =
                    report.Timing.RenderIntervalMsP99 / report.Timing.ExpectedFrameDurationMs;
                classification.MaxFrameGapFrameRatio =
                    report.Timing.MaxFrameGapMs / report.Timing.ExpectedFrameDurationMs;
            }

            var observedFrames =
                (double)report.Timing.RenderedVideoFrames + report.Timing.DroppedVideoFrames;
            if (observedFrames > 0)
            {
                classification.DroppedVideoFramePercent =
                    report.Timing.DroppedVideoFrames * 100.0 / observedFrames;
            }
        }

        private static bool HasFailedSignal(
            PlaybackQualityModelAnalysis analysis,
            string signal)
        {
            foreach (var check in analysis.FailedChecks)
            {
                if (check.Signal == signal)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddFailedSignalIfPresent(
            PlaybackQualityModelAnalysis analysis,
            PlaybackQualityFramePacingClassification classification,
            string signal)
        {
            if (HasFailedSignal(analysis, signal))
            {
                AddUnique(classification.Signals, signal);
            }
        }

        private static PlaybackQualityOptimizationGate AssessOptimizationGate(
            PlaybackQualityModelAnalysis analysis)
        {
            var gate = new PlaybackQualityOptimizationGate();
            if (analysis.Result != "fail")
            {
                gate.Status = "not-needed";
                AddUnique(gate.Blockers, "result." + analysis.Result);
                return gate;
            }

            if (analysis.Sample.Status != "sufficient")
            {
                AddUnique(gate.Blockers, "sample.insufficient");
                AddUnique(gate.BlockerSignals, "sample.status");
            }

            if (analysis.MissingEvidence.Count > 0)
            {
                AddUnique(gate.Blockers, "missingEvidence");
                foreach (var signal in analysis.MissingEvidence)
                {
                    AddUnique(gate.BlockerSignals, signal);
                }
            }

            if (analysis.FailureAreas.Count == 0)
            {
                AddUnique(gate.Blockers, "failureAreas.missing");
            }

            if (analysis.Source.Status == "mismatch" || analysis.Source.Status == "unsupported")
            {
                AddUnique(gate.Blockers, "source." + analysis.Source.Status);
                foreach (var signal in analysis.Source.MismatchedSignals)
                {
                    AddUnique(gate.BlockerSignals, signal);
                }
            }

            if (gate.Blockers.Count > 0)
            {
                gate.Status = "blocked";
                return gate;
            }

            gate.Status = "ready";
            gate.CanOptimizePlaybackCore = true;
            var targetArea = GetHighestPriorityFailureArea(analysis);
            if (!string.IsNullOrWhiteSpace(targetArea))
            {
                AddUnique(gate.TargetFailureAreas, targetArea);
                foreach (var check in analysis.FailedChecks)
                {
                    if (check.FailureArea == targetArea &&
                        !string.IsNullOrWhiteSpace(check.Signal))
                    {
                        AddUnique(gate.BlockerSignals, check.Signal);
                    }
                }
            }

            return gate;
        }

        private static string GetHighestPriorityFailureArea(
            PlaybackQualityModelAnalysis analysis)
        {
            var priorityAreas = new[]
            {
                "unsupported-source",
                "color-pipeline",
                "startup",
                "buffering",
                "av-sync",
                "frame-pacing",
                "unknown"
            };

            foreach (var area in priorityAreas)
            {
                if (analysis.FailureAreas.Contains(area))
                {
                    return area;
                }
            }

            return "";
        }

        private static void AddMissingEvidence(
            PlaybackQualityModelAnalysis analysis,
            PlaybackQualityReport report)
        {
            if (string.IsNullOrWhiteSpace(report.Source.Codec))
            {
                analysis.MissingEvidence.Add("source.codec");
            }

            if (report.Source.FrameRate <= 0)
            {
                analysis.MissingEvidence.Add("source.frameRate");
            }

            if (report.Timing.RenderedVideoFrames == 0)
            {
                analysis.MissingEvidence.Add("timing.renderedVideoFrames");
            }

            if (report.Expected != null &&
                report.Expected.MaxStartupDurationMs.HasValue &&
                report.Startup.StartupDurationMs <= 0)
            {
                analysis.MissingEvidence.Add("startup.startupDurationMs");
            }

            if (report.Expected != null &&
                report.Expected.MaxRenderIntervalMsP95.HasValue &&
                report.Timing.RenderIntervalMsP95 <= 0)
            {
                analysis.MissingEvidence.Add("timing.renderIntervalMsP95");
            }

            if (report.Expected != null &&
                report.Expected.MaxFrameGapMs.HasValue &&
                report.Timing.MaxFrameGapMs <= 0)
            {
                analysis.MissingEvidence.Add("timing.maxFrameGapMs");
            }

            if (report.Expected != null &&
                report.Expected.MaxRenderIntervalMsP99.HasValue &&
                report.Timing.RenderIntervalMsP99 <= 0)
            {
                analysis.MissingEvidence.Add("timing.renderIntervalMsP99");
            }

            if (report.Source.FrameRate > 0 && report.Timing.ExpectedFrameDurationMs <= 0)
            {
                analysis.MissingEvidence.Add("timing.expectedFrameDurationMs");
            }

            if (report.Expected != null &&
                report.Expected.MaxAudioVideoDriftMsP95.HasValue &&
                report.Sync.AudioVideoDriftMsP95 <= 0)
            {
                analysis.MissingEvidence.Add("sync.audioVideoDriftMsP95");
            }

            if (report.Sync.AudioVideoDriftMsP95 <= 0 && report.Timing.RenderedVideoFrames == 0)
            {
                AddUnique(analysis.MissingEvidence, "sync.audioVideoDriftMsP95");
            }

            if (report.Buffers.QueuedAudioBuffers == 0 && report.Timing.RenderedVideoFrames == 0)
            {
                analysis.MissingEvidence.Add("buffers.queuedAudioBuffers");
            }

            if (report.Expected != null &&
                !string.IsNullOrWhiteSpace(report.Expected.HdrOutput) &&
                string.IsNullOrWhiteSpace(report.ColorPipeline.ActualHdrOutput))
            {
                analysis.MissingEvidence.Add("colorPipeline.actualHdrOutput");
            }

            if (string.IsNullOrWhiteSpace(report.ColorPipeline.DxgiInput))
            {
                AddUnique(analysis.MissingEvidence, "colorPipeline.dxgiInput");
            }

            if (report.Expected != null &&
                !string.IsNullOrWhiteSpace(report.Expected.DxgiOutput) &&
                string.IsNullOrWhiteSpace(report.ColorPipeline.DxgiOutput))
            {
                analysis.MissingEvidence.Add("colorPipeline.dxgiOutput");
            }

            if (report.Expected != null &&
                report.Expected.RequireValidatedConversion &&
                string.IsNullOrWhiteSpace(report.ColorPipeline.ConversionStatus))
            {
                analysis.MissingEvidence.Add("colorPipeline.conversionStatus");
            }

            if (string.IsNullOrWhiteSpace(report.Display.HdrStatus))
            {
                analysis.MissingEvidence.Add("display.hdrStatus");
            }

            if (report.Display.RefreshRateHz <= 0)
            {
                analysis.MissingEvidence.Add("display.refreshRateHz");
            }
        }

        private static PlaybackQualitySampleAssessment AssessSample(PlaybackQualityReport report)
        {
            var sample = new PlaybackQualitySampleAssessment
            {
                RenderedVideoFrames = report.Timing.RenderedVideoFrames,
                MinRenderedVideoFrames = report.Expected?.MinRenderedVideoFrames
            };
            var sampleFrameRate = GetSampleFrameRate(report);
            if (sampleFrameRate > 0)
            {
                sample.ObservedSampleDurationMs = report.Timing.RenderedVideoFrames * 1000.0 / sampleFrameRate;
                if (report.Expected != null && report.Expected.MinRenderedVideoFrames.HasValue)
                {
                    sample.MinimumSampleDurationMs =
                        report.Expected.MinRenderedVideoFrames.Value * 1000.0 / sampleFrameRate;
                }
            }

            if (report.Expected != null && report.Expected.MinRenderedVideoFrames.HasValue)
            {
                var remaining = report.Expected.MinRenderedVideoFrames.Value - (long)report.Timing.RenderedVideoFrames;
                sample.AdditionalRenderedFramesRequired = remaining > 0 ? remaining : 0;
            }

            if (report.Timing.RenderedVideoFrames == 0)
            {
                sample.Status = "insufficient";
                sample.Reason = "No rendered video frames were captured; collect a real playback sample before optimizing playback core.";
                return sample;
            }

            if (report.Expected != null &&
                report.Expected.MinRenderedVideoFrames.HasValue &&
                report.Timing.RenderedVideoFrames < (ulong)report.Expected.MinRenderedVideoFrames.Value)
            {
                sample.Status = "insufficient";
                sample.Reason = "Rendered frame sample is below the expected minimum; do not tune frame pacing from this run alone.";
                return sample;
            }

            sample.Status = "sufficient";
            sample.Reason = report.Expected != null && report.Expected.MinRenderedVideoFrames.HasValue
                ? "Rendered frame sample met the expected minimum."
                : "Rendered video frames were captured; no minimum rendered frame expectation was supplied.";
            return sample;
        }

        private static PlaybackQualityCadenceAssessment AssessCadence(PlaybackQualityReport report)
        {
            var cadence = PlaybackRefreshRatePolicy.AssessCadence(
                report.Display.RefreshRateHz,
                report.Source.FrameRate);

            if (report.Source.FrameRate > 0)
            {
                AddUnique(cadence.Signals, "source.frameRate");
            }

            if (report.Display.RefreshRateHz > 0)
            {
                AddUnique(cadence.Signals, "display.refreshRateHz");
            }

            if (cadence.IsClockSpeedAdjustmentRequired)
            {
                AddUnique(cadence.Signals, "cadence.clockSpeedAdjustmentPercent");
            }

            return cadence;
        }

        private static double GetSampleFrameRate(PlaybackQualityReport report)
        {
            if (report.Source.FrameRate > 0)
            {
                return report.Source.FrameRate;
            }

            if (report.Expected != null && report.Expected.FrameRate > 0)
            {
                return report.Expected.FrameRate;
            }

            return 0;
        }

        private static void AddInvestigationHints(PlaybackQualityModelAnalysis analysis)
        {
            var areas = new List<string>();
            if (!string.IsNullOrWhiteSpace(analysis.PrimaryFailureArea) &&
                analysis.PrimaryFailureArea != "none")
            {
                AddUnique(areas, analysis.PrimaryFailureArea);
            }

            foreach (var area in analysis.FailureAreas)
            {
                AddUnique(areas, area);
            }

            if (analysis.MissingEvidence.Count > 0)
            {
                AddUnique(areas, "evidence-collection");
            }

            foreach (var area in areas)
            {
                var hint = CreateInvestigationHint(area, analysis);
                if (hint == null)
                {
                    continue;
                }

                foreach (var check in analysis.FailedChecks)
                {
                    if (check.FailureArea == area && !string.IsNullOrWhiteSpace(check.Signal))
                    {
                        AddUnique(hint.Signals, check.Signal);
                    }
                }

                if (area == "evidence-collection")
                {
                    foreach (var signal in analysis.MissingEvidence)
                    {
                        AddUnique(hint.Signals, signal);
                    }
                }

                analysis.InvestigationHints.Add(hint);
            }
        }

        private static PlaybackQualityInvestigationHint? CreateInvestigationHint(
            string area,
            PlaybackQualityModelAnalysis analysis)
        {
            switch (area)
            {
                case "unsupported-source":
                    return NewHint(
                        area,
                        "Verify media source selection, codec support, HDR/Dolby Vision classification, and fallback policy before tuning playback timing.",
                        new[]
                        {
                            "src/NextGenEmby.Core/Playback/PlaybackOrchestrator.cs",
                            "src/NextGenEmby.Core/Playback/HdrPlaybackProfileClassifier.cs",
                            "src/NextGenEmby.Core/Emby"
                        },
                        new[]
                        {
                            "source.codec",
                            "source.frameRate",
                            "source.hdrKind",
                            "source.hdrPlaybackStrategy",
                            "source.isHdr",
                            "source.isDirectPlayable",
                            "source.isDolbyVision",
                            "source.dolbyVisionProfile",
                            "source.dolbyVisionCompatibilityId",
                            "source.hasHdr10BaseLayer",
                            "source.hasHlgBaseLayer"
                        });
                case "color-pipeline":
                    return NewHint(
                        area,
                        "Compare source HDR kind, display HDR state, swapchain format, DXGI input/output color spaces, and conversion validation.",
                        new[]
                        {
                            "src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.cpp",
                            "src/NextGenEmby.Native/DxDeviceResources.cpp",
                            "src/NextGenEmby.Native/NativePlaybackEngine.cpp"
                        },
                        new[]
                        {
                            "colorPipeline.actualHdrOutput",
                            "colorPipeline.swapChainFormat",
                            "colorPipeline.swapChainColorSpace",
                            "colorPipeline.dxgiInput",
                            "colorPipeline.dxgiOutput",
                            "colorPipeline.conversionStatus",
                            "display.hdrStatus"
                        });
                case "startup":
                    return NewHint(
                        area,
                        "Separate Emby request latency, native open/demux initialization, and first-frame readiness before changing render pacing.",
                        new[]
                        {
                            "src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportComposer.cs",
                            "src/NextGenEmby.Native/NativePlaybackEngine.cpp",
                            "src/NextGenEmby.Native/Media/PlaybackGraph.cpp"
                        },
                        new[]
                        {
                            "startup.commandReceivedAt",
                            "startup.playbackStartedAt",
                            "startup.startupDurationMs"
                        });
                case "buffering":
                    return NewHint(
                        area,
                        "Inspect demux, network, decode starvation, and audio queue depth before changing frame drop thresholds.",
                        new[]
                        {
                            "src/NextGenEmby.Native/Media/PlaybackGraph.cpp",
                            "src/NextGenEmby.Native/Media/VideoDecoder.cpp",
                            "src/NextGenEmby.Native/Media/AudioDecoder.cpp"
                        },
                        new[]
                        {
                            "buffers.videoStarvedPasses",
                            "buffers.audioStarvedPasses",
                            "buffers.queuedAudioBuffers"
                        });
                case "av-sync":
                    return NewHint(
                        area,
                        "Inspect XAudio clock derivation, queued buffer depth, video PTS comparison, and audio-wait policy.",
                        new[]
                        {
                            "src/NextGenEmby.Native/Media/AudioRenderer.cpp",
                            "src/NextGenEmby.Native/Media/PlaybackGraph.cpp",
                            "src/NextGenEmby.Native/Media/FramePacing.h"
                        },
                        new[]
                        {
                            "sync.audioVideoDriftMsP50",
                            "sync.audioVideoDriftMsP95",
                            "sync.audioVideoDriftMsP99",
                            "sync.audioVideoDriftMsMax",
                            "buffers.queuedAudioBuffers"
                        });
                case "frame-pacing":
                    if (analysis.FramePacing.Pattern == "sample-insufficient")
                    {
                        return NewHint(
                            area,
                            "Collect a longer rendered-frame sample and verify playback startup/render readiness before changing frame pacing thresholds.",
                            new[]
                            {
                                "src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportComposer.cs",
                                "src/NextGenEmby.Native/NativePlaybackQualityMetrics.cpp",
                                "src/NextGenEmby.Native/Media/PlaybackGraph.cpp"
                            },
                            new[]
                            {
                                "sample.status",
                                "sample.renderedVideoFrames",
                                "timing.renderedVideoFrames",
                                "startup.startupDurationMs"
                            });
                    }

                    if (analysis.FramePacing.Pattern == "starvation-driven")
                    {
                        return NewHint(
                            area,
                            "Inspect demux, decode, network supply, and audio queue depth before changing frame pacing wait/drop thresholds.",
                            new[]
                            {
                                "src/NextGenEmby.Native/Media/PlaybackGraph.cpp",
                                "src/NextGenEmby.Native/Media/VideoDecoder.cpp",
                                "src/NextGenEmby.Native/Media/AudioDecoder.cpp",
                                "src/NextGenEmby.Native/Media/AudioRenderer.cpp"
                            },
                            new[]
                            {
                                "timing.maxFrameGapMs",
                                "buffers.videoStarvedPasses",
                                "buffers.audioStarvedPasses",
                                "buffers.queuedAudioBuffers"
                            });
                    }

                    return NewHint(
                        area,
                        "Inspect render interval percentiles, max frame gap, source/display cadence match, wait/drop thresholds, and starvation counters together.",
                        new[]
                        {
                            "src/NextGenEmby.Native/Media/FramePacing.h",
                            "src/NextGenEmby.Native/Media/PlaybackGraph.cpp",
                            "src/NextGenEmby.Native/HdrDisplayController.cpp",
                            "src/NextGenEmby.Core/PlaybackQuality/PlaybackRefreshRatePolicy.cs"
                        },
                        new[]
                        {
                            "timing.expectedFrameDurationMs",
                            "timing.renderIntervalMsP95",
                            "timing.renderIntervalMsP99",
                            "timing.maxFrameGapMs",
                            "timing.droppedVideoFrames",
                            "display.refreshRateHz",
                            "source.frameRate"
                        });
                case "evidence-collection":
                    return NewHint(
                        area,
                        "Collect missing telemetry before optimizing playback behavior; absent evidence is treated separately from a real playback failure.",
                        new[]
                        {
                            "src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportMapper.cs",
                            "src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportComposer.cs",
                            "src/NextGenEmby.Native/NativePlaybackQualityMetrics.cpp",
                            "src/NextGenEmby.Native/Media/PlaybackGraph.cpp"
                        },
                        new string[0]);
                default:
                    return NewHint(
                        "unknown",
                        "Inspect raw metrics, failed checks, and missing evidence before changing playback behavior.",
                        new[]
                        {
                            "src/NextGenEmby.Core/PlaybackQuality",
                            "src/NextGenEmby.Native"
                        },
                        new string[0]);
            }
        }

        private static PlaybackQualityInvestigationHint NewHint(
            string area,
            string suggestedAction,
            string[] codeTargets,
            string[] signals)
        {
            var hint = new PlaybackQualityInvestigationHint
            {
                FailureArea = area,
                SuggestedAction = suggestedAction
            };

            foreach (var target in codeTargets)
            {
                AddUnique(hint.CodeTargets, target);
            }

            foreach (var signal in signals)
            {
                AddUnique(hint.Signals, signal);
            }

            return hint;
        }

        private static void AddDerivedEvidence(
            PlaybackQualityModelAnalysis analysis,
            PlaybackQualityReport report)
        {
            if (report.Timing.ExpectedFrameDurationMs <= 0)
            {
                return;
            }

            foreach (var check in report.Checks)
            {
                if (check.Status == "fail" && check.FailureArea == "frame-pacing")
                {
                    AddUnique(analysis.EvidenceSignals, "timing.expectedFrameDurationMs");
                    return;
                }
            }
        }

        private static PlaybackQualityCheck CloneCheck(PlaybackQualityCheck check)
        {
            return new PlaybackQualityCheck
            {
                Name = check.Name,
                Signal = check.Signal,
                Status = check.Status,
                FailureArea = check.FailureArea,
                Expected = check.Expected,
                Actual = check.Actual,
                Message = check.Message
            };
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }
    }
}
