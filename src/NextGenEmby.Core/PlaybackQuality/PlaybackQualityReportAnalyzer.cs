using System.Collections.Generic;

namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityModelAnalysis
    {
        public string RunId { get; set; } = "";
        public string Result { get; set; } = "";
        public string PrimaryFailureArea { get; set; } = "none";
        public string SuggestedNextAction { get; set; } = "";
        public PlaybackQualitySampleAssessment Sample { get; set; } = new PlaybackQualitySampleAssessment();
        public PlaybackQualityOptimizationGate OptimizationGate { get; set; } = new PlaybackQualityOptimizationGate();
        public PlaybackQualityFramePacingClassification FramePacing { get; set; } = new PlaybackQualityFramePacingClassification();
        public List<string> FailureReasons { get; } = new List<string>();
        public List<PlaybackQualityCheck> FailedChecks { get; } = new List<PlaybackQualityCheck>();
        public List<string> FailureAreas { get; } = new List<string>();
        public List<PlaybackQualityInvestigationHint> InvestigationHints { get; } = new List<PlaybackQualityInvestigationHint>();
        public List<string> EvidenceSignals { get; } = new List<string>();
        public List<string> MissingEvidence { get; } = new List<string>();
        public List<string> Limitations { get; } = new List<string>();
    }

    public sealed class PlaybackQualityInvestigationHint
    {
        public string FailureArea { get; set; } = "";
        public string SuggestedAction { get; set; } = "";
        public List<string> CodeTargets { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
    }

    public sealed class PlaybackQualitySampleAssessment
    {
        public string Status { get; set; } = "unknown";
        public ulong RenderedVideoFrames { get; set; }
        public long? MinRenderedVideoFrames { get; set; }
        public string Reason { get; set; } = "";
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
            AddDerivedEvidence(analysis, report);
            AddMissingEvidence(analysis, report);
            analysis.OptimizationGate = AssessOptimizationGate(analysis);
            analysis.FramePacing = ClassifyFramePacing(analysis);
            AddInvestigationHints(analysis);

            if (string.IsNullOrWhiteSpace(analysis.SuggestedNextAction))
            {
                analysis.SuggestedNextAction = analysis.MissingEvidence.Count == 0
                    ? "Inspect report checks and raw metrics."
                    : "Collect missing evidence before optimizing playback core.";
            }

            return analysis;
        }

        private static PlaybackQualityFramePacingClassification ClassifyFramePacing(
            PlaybackQualityModelAnalysis analysis)
        {
            var classification = new PlaybackQualityFramePacingClassification();
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

            if (gate.Blockers.Count > 0)
            {
                gate.Status = "blocked";
                return gate;
            }

            gate.Status = "ready";
            gate.CanOptimizePlaybackCore = true;
            foreach (var area in analysis.FailureAreas)
            {
                AddUnique(gate.TargetFailureAreas, area);
            }

            return gate;
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
                            "source.hdrKind"
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
