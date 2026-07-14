using System;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityStartupAttribution
    {
        public string Attribution { get; set; } = "not-evaluated";
        public string Reason { get; set; } = "";
        public double TransportWaitDurationMs { get; set; }
        public double TransportWaitRatio { get; set; }
        public int MeasuredTransportComponentCount { get; set; }
    }

    public static class PlaybackQualityStartupAttributionPolicy
    {
        private const double DominantStartupRatio = 0.25;

        public static PlaybackQualityStartupAttribution Assess(
            PlaybackQualityReport report,
            double maximumStartupDurationMs)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var result = new PlaybackQualityStartupAttribution();
            var startupDurationMs = report.Startup.StartupDurationMs;
            if (startupDurationMs <= 0 || maximumStartupDurationMs <= 0)
            {
                result.Attribution = "insufficient-evidence";
                result.Reason = "Startup duration or threshold is unavailable.";
                return result;
            }

            foreach (var stage in report.Startup.Stages)
            {
                foreach (var component in stage.Components)
                {
                    if (!string.Equals(
                            component.TransportCallEvidenceStatus,
                            "measured",
                            StringComparison.Ordinal) ||
                        !component.TransportReadWaitMs.HasValue ||
                        !component.TransportSeekWaitMs.HasValue)
                    {
                        continue;
                    }

                    result.MeasuredTransportComponentCount++;
                    result.TransportWaitDurationMs +=
                        Math.Max(0, component.TransportReadWaitMs.Value) +
                        Math.Max(0, component.TransportSeekWaitMs.Value);
                }
            }

            if (result.MeasuredTransportComponentCount == 0)
            {
                result.Attribution = "transport-evidence-unavailable";
                result.Reason = "Measured startup transport call timing is unavailable.";
                return result;
            }

            result.TransportWaitRatio = result.TransportWaitDurationMs / startupDurationMs;
            if (startupDurationMs <= maximumStartupDurationMs)
            {
                result.Attribution = "within-threshold";
                result.Reason =
                    "Startup did not exceed the configured threshold; transport timing is context, not a failure cause.";
                return result;
            }

            var overrunMs = Math.Max(0, startupDurationMs - maximumStartupDurationMs);
            if (overrunMs > 0 &&
                result.TransportWaitDurationMs >= overrunMs &&
                result.TransportWaitRatio >= DominantStartupRatio)
            {
                result.Attribution = "transport-wait-dominant";
                result.Reason =
                    "Measured startup transport wait both explains the threshold overrun and consumes at least 25% of startup.";
                return result;
            }

            result.Attribution = "startup-processing-dominant";
            result.Reason =
                "Measured startup transport wait does not dominate the threshold overrun.";
            return result;
        }
    }
}
