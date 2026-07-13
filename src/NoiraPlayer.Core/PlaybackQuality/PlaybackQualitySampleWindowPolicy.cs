using System;
using System.Globalization;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualitySampleWindowPolicy
    {
        public static double GetObservedMediaDurationMs(PlaybackQualityReport report)
        {
            if (report == null || report.Source.FrameRate <= 0)
            {
                return 0;
            }

            var observedFrames =
                (double)report.Timing.RenderedVideoFrames + report.Timing.DroppedVideoFrames;
            if (report.Execution != null &&
                string.Equals(
                    report.Execution.Scenario,
                    PlaybackQualityExecutionScenario.Timeline,
                    StringComparison.Ordinal) &&
                report.Position.SeekResetRuntimeMetrics == true)
            {
                observedFrames +=
                    report.Position.PreSeekRenderedVideoFrames.GetValueOrDefault() +
                    report.Position.PreSeekDroppedVideoFrames.GetValueOrDefault();
            }
            return observedFrames * 1000.0 / report.Source.FrameRate;
        }

        public static double GetRequiredMediaDurationMs(PlaybackQualityReport report)
        {
            if (report?.Execution == null || report.Execution.RequestedSampleDurationMs <= 0)
            {
                return 0;
            }

            var requestedDurationMs = report.Execution.RequestedSampleDurationMs;
            if (!HasCompletedNaturalEndOfStream(report) || report.Source.DurationTicks <= 0)
            {
                return requestedDurationMs;
            }

            var requestedStartTicks = Math.Max(
                0,
                report.Position.RequestedStartPositionTicks.GetValueOrDefault());
            var remainingTicks = Math.Max(0, report.Source.DurationTicks - requestedStartTicks);
            var remainingDurationMs = remainingTicks / (double)TimeSpan.TicksPerMillisecond;
            return Math.Min(requestedDurationMs, remainingDurationMs);
        }

        public static double GetCaptureBoundaryToleranceMs(PlaybackQualityReport report)
        {
            if (report == null || report.Source.FrameRate <= 0)
            {
                return 0;
            }

            return Math.Max(100.0, 1000.0 / report.Source.FrameRate);
        }

        public static bool HasCompletedNaturalEndOfStream(PlaybackQualityReport report)
        {
            if (report?.Execution == null ||
                !string.Equals(
                    report.Execution.Scenario,
                    PlaybackQualityExecutionScenario.EndOfStream,
                    StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var item in report.Lifecycle.Events)
            {
                if (item != null &&
                    string.Equals(item.Operation, "endOfStream", StringComparison.Ordinal) &&
                    string.Equals(item.Status, "completed", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasMatchingIncompleteSampleFailure(
            PlaybackQualityReport report,
            double requiredDurationMs,
            double observedDurationMs)
        {
            if (report == null ||
                !string.Equals(
                    report.Result,
                    PlaybackQualityReportResult.Fail,
                    StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var check in report.Checks)
            {
                if (check == null ||
                    !string.Equals(check.Name, "SampleWindowCoverage", StringComparison.Ordinal) ||
                    !string.Equals(check.Signal, "execution.requestedSampleDurationMs", StringComparison.Ordinal) ||
                    !string.Equals(check.Status, "fail", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(check.FailureArea) ||
                    !PlaybackQualityFailureClassification.IsKnown(check.FailureClass) ||
                    !double.TryParse(
                        check.Expected,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var checkedRequiredDurationMs) ||
                    !double.TryParse(
                        check.Actual,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var checkedObservedDurationMs))
                {
                    continue;
                }

                return Math.Abs(checkedRequiredDurationMs - requiredDurationMs) <= 0.001 &&
                    Math.Abs(checkedObservedDurationMs - observedDurationMs) <= 0.001;
            }

            return false;
        }
    }
}
