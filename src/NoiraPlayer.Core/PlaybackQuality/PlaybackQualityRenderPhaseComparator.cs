using System;
using System.Collections.Generic;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityRenderPhaseComparison
    {
        public int SchemaVersion { get; set; } = 1;
        public string EvaluationVersion { get; set; } =
            PlaybackQualityRunResult.CurrentEvaluationVersion;
        public string ComparisonScope { get; set; } = "video-render-phases";
        public string CaseId { get; set; } = "";
        public int RepeatIndex { get; set; }
        public string BaselineRunId { get; set; } = "";
        public string CandidateRunId { get; set; } = "";
        public string Status { get; set; } = "insufficient-evidence";
        public string Result { get; set; } = "insufficient-evidence";
        public PlaybackQualityRenderPhaseSourceIdentity Source { get; set; } =
            new PlaybackQualityRenderPhaseSourceIdentity();
        public PlaybackQualityRenderPhaseBuildIdentity Builds { get; set; } =
            new PlaybackQualityRenderPhaseBuildIdentity();
        public PlaybackQualityRenderPhaseExecutionContext Execution { get; set; } =
            new PlaybackQualityRenderPhaseExecutionContext();
        public PlaybackQualityRenderPhaseSamples Samples { get; set; } =
            new PlaybackQualityRenderPhaseSamples();
        public PlaybackQualityRenderPhaseTransportContext Transport { get; set; } =
            new PlaybackQualityRenderPhaseTransportContext();
        public List<PlaybackQualityRenderPhaseMetricDelta> Metrics { get; } =
            new List<PlaybackQualityRenderPhaseMetricDelta>();
        public List<string> Blockers { get; } = new List<string>();
        public List<string> Limitations { get; } = new List<string>
        {
            "phase-local diagnostic: does not accept or reject the overall playback candidate",
            "CPU timing only: does not prove GPU completion, display output, frame pacing, A/V sync, or color accuracy",
            "transport wait is context only and is not used to rank video processor CPU phases"
        };
    }

    public sealed class PlaybackQualityRenderPhaseSourceIdentity
    {
        public string OpenedSourceHash { get; set; } = "";
        public string OpenedSourceHashKind { get; set; } = "";
        public string ColorExpectationProfile { get; set; } = "";
    }

    public sealed class PlaybackQualityRenderPhaseBuildIdentity
    {
        public string BaselineCollectorVersion { get; set; } = "";
        public string CandidateCollectorVersion { get; set; } = "";
        public string BaselinePlayerCoreVersion { get; set; } = "";
        public string CandidatePlayerCoreVersion { get; set; } = "";
        public string BaselineSourceRevision { get; set; } = "";
        public string CandidateSourceRevision { get; set; } = "";
        public string BaselineBuildConfiguration { get; set; } = "";
        public string CandidateBuildConfiguration { get; set; } = "";
    }

    public sealed class PlaybackQualityRenderPhaseExecutionContext
    {
        public string BaselineRunner { get; set; } = "";
        public string CandidateRunner { get; set; } = "";
        public string BaselineEvidenceLevel { get; set; } = "";
        public string CandidateEvidenceLevel { get; set; } = "";
        public string BaselineStatus { get; set; } = "";
        public string CandidateStatus { get; set; } = "";
    }

    public sealed class PlaybackQualityRenderPhaseSamples
    {
        public ulong MinimumRequiredProcessorSamples { get; set; } =
            PlaybackQualityRenderPhaseComparator.MinimumRequiredProcessorSamples;
        public ulong BaselineDirectCopyFrameCount { get; set; }
        public ulong CandidateDirectCopyFrameCount { get; set; }
        public ulong BaselineVideoProcessorFrameCount { get; set; }
        public ulong CandidateVideoProcessorFrameCount { get; set; }
        public ulong BaselineBgraFrameCount { get; set; }
        public ulong CandidateBgraFrameCount { get; set; }
        public ulong BaselinePostProcessFrameCount { get; set; }
        public ulong CandidatePostProcessFrameCount { get; set; }
        public PlaybackQualityRenderPhaseSamplePair Setup { get; set; } = new PlaybackQualityRenderPhaseSamplePair();
        public PlaybackQualityRenderPhaseSamplePair ViewTarget { get; set; } = new PlaybackQualityRenderPhaseSamplePair();
        public PlaybackQualityRenderPhaseSamplePair Clear { get; set; } = new PlaybackQualityRenderPhaseSamplePair();
        public PlaybackQualityRenderPhaseSamplePair Blt { get; set; } = new PlaybackQualityRenderPhaseSamplePair();
        public PlaybackQualityRenderPhaseSamplePair PostProcess { get; set; } = new PlaybackQualityRenderPhaseSamplePair();
    }

    public sealed class PlaybackQualityRenderPhaseSamplePair
    {
        public ulong Baseline { get; set; }
        public ulong Candidate { get; set; }
    }

    public sealed class PlaybackQualityRenderPhaseTransportContext
    {
        public string BaselineProvider { get; set; } = "";
        public string CandidateProvider { get; set; } = "";
        public double? BaselineReadWaitMs { get; set; }
        public double? CandidateReadWaitMs { get; set; }
        public double? BaselineSeekWaitMs { get; set; }
        public double? CandidateSeekWaitMs { get; set; }
    }

    public sealed class PlaybackQualityRenderPhaseMetricDelta
    {
        public string Signal { get; set; } = "";
        public string Unit { get; set; } = "ms";
        public bool LowerIsBetter { get; set; } = true;
        public double Baseline { get; set; }
        public double Candidate { get; set; }
        public double AbsoluteDelta { get; set; }
        public double? CandidateToBaselineRatio { get; set; }
        public double? PercentChange { get; set; }
        public string Direction { get; set; } = "unchanged";
    }

    public static class PlaybackQualityRenderPhaseComparator
    {
        public const ulong MinimumRequiredProcessorSamples = 30;
        private const double ComparisonEpsilon = 0.000001;

        public static PlaybackQualityRenderPhaseComparison Compare(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate,
            string caseId,
            int repeatIndex)
        {
            if (baseline == null)
            {
                throw new ArgumentNullException(nameof(baseline));
            }

            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var result = CreateResult(baseline, candidate, caseId, repeatIndex);
            ValidateIdentity(baseline, candidate, result);
            ValidateSamples(baseline, candidate, result);
            if (result.Blockers.Count > 0)
            {
                return result;
            }

            AddMetricSet(result, "timing.videoRenderDurationMs",
                baseline.Timing.VideoRenderDurationMsP50,
                baseline.Timing.VideoRenderDurationMsP95,
                baseline.Timing.VideoRenderDurationMsP99,
                baseline.Timing.VideoRenderDurationMsMax,
                candidate.Timing.VideoRenderDurationMsP50,
                candidate.Timing.VideoRenderDurationMsP95,
                candidate.Timing.VideoRenderDurationMsP99,
                candidate.Timing.VideoRenderDurationMsMax);
            AddMetricSet(result, "timing.videoProcessorSetupCpuDurationMs",
                baseline.Timing.VideoProcessorSetupCpuDurationMsP50,
                baseline.Timing.VideoProcessorSetupCpuDurationMsP95,
                baseline.Timing.VideoProcessorSetupCpuDurationMsP99,
                baseline.Timing.VideoProcessorSetupCpuDurationMsMax,
                candidate.Timing.VideoProcessorSetupCpuDurationMsP50,
                candidate.Timing.VideoProcessorSetupCpuDurationMsP95,
                candidate.Timing.VideoProcessorSetupCpuDurationMsP99,
                candidate.Timing.VideoProcessorSetupCpuDurationMsMax);
            AddMetricSet(result, "timing.videoProcessorViewTargetCpuDurationMs",
                baseline.Timing.VideoProcessorViewTargetCpuDurationMsP50,
                baseline.Timing.VideoProcessorViewTargetCpuDurationMsP95,
                baseline.Timing.VideoProcessorViewTargetCpuDurationMsP99,
                baseline.Timing.VideoProcessorViewTargetCpuDurationMsMax,
                candidate.Timing.VideoProcessorViewTargetCpuDurationMsP50,
                candidate.Timing.VideoProcessorViewTargetCpuDurationMsP95,
                candidate.Timing.VideoProcessorViewTargetCpuDurationMsP99,
                candidate.Timing.VideoProcessorViewTargetCpuDurationMsMax);
            AddMetricSet(result, "timing.videoProcessorClearCpuDurationMs",
                baseline.Timing.VideoProcessorClearCpuDurationMsP50,
                baseline.Timing.VideoProcessorClearCpuDurationMsP95,
                baseline.Timing.VideoProcessorClearCpuDurationMsP99,
                baseline.Timing.VideoProcessorClearCpuDurationMsMax,
                candidate.Timing.VideoProcessorClearCpuDurationMsP50,
                candidate.Timing.VideoProcessorClearCpuDurationMsP95,
                candidate.Timing.VideoProcessorClearCpuDurationMsP99,
                candidate.Timing.VideoProcessorClearCpuDurationMsMax);
            AddMetricSet(result, "timing.videoProcessorBltCpuDurationMs",
                baseline.Timing.VideoProcessorBltCpuDurationMsP50,
                baseline.Timing.VideoProcessorBltCpuDurationMsP95,
                baseline.Timing.VideoProcessorBltCpuDurationMsP99,
                baseline.Timing.VideoProcessorBltCpuDurationMsMax,
                candidate.Timing.VideoProcessorBltCpuDurationMsP50,
                candidate.Timing.VideoProcessorBltCpuDurationMsP95,
                candidate.Timing.VideoProcessorBltCpuDurationMsP99,
                candidate.Timing.VideoProcessorBltCpuDurationMsMax);
            AddMetricSet(result, "timing.videoProcessorPostProcessCpuDurationMs",
                baseline.Timing.VideoProcessorPostProcessCpuDurationMsP50,
                baseline.Timing.VideoProcessorPostProcessCpuDurationMsP95,
                baseline.Timing.VideoProcessorPostProcessCpuDurationMsP99,
                baseline.Timing.VideoProcessorPostProcessCpuDurationMsMax,
                candidate.Timing.VideoProcessorPostProcessCpuDurationMsP50,
                candidate.Timing.VideoProcessorPostProcessCpuDurationMsP95,
                candidate.Timing.VideoProcessorPostProcessCpuDurationMsP99,
                candidate.Timing.VideoProcessorPostProcessCpuDurationMsMax);

            var hasLower = false;
            var hasHigher = false;
            foreach (var metric in result.Metrics)
            {
                hasLower |= metric.Direction == "lower";
                hasHigher |= metric.Direction == "higher";
            }

            result.Status = "comparable";
            result.Result = hasLower && hasHigher
                ? "mixed"
                : hasLower
                    ? "improved"
                    : hasHigher
                        ? "regressed"
                        : "unchanged";
            return result;
        }

        private static PlaybackQualityRenderPhaseComparison CreateResult(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate,
            string caseId,
            int repeatIndex)
        {
            return new PlaybackQualityRenderPhaseComparison
            {
                CaseId = caseId ?? "",
                RepeatIndex = repeatIndex,
                BaselineRunId = baseline.RunId ?? "",
                CandidateRunId = candidate.RunId ?? "",
                Source = new PlaybackQualityRenderPhaseSourceIdentity
                {
                    OpenedSourceHash = baseline.Execution.OpenedSourceHash ?? "",
                    OpenedSourceHashKind = baseline.Execution.OpenedSourceHashKind ?? "",
                    ColorExpectationProfile = baseline.ColorPipeline.ExpectationProfile ?? ""
                },
                Builds = new PlaybackQualityRenderPhaseBuildIdentity
                {
                    BaselineCollectorVersion = baseline.Environment.CollectorVersion ?? "",
                    CandidateCollectorVersion = candidate.Environment.CollectorVersion ?? "",
                    BaselinePlayerCoreVersion = baseline.Environment.PlayerCoreVersion ?? "",
                    CandidatePlayerCoreVersion = candidate.Environment.PlayerCoreVersion ?? "",
                    BaselineSourceRevision = baseline.Environment.SourceRevision ?? "",
                    CandidateSourceRevision = candidate.Environment.SourceRevision ?? "",
                    BaselineBuildConfiguration = baseline.Environment.BuildConfiguration ?? "",
                    CandidateBuildConfiguration = candidate.Environment.BuildConfiguration ?? ""
                },
                Execution = new PlaybackQualityRenderPhaseExecutionContext
                {
                    BaselineRunner = baseline.Execution.Runner ?? "",
                    CandidateRunner = candidate.Execution.Runner ?? "",
                    BaselineEvidenceLevel = baseline.Execution.EvidenceLevel ?? "",
                    CandidateEvidenceLevel = candidate.Execution.EvidenceLevel ?? "",
                    BaselineStatus = baseline.Execution.Status ?? "",
                    CandidateStatus = candidate.Execution.Status ?? ""
                },
                Samples = CreateSamples(baseline, candidate),
                Transport = new PlaybackQualityRenderPhaseTransportContext
                {
                    BaselineProvider = baseline.Buffers.PlaybackTransportProvider ?? "",
                    CandidateProvider = candidate.Buffers.PlaybackTransportProvider ?? "",
                    BaselineReadWaitMs = baseline.Buffers.PlaybackTransportReadWaitMs,
                    CandidateReadWaitMs = candidate.Buffers.PlaybackTransportReadWaitMs,
                    BaselineSeekWaitMs = baseline.Buffers.PlaybackTransportSeekWaitMs,
                    CandidateSeekWaitMs = candidate.Buffers.PlaybackTransportSeekWaitMs
                }
            };
        }

        private static PlaybackQualityRenderPhaseSamples CreateSamples(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate)
        {
            return new PlaybackQualityRenderPhaseSamples
            {
                BaselineDirectCopyFrameCount = baseline.Timing.VideoRenderDirectCopyFrameCount,
                CandidateDirectCopyFrameCount = candidate.Timing.VideoRenderDirectCopyFrameCount,
                BaselineVideoProcessorFrameCount = baseline.Timing.VideoRenderVideoProcessorFrameCount,
                CandidateVideoProcessorFrameCount = candidate.Timing.VideoRenderVideoProcessorFrameCount,
                BaselineBgraFrameCount = baseline.Timing.VideoRenderBgraFrameCount,
                CandidateBgraFrameCount = candidate.Timing.VideoRenderBgraFrameCount,
                BaselinePostProcessFrameCount = baseline.Timing.VideoRenderPostProcessFrameCount,
                CandidatePostProcessFrameCount = candidate.Timing.VideoRenderPostProcessFrameCount,
                Setup = Pair(baseline.Timing.VideoProcessorSetupCpuSampleCount, candidate.Timing.VideoProcessorSetupCpuSampleCount),
                ViewTarget = Pair(baseline.Timing.VideoProcessorViewTargetCpuSampleCount, candidate.Timing.VideoProcessorViewTargetCpuSampleCount),
                Clear = Pair(baseline.Timing.VideoProcessorClearCpuSampleCount, candidate.Timing.VideoProcessorClearCpuSampleCount),
                Blt = Pair(baseline.Timing.VideoProcessorBltCpuSampleCount, candidate.Timing.VideoProcessorBltCpuSampleCount),
                PostProcess = Pair(baseline.Timing.VideoProcessorPostProcessCpuSampleCount, candidate.Timing.VideoProcessorPostProcessCpuSampleCount)
            };
        }

        private static PlaybackQualityRenderPhaseSamplePair Pair(ulong baseline, ulong candidate)
        {
            return new PlaybackQualityRenderPhaseSamplePair
            {
                Baseline = baseline,
                Candidate = candidate
            };
        }

        private static void ValidateIdentity(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate,
            PlaybackQualityRenderPhaseComparison result)
        {
            if (string.IsNullOrWhiteSpace(baseline.Execution.OpenedSourceHash) ||
                string.IsNullOrWhiteSpace(candidate.Execution.OpenedSourceHash))
            {
                AddUnique(result.Blockers, "execution.openedSourceHash.missing");
            }
            else if (!string.Equals(
                baseline.Execution.OpenedSourceHash,
                candidate.Execution.OpenedSourceHash,
                StringComparison.Ordinal))
            {
                AddUnique(result.Blockers, "execution.openedSourceHash.mismatch");
            }

            if (!string.IsNullOrWhiteSpace(baseline.Execution.OpenedSourceHash) &&
                (!string.Equals(
                     baseline.Execution.OpenedSourceHash,
                     PlaybackQualitySourceFingerprint.ComputeOpenedMediaSignature(baseline),
                     StringComparison.Ordinal) ||
                 !string.Equals(
                     candidate.Execution.OpenedSourceHash,
                     PlaybackQualitySourceFingerprint.ComputeOpenedMediaSignature(candidate),
                     StringComparison.Ordinal)))
            {
                AddUnique(result.Blockers, "execution.openedSourceHash.invalid");
            }

            if (!string.Equals(
                    baseline.Execution.OpenedSourceHashKind,
                    PlaybackQualitySourceFingerprint.OpenedMediaSignatureKind,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    candidate.Execution.OpenedSourceHashKind,
                    PlaybackQualitySourceFingerprint.OpenedMediaSignatureKind,
                    StringComparison.Ordinal))
            {
                AddUnique(result.Blockers, "execution.openedSourceHashKind.invalid");
            }

            if (string.IsNullOrWhiteSpace(baseline.ColorPipeline.ExpectationProfile) ||
                string.IsNullOrWhiteSpace(candidate.ColorPipeline.ExpectationProfile))
            {
                AddUnique(result.Blockers, "colorPipeline.expectationProfile.missing");
            }
            else if (!string.Equals(
                baseline.ColorPipeline.ExpectationProfile,
                candidate.ColorPipeline.ExpectationProfile,
                StringComparison.Ordinal))
            {
                AddUnique(result.Blockers, "colorPipeline.expectationProfile.mismatch");
            }

            if (string.IsNullOrWhiteSpace(baseline.Execution.Runner) ||
                string.IsNullOrWhiteSpace(candidate.Execution.Runner))
            {
                AddUnique(result.Blockers, "execution.runner.missing");
            }
            else if (!string.Equals(
                baseline.Execution.Runner,
                candidate.Execution.Runner,
                StringComparison.Ordinal))
            {
                AddUnique(result.Blockers, "execution.runner.mismatch");
            }

            if (!HasCompletedNativePlayback(baseline.Execution) ||
                !HasCompletedNativePlayback(candidate.Execution))
            {
                AddUnique(result.Blockers, "execution.nativePlayback.incomplete");
            }

            if (string.IsNullOrWhiteSpace(baseline.Environment.CollectorVersion) ||
                string.IsNullOrWhiteSpace(candidate.Environment.CollectorVersion))
            {
                AddUnique(result.Blockers, "environment.collectorVersion.missing");
            }
            else if (!string.Equals(
                baseline.Environment.CollectorVersion,
                candidate.Environment.CollectorVersion,
                StringComparison.Ordinal))
            {
                AddUnique(result.Blockers, "environment.collectorVersion.mismatch");
            }

            if (string.IsNullOrWhiteSpace(baseline.Environment.BuildConfiguration) ||
                string.IsNullOrWhiteSpace(candidate.Environment.BuildConfiguration))
            {
                AddUnique(result.Blockers, "environment.buildConfiguration.missing");
            }
            else if (!string.Equals(
                baseline.Environment.BuildConfiguration,
                candidate.Environment.BuildConfiguration,
                StringComparison.Ordinal))
            {
                AddUnique(result.Blockers, "environment.buildConfiguration.mismatch");
            }

            var baselineBuildPresent =
                !string.IsNullOrWhiteSpace(baseline.Environment.PlayerCoreVersion) &&
                !string.IsNullOrWhiteSpace(baseline.Environment.SourceRevision);
            var candidateBuildPresent =
                !string.IsNullOrWhiteSpace(candidate.Environment.PlayerCoreVersion) &&
                !string.IsNullOrWhiteSpace(candidate.Environment.SourceRevision);
            if (!baselineBuildPresent || !candidateBuildPresent)
            {
                AddUnique(result.Blockers, "environment.buildIdentity.missing");
            }
            else if (string.Equals(
                    baseline.Environment.PlayerCoreVersion,
                    candidate.Environment.PlayerCoreVersion,
                    StringComparison.Ordinal) &&
                string.Equals(
                    baseline.Environment.SourceRevision,
                    candidate.Environment.SourceRevision,
                    StringComparison.Ordinal))
            {
                AddUnique(result.Blockers, "environment.buildIdentity.same");
            }
        }

        private static void ValidateSamples(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate,
            PlaybackQualityRenderPhaseComparison result)
        {
            ValidateProcessorSamplePair(
                "timing.videoProcessorSetupCpuSampleCount",
                baseline.Timing.VideoProcessorSetupCpuSampleCount,
                candidate.Timing.VideoProcessorSetupCpuSampleCount,
                baseline.Timing.VideoRenderVideoProcessorFrameCount,
                candidate.Timing.VideoRenderVideoProcessorFrameCount,
                result);
            ValidateProcessorSamplePair(
                "timing.videoProcessorViewTargetCpuSampleCount",
                baseline.Timing.VideoProcessorViewTargetCpuSampleCount,
                candidate.Timing.VideoProcessorViewTargetCpuSampleCount,
                baseline.Timing.VideoRenderVideoProcessorFrameCount,
                candidate.Timing.VideoRenderVideoProcessorFrameCount,
                result);
            ValidateProcessorSamplePair(
                "timing.videoProcessorClearCpuSampleCount",
                baseline.Timing.VideoProcessorClearCpuSampleCount,
                candidate.Timing.VideoProcessorClearCpuSampleCount,
                baseline.Timing.VideoRenderVideoProcessorFrameCount,
                candidate.Timing.VideoRenderVideoProcessorFrameCount,
                result);
            ValidateProcessorSamplePair(
                "timing.videoProcessorBltCpuSampleCount",
                baseline.Timing.VideoProcessorBltCpuSampleCount,
                candidate.Timing.VideoProcessorBltCpuSampleCount,
                baseline.Timing.VideoRenderVideoProcessorFrameCount,
                candidate.Timing.VideoRenderVideoProcessorFrameCount,
                result);

            ValidatePostProcessSamples(baseline, candidate, result);
        }

        private static bool HasCompletedNativePlayback(PlaybackQualityExecutionEvidence execution)
        {
            return string.Equals(
                    execution.EvidenceLevel,
                    PlaybackQualityEvidenceLevel.NativePlayback,
                    StringComparison.Ordinal) &&
                string.Equals(
                    execution.Status,
                    PlaybackQualityExecutionStatus.Completed,
                    StringComparison.Ordinal) &&
                execution.SourceOpened &&
                execution.NativeGraphOpened &&
                execution.DemuxStarted &&
                execution.DecoderOpened &&
                execution.PlaybackSampleObserved;
        }

        private static void ValidateProcessorSamplePair(
            string signal,
            ulong baselineSamples,
            ulong candidateSamples,
            ulong baselineFrames,
            ulong candidateFrames,
            PlaybackQualityRenderPhaseComparison result)
        {
            if (baselineSamples < MinimumRequiredProcessorSamples ||
                candidateSamples < MinimumRequiredProcessorSamples)
            {
                AddUnique(result.Blockers, signal + ".insufficient");
            }

            if (baselineSamples != baselineFrames || candidateSamples != candidateFrames)
            {
                AddUnique(result.Blockers, signal + ".frame-count-mismatch");
            }
        }

        private static void ValidatePostProcessSamples(
            PlaybackQualityReport baseline,
            PlaybackQualityReport candidate,
            PlaybackQualityRenderPhaseComparison result)
        {
            if (baseline.Timing.VideoProcessorPostProcessCpuSampleCount !=
                    baseline.Timing.VideoRenderPostProcessFrameCount ||
                candidate.Timing.VideoProcessorPostProcessCpuSampleCount !=
                    candidate.Timing.VideoRenderPostProcessFrameCount)
            {
                AddUnique(result.Blockers, "timing.videoProcessorPostProcessCpuSampleCount.frame-count-mismatch");
            }

            var baselineUsesPostProcess = baseline.Timing.VideoRenderPostProcessFrameCount > 0;
            var candidateUsesPostProcess = candidate.Timing.VideoRenderPostProcessFrameCount > 0;
            if (baselineUsesPostProcess != candidateUsesPostProcess)
            {
                AddUnique(result.Blockers, "timing.videoRenderPostProcessFrameCount.path-mismatch");
            }
            else if (baselineUsesPostProcess &&
                (baseline.Timing.VideoProcessorPostProcessCpuSampleCount < MinimumRequiredProcessorSamples ||
                 candidate.Timing.VideoProcessorPostProcessCpuSampleCount < MinimumRequiredProcessorSamples))
            {
                AddUnique(result.Blockers, "timing.videoProcessorPostProcessCpuSampleCount.insufficient");
            }
        }

        private static void AddMetricSet(
            PlaybackQualityRenderPhaseComparison result,
            string prefix,
            double baselineP50,
            double baselineP95,
            double baselineP99,
            double baselineMax,
            double candidateP50,
            double candidateP95,
            double candidateP99,
            double candidateMax)
        {
            AddMetric(result, prefix + "P50", baselineP50, candidateP50);
            AddMetric(result, prefix + "P95", baselineP95, candidateP95);
            AddMetric(result, prefix + "P99", baselineP99, candidateP99);
            AddMetric(result, prefix + "Max", baselineMax, candidateMax);
        }

        private static void AddMetric(
            PlaybackQualityRenderPhaseComparison result,
            string signal,
            double baseline,
            double candidate)
        {
            var delta = candidate - baseline;
            result.Metrics.Add(new PlaybackQualityRenderPhaseMetricDelta
            {
                Signal = signal,
                Baseline = baseline,
                Candidate = candidate,
                AbsoluteDelta = delta,
                CandidateToBaselineRatio = Math.Abs(baseline) <= ComparisonEpsilon
                    ? null
                    : candidate / baseline,
                PercentChange = Math.Abs(baseline) <= ComparisonEpsilon
                    ? null
                    : delta / baseline * 100.0,
                Direction = delta < -ComparisonEpsilon
                    ? "lower"
                    : delta > ComparisonEpsilon
                        ? "higher"
                        : "unchanged"
            });
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
