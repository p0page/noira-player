using System;
using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityRuntimeEvidenceCollector
    {
        public static PlaybackQualityReportRequest CreateRequest(
            PlaybackQualityReferenceCase referenceCase,
            PlaybackDescriptor descriptor,
            IPlaybackBackendDiagnostics? diagnostics = null,
            IPlaybackQualityMetricsProvider? metricsProvider = null,
            PlaybackQualityStartup? startup = null,
            PlaybackQualityEnvironment? environment = null,
            PlaybackQualityLifecycle? lifecycle = null)
        {
            if (referenceCase == null)
            {
                throw new ArgumentNullException(nameof(referenceCase));
            }

            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            PlaybackQualityMetricsSnapshot? metrics = null;
            PlaybackQualityRuntimeMetrics runtimeMetrics;
            var provider = metricsProvider ?? diagnostics as IPlaybackQualityMetricsProvider;
            if (provider == null)
            {
                runtimeMetrics = PlaybackQualityRuntimeMetricsFactory.Unavailable("not-provided");
            }
            else if (provider.TryGetQualityMetrics(out var snapshot))
            {
                metrics = snapshot;
                runtimeMetrics = PlaybackQualityRuntimeMetricsFactory.FromSnapshot(
                    snapshot,
                    ComposeProviderStatus(provider, "returned-snapshot"));
            }
            else
            {
                runtimeMetrics = PlaybackQualityRuntimeMetricsFactory.Unavailable(
                    ComposeProviderStatus(provider, "returned-false"));
            }

            var request = PlaybackQualityReferenceCaseReportRequestFactory.CreateRequest(
                referenceCase,
                descriptor,
                diagnostics?.DisplayStatus,
                metrics,
                startup);
            request.RuntimeMetrics = runtimeMetrics;
            request.Lifecycle = lifecycle;
            request.Environment = environment;
            return request;
        }

        private static string ComposeProviderStatus(
            IPlaybackQualityMetricsProvider provider,
            string outcome)
        {
            if (provider is IPlaybackQualityMetricsProviderIdentity identity &&
                !string.IsNullOrWhiteSpace(identity.PlaybackQualityMetricsProviderId))
            {
                return identity.PlaybackQualityMetricsProviderId + ":" + outcome;
            }

            return outcome;
        }

        public static PlaybackQualityRunResult ComposeRunResult(
            PlaybackQualityReferenceCase referenceCase,
            PlaybackDescriptor descriptor,
            IPlaybackBackendDiagnostics? diagnostics = null,
            IPlaybackQualityMetricsProvider? metricsProvider = null,
            PlaybackQualityStartup? startup = null,
            PlaybackQualityEnvironment? environment = null,
            PlaybackQualityLifecycle? lifecycle = null)
        {
            return PlaybackQualityReportComposer.Compose(
                CreateRequest(
                    referenceCase,
                    descriptor,
                    diagnostics,
                    metricsProvider,
                    startup,
                    environment,
                    lifecycle));
        }

        public static PlaybackQualityRunResult ComposeErrorRunResult(
            PlaybackQualityReferenceCase referenceCase,
            PlaybackQualityError error,
            PlaybackQualityEnvironment? environment = null)
        {
            if (referenceCase == null)
            {
                throw new ArgumentNullException(nameof(referenceCase));
            }

            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            var report = new PlaybackQualityReport
            {
                RunId = referenceCase.CaseId ?? "",
                Expected = CloneExpected(referenceCase.Expected),
                Environment = MergeEnvironment(environment),
                Result = "error",
                Error = CloneError(error)
            };
            report.Error.FailureArea = string.IsNullOrWhiteSpace(report.Error.FailureArea)
                ? "error-handling"
                : report.Error.FailureArea;
            report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
            {
                Operation = string.IsNullOrWhiteSpace(report.Error.Operation)
                    ? "error"
                    : report.Error.Operation,
                Status = "error",
                Message = report.Error.Message
            });

            var failureClass = string.IsNullOrWhiteSpace(report.Error.FailureClass)
                ? "needs human confirmation"
                : report.Error.FailureClass;
            var message = FormatErrorMessage(report.Error);
            report.FailureReasons.Add(message);
            report.Analysis.PrimaryFailureArea = report.Error.FailureArea;
            report.Analysis.SuggestedNextAction =
                "Inspect runtime playback error evidence before interpreting playback-quality metrics.";
            AddErrorSignal(report, "error.code");
            AddErrorSignal(report, "error.message");
            AddErrorSignal(report, "error.operation");
            AddErrorSignal(report, "error.exceptionType");
            AddErrorSignal(report, "error.failureClass");
            AddErrorSignal(report, "error.failureArea");
            AddErrorSignal(report, "lifecycle.error");

            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = "PlaybackRuntimeError",
                Signal = "error.code",
                Status = "fail",
                FailureArea = report.Error.FailureArea,
                FailureClass = failureClass,
                Expected = "playback operation completed",
                Actual = report.Error.Code,
                Message = message
            });

            return new PlaybackQualityRunResult(
                report,
                PlaybackQualityReportAnalyzer.Analyze(report),
                CreateCaseMetadata(referenceCase));
        }

        public static PlaybackQualityRunResult ComposeSkipRunResult(
            PlaybackQualityReferenceCase referenceCase,
            PlaybackQualitySkip skip,
            PlaybackQualityEnvironment? environment = null)
        {
            if (referenceCase == null)
            {
                throw new ArgumentNullException(nameof(referenceCase));
            }

            if (skip == null)
            {
                throw new ArgumentNullException(nameof(skip));
            }

            var report = new PlaybackQualityReport
            {
                RunId = referenceCase.CaseId ?? "",
                Expected = CloneExpected(referenceCase.Expected),
                Environment = MergeEnvironment(environment),
                Result = "skip",
                Skip = CloneSkip(skip)
            };
            report.Skip.FailureArea = string.IsNullOrWhiteSpace(report.Skip.FailureArea)
                ? "evidence-collection"
                : report.Skip.FailureArea;
            report.Skip.FailureClass = string.IsNullOrWhiteSpace(report.Skip.FailureClass)
                ? "needs human confirmation"
                : report.Skip.FailureClass;
            report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
            {
                Operation = string.IsNullOrWhiteSpace(report.Skip.Operation)
                    ? "skip"
                    : report.Skip.Operation,
                Status = "skipped",
                Message = report.Skip.Reason
            });

            var message = FormatSkipMessage(report.Skip);
            report.FailureReasons.Add(message);
            report.Analysis.PrimaryFailureArea = report.Skip.FailureArea;
            report.Analysis.SuggestedNextAction =
                "Review the structured skip reason before interpreting missing playback telemetry.";
            AddSkipSignal(report, "skip.code");
            AddSkipSignal(report, "skip.reason");
            AddSkipSignal(report, "skip.operation");
            AddSkipSignal(report, "skip.failureClass");
            AddSkipSignal(report, "skip.failureArea");
            AddSkipSignal(report, "skip.isExpected");
            AddSkipSignal(report, "skip.isRetriable");
            AddSkipSignal(report, "lifecycle.skip");

            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = "PlaybackQualitySkipped",
                Signal = "skip.reason",
                Status = "skip",
                FailureArea = report.Skip.FailureArea,
                FailureClass = report.Skip.FailureClass,
                Expected = "reference case evaluated or explicitly skipped",
                Actual = string.IsNullOrWhiteSpace(report.Skip.Code)
                    ? report.Skip.Reason
                    : report.Skip.Code,
                Message = message
            });

            return new PlaybackQualityRunResult(
                report,
                PlaybackQualityReportAnalyzer.Analyze(report),
                CreateCaseMetadata(referenceCase));
        }

        private static void AddErrorSignal(
            PlaybackQualityReport report,
            string signal)
        {
            if (!report.Analysis.RelevantSignals.Contains(signal))
            {
                report.Analysis.RelevantSignals.Add(signal);
            }
        }

        private static string FormatErrorMessage(PlaybackQualityError error)
        {
            var code = string.IsNullOrWhiteSpace(error.Code)
                ? "runtime.error"
                : error.Code;
            if (string.IsNullOrWhiteSpace(error.Message))
            {
                return code;
            }

            return code + ": " + error.Message;
        }

        private static void AddSkipSignal(
            PlaybackQualityReport report,
            string signal)
        {
            if (!report.Analysis.RelevantSignals.Contains(signal))
            {
                report.Analysis.RelevantSignals.Add(signal);
            }
        }

        private static string FormatSkipMessage(PlaybackQualitySkip skip)
        {
            var code = string.IsNullOrWhiteSpace(skip.Code)
                ? "quality.skip"
                : skip.Code;
            if (string.IsNullOrWhiteSpace(skip.Reason))
            {
                return code;
            }

            return code + ": " + skip.Reason;
        }

        private static PlaybackQualityError CloneError(PlaybackQualityError source)
        {
            return new PlaybackQualityError
            {
                Code = source.Code,
                Message = source.Message,
                Operation = source.Operation,
                ExceptionType = source.ExceptionType,
                FailureClass = source.FailureClass,
                FailureArea = source.FailureArea,
                IsTerminal = source.IsTerminal,
                IsRetriable = source.IsRetriable
            };
        }

        private static PlaybackQualitySkip CloneSkip(PlaybackQualitySkip source)
        {
            return new PlaybackQualitySkip
            {
                Code = source.Code,
                Reason = source.Reason,
                Operation = source.Operation,
                FailureClass = source.FailureClass,
                FailureArea = source.FailureArea,
                IsExpected = source.IsExpected,
                IsRetriable = source.IsRetriable
            };
        }

        private static PlaybackQualityCaseMetadata CreateCaseMetadata(
            PlaybackQualityReferenceCase referenceCase)
        {
            return new PlaybackQualityCaseMetadata
            {
                CaseId = referenceCase.CaseId ?? "",
                Category = string.IsNullOrWhiteSpace(referenceCase.Category)
                    ? "stable"
                    : referenceCase.Category,
                Severity = string.IsNullOrWhiteSpace(referenceCase.Severity)
                    ? "medium"
                    : referenceCase.Severity,
                Stability = string.IsNullOrWhiteSpace(referenceCase.Stability)
                    ? "stable"
                    : referenceCase.Stability
            };
        }

        private static PlaybackQualityEnvironment MergeEnvironment(
            PlaybackQualityEnvironment? environment)
        {
            return new PlaybackQualityEnvironment
            {
                CollectorVersion = environment?.CollectorVersion ?? "",
                PlayerCoreVersion = environment?.PlayerCoreVersion ?? "",
                SourceRevision = environment?.SourceRevision ?? "",
                BuildConfiguration = environment?.BuildConfiguration ?? ""
            };
        }

        private static PlaybackQualityExpected CloneExpected(
            PlaybackQualityExpected source)
        {
            if (source == null)
            {
                return new PlaybackQualityExpected();
            }

            return new PlaybackQualityExpected
            {
                Codec = source.Codec,
                Width = source.Width,
                Height = source.Height,
                FrameRate = source.FrameRate,
                HdrKind = source.HdrKind,
                HdrPlaybackStrategy = source.HdrPlaybackStrategy,
                IsHdr = source.IsHdr,
                IsDirectPlayable = source.IsDirectPlayable,
                IsDolbyVision = source.IsDolbyVision,
                DolbyVisionProfile = source.DolbyVisionProfile,
                DolbyVisionCompatibilityId = source.DolbyVisionCompatibilityId,
                HasHdr10BaseLayer = source.HasHdr10BaseLayer,
                HasHlgBaseLayer = source.HasHlgBaseLayer,
                HdrOutput = source.HdrOutput,
                DxgiInput = source.DxgiInput,
                DxgiOutput = source.DxgiOutput,
                MaxStartupDurationMs = source.MaxStartupDurationMs,
                MinRenderedVideoFrames = source.MinRenderedVideoFrames,
                MaxDroppedFrames = source.MaxDroppedFrames,
                MaxFrameGapMs = source.MaxFrameGapMs,
                MaxRenderIntervalMsP95 = source.MaxRenderIntervalMsP95,
                MaxRenderIntervalMsP99 = source.MaxRenderIntervalMsP99,
                MaxAudioVideoDriftMsP95 = source.MaxAudioVideoDriftMsP95,
                MaxSeekPositionErrorMs = source.MaxSeekPositionErrorMs,
                MaxVideoStarvedPasses = source.MaxVideoStarvedPasses,
                MaxAudioStarvedPasses = source.MaxAudioStarvedPasses,
                RequireValidatedConversion = source.RequireValidatedConversion,
                RequireMatchedDisplayRefreshRate = source.RequireMatchedDisplayRefreshRate
            };
        }
    }
}
