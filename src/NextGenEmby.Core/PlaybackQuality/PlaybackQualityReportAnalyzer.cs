using System.Collections.Generic;

namespace NextGenEmby.Core.PlaybackQuality
{
    public sealed class PlaybackQualityModelAnalysis
    {
        public string RunId { get; set; } = "";
        public string Result { get; set; } = "";
        public string PrimaryFailureArea { get; set; } = "none";
        public string SuggestedNextAction { get; set; } = "";
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

            AddDerivedEvidence(analysis, report);
            AddMissingEvidence(analysis, report);
            AddInvestigationHints(analysis);

            if (string.IsNullOrWhiteSpace(analysis.SuggestedNextAction))
            {
                analysis.SuggestedNextAction = analysis.MissingEvidence.Count == 0
                    ? "Inspect report checks and raw metrics."
                    : "Collect missing evidence before optimizing playback core.";
            }

            return analysis;
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

            if (areas.Count == 0 && analysis.MissingEvidence.Count > 0)
            {
                AddUnique(areas, "evidence-collection");
            }

            foreach (var area in areas)
            {
                var hint = CreateInvestigationHint(area);
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

        private static PlaybackQualityInvestigationHint? CreateInvestigationHint(string area)
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
