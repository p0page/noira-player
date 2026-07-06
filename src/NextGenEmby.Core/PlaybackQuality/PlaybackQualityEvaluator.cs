using System.Globalization;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityEvaluator
    {
        public static void Evaluate(PlaybackQualityReport report)
        {
            report.FailureReasons.Clear();
            report.Analysis = new PlaybackQualityAnalysis();

            if (report.Expected == null)
            {
                report.Result = "observed";
                report.Analysis.PrimaryFailureArea = "none";
                report.Analysis.SuggestedNextAction = "No thresholds supplied; inspect raw metrics only.";
                report.Analysis.IgnoredSignals.Add("expected.* thresholds");
                return;
            }

            var expected = report.Expected;
            CheckMax(
                report,
                "DroppedVideoFrames",
                (long)report.Timing.DroppedVideoFrames,
                expected.MaxDroppedFrames,
                "MaxDroppedFrames",
                "timing.droppedVideoFrames");
            CheckMax(
                report,
                "MaxFrameGapMs",
                report.Timing.MaxFrameGapMs,
                expected.MaxFrameGapMs,
                "MaxFrameGapMs",
                "timing.maxFrameGapMs");
            CheckMax(
                report,
                "AudioVideoDriftMsP95",
                report.Sync.AudioVideoDriftMsP95,
                expected.MaxAudioVideoDriftMsP95,
                "MaxAudioVideoDriftMsP95",
                "sync.audioVideoDriftMsP95");
            CheckMax(
                report,
                "VideoStarvedPasses",
                (long)report.Buffers.VideoStarvedPasses,
                expected.MaxVideoStarvedPasses,
                "MaxVideoStarvedPasses",
                "buffers.videoStarvedPasses");
            CheckMax(
                report,
                "AudioStarvedPasses",
                (long)report.Buffers.AudioStarvedPasses,
                expected.MaxAudioStarvedPasses,
                "MaxAudioStarvedPasses",
                "buffers.audioStarvedPasses");
            CheckEquals(
                report,
                "ActualHdrOutput",
                report.ColorPipeline.ActualHdrOutput,
                expected.HdrOutput,
                "colorPipeline.actualHdrOutput");
            CheckEquals(
                report,
                "DxgiInput",
                report.ColorPipeline.DxgiInput,
                expected.DxgiInput,
                "colorPipeline.dxgiInput");
            CheckEquals(
                report,
                "DxgiOutput",
                report.ColorPipeline.DxgiOutput,
                expected.DxgiOutput,
                "colorPipeline.dxgiOutput");

            if (expected.RequireValidatedConversion &&
                report.ColorPipeline.ConversionStatus != "validated" &&
                report.ColorPipeline.ConversionStatus != "validated;tone-mapped-hable")
            {
                report.FailureReasons.Add(
                    "ConversionStatus " + report.ColorPipeline.ConversionStatus + " is not validated.");
                AddRelevantSignal(report, "colorPipeline.conversionStatus");
            }

            report.Result = report.FailureReasons.Count == 0 ? "pass" : "fail";
            if (report.Result == "pass")
            {
                report.Analysis.PrimaryFailureArea = "none";
                report.Analysis.SuggestedNextAction = "No failing thresholds.";
                return;
            }

            AssignFailureAnalysis(report);
        }

        private static void CheckMax(
            PlaybackQualityReport report,
            string metricName,
            long actual,
            long? max,
            string thresholdName,
            string signal)
        {
            if (max.HasValue && actual > max.Value)
            {
                report.FailureReasons.Add(
                    metricName + " " + actual + " exceeded " + thresholdName + " " + max.Value + ".");
                AddRelevantSignal(report, signal);
            }
        }

        private static void CheckMax(
            PlaybackQualityReport report,
            string metricName,
            double actual,
            double? max,
            string thresholdName,
            string signal)
        {
            if (max.HasValue && actual > max.Value)
            {
                report.FailureReasons.Add(
                    metricName + " " + Format(actual) + " exceeded " + thresholdName + " " + Format(max.Value) + ".");
                AddRelevantSignal(report, signal);
            }
        }

        private static void CheckEquals(
            PlaybackQualityReport report,
            string name,
            string actual,
            string expected,
            string signal)
        {
            if (!string.IsNullOrWhiteSpace(expected) &&
                !string.Equals(actual, expected, System.StringComparison.Ordinal))
            {
                report.FailureReasons.Add(name + " " + actual + " did not match expected " + expected + ".");
                AddRelevantSignal(report, signal);
            }
        }

        private static void AssignFailureAnalysis(PlaybackQualityReport report)
        {
            if (HasReason(report, "unsupported"))
            {
                report.Analysis.PrimaryFailureArea = "unsupported-source";
                report.Analysis.SuggestedNextAction = "Inspect container, codec, Dolby Vision profile, and media source selection.";
                return;
            }

            if (HasReason(report, "ActualHdrOutput", "DxgiInput", "DxgiOutput", "ConversionStatus"))
            {
                report.Analysis.PrimaryFailureArea = "color-pipeline";
                report.Analysis.SuggestedNextAction = "Inspect HDR display switch and DXGI color-space mapping.";
                return;
            }

            if (HasReason(report, "StartupDurationMs"))
            {
                report.Analysis.PrimaryFailureArea = "startup";
                report.Analysis.SuggestedNextAction = "Inspect Emby request, source open, demux initialization, and first-frame readiness.";
                return;
            }

            if (HasReason(report, "VideoStarvedPasses", "AudioStarvedPasses"))
            {
                report.Analysis.PrimaryFailureArea = "buffering";
                report.Analysis.SuggestedNextAction = "Inspect demux/network stalls before changing render pacing.";
                return;
            }

            if (HasReason(report, "AudioVideoDriftMsP95"))
            {
                report.Analysis.PrimaryFailureArea = "av-sync";
                report.Analysis.SuggestedNextAction = "Inspect audio renderer clock and queued buffer depth.";
                return;
            }

            if (HasReason(report, "DroppedVideoFrames", "MaxFrameGapMs"))
            {
                report.Analysis.PrimaryFailureArea = "frame-pacing";
                report.Analysis.SuggestedNextAction = "Inspect frame pacing wait/drop thresholds around PlaybackFramePacing.";
                return;
            }

            report.Analysis.PrimaryFailureArea = "unknown";
            report.Analysis.SuggestedNextAction = "Inspect raw metrics and failure reasons.";
        }

        private static bool HasReason(PlaybackQualityReport report, params string[] fragments)
        {
            foreach (var reason in report.FailureReasons)
            {
                foreach (var fragment in fragments)
                {
                    if (reason.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddRelevantSignal(PlaybackQualityReport report, string signal)
        {
            if (!report.Analysis.RelevantSignals.Contains(signal))
            {
                report.Analysis.RelevantSignals.Add(signal);
            }
        }

        private static string Format(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
