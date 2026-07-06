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

            if (report.Sync.AudioVideoDriftMsP95 <= 0 && report.Timing.RenderedVideoFrames == 0)
            {
                analysis.MissingEvidence.Add("sync.audioVideoDriftMsP95");
            }

            if (report.Buffers.QueuedAudioBuffers == 0 && report.Timing.RenderedVideoFrames == 0)
            {
                analysis.MissingEvidence.Add("buffers.queuedAudioBuffers");
            }

            if (string.IsNullOrWhiteSpace(report.ColorPipeline.DxgiInput))
            {
                analysis.MissingEvidence.Add("colorPipeline.dxgiInput");
            }

            if (string.IsNullOrWhiteSpace(report.Display.HdrStatus))
            {
                analysis.MissingEvidence.Add("display.hdrStatus");
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
