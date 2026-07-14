using System;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityThroughputAttribution
    {
        public string Attribution { get; set; } = "not-evaluated";
        public string Reason { get; set; } = "";
        public double DemuxReadRatio { get; set; }
        public double TransportReadWaitRatio { get; set; }
    }

    public static class PlaybackQualityThroughputAttributionPolicy
    {
        private const double DominantObservationRatio = 0.25;

        public static PlaybackQualityThroughputAttribution Assess(
            PlaybackQualityReport report,
            bool sampleIncomplete)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var result = new PlaybackQualityThroughputAttribution();
            var requestedDurationMs = report.Execution?.RequestedSampleDurationMs ?? 0;
            if (requestedDurationMs <= 0)
            {
                result.Attribution = "insufficient-evidence";
                result.Reason = "Requested observation duration is unavailable.";
                return result;
            }

            result.DemuxReadRatio =
                report.Buffers.PlaybackDemuxReadDurationMs / requestedDurationMs;
            result.TransportReadWaitRatio =
                report.Buffers.PlaybackTransportReadWaitMs.GetValueOrDefault() /
                requestedDurationMs;
            var hasTransportEvidence = string.Equals(
                    report.Buffers.PlaybackTransportCallEvidenceStatus,
                    "available",
                    StringComparison.Ordinal) &&
                report.Buffers.PlaybackTransportReadWaitMs.HasValue;
            if (hasTransportEvidence &&
                result.TransportReadWaitRatio >= DominantObservationRatio)
            {
                result.Attribution = "transport-wait-dominant";
                result.Reason =
                    "Transport read wait consumed at least 25% of the requested observation window.";
                return result;
            }

            if (hasTransportEvidence && sampleIncomplete)
            {
                var requiredMediaDurationMs =
                    PlaybackQualitySampleWindowPolicy.GetRequiredMediaDurationMs(report);
                var observedMediaDurationMs =
                    PlaybackQualitySampleWindowPolicy.GetObservedMediaDurationMs(report);
                var mediaShortfallMs = Math.Max(
                    0,
                    requiredMediaDurationMs - observedMediaDurationMs);
                var captureToleranceMs =
                    PlaybackQualitySampleWindowPolicy.GetCaptureBoundaryToleranceMs(report);
                if (mediaShortfallMs > captureToleranceMs &&
                    report.Buffers.PlaybackTransportReadWaitMs.GetValueOrDefault() +
                        captureToleranceMs >= mediaShortfallMs)
                {
                    result.Attribution = "transport-wait-dominant";
                    result.Reason =
                        "Measured transport read wait was sufficient to explain the incomplete media shortfall.";
                    return result;
                }
            }

            if (!sampleIncomplete)
            {
                result.Attribution = "sample-complete";
                result.Reason = "The requested media observation window was covered.";
                return result;
            }

            if (!hasTransportEvidence)
            {
                result.Attribution = "transport-evidence-unavailable";
                result.Reason =
                    "Transport call timing is unavailable, so the incomplete sample cannot be attributed to network wait.";
                return result;
            }

            if (result.DemuxReadRatio >= DominantObservationRatio)
            {
                result.Attribution = "demux-processing-dominant";
                result.Reason =
                    "Demux read time consumed at least 25% of the requested window without dominant transport wait.";
                return result;
            }

            result.Attribution = "downstream-or-scheduling";
            result.Reason =
                "The sample was incomplete without dominant demux or transport wait; inspect decode, render, clock, and scheduling evidence.";
            return result;
        }
    }
}
