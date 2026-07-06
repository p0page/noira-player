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
        public List<string> EvidenceSignals { get; } = new List<string>();
        public List<string> MissingEvidence { get; } = new List<string>();
        public List<string> Limitations { get; } = new List<string>();
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
