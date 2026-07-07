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
            PlaybackQualityEnvironment? environment = null)
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
            var provider = metricsProvider ?? diagnostics as IPlaybackQualityMetricsProvider;
            if (provider != null && provider.TryGetQualityMetrics(out var snapshot))
            {
                metrics = snapshot;
            }

            var request = PlaybackQualityReferenceCaseReportRequestFactory.CreateRequest(
                referenceCase,
                descriptor,
                diagnostics?.DisplayStatus,
                metrics,
                startup);
            request.Environment = environment;
            return request;
        }

        public static PlaybackQualityRunResult ComposeRunResult(
            PlaybackQualityReferenceCase referenceCase,
            PlaybackDescriptor descriptor,
            IPlaybackBackendDiagnostics? diagnostics = null,
            IPlaybackQualityMetricsProvider? metricsProvider = null,
            PlaybackQualityStartup? startup = null,
            PlaybackQualityEnvironment? environment = null)
        {
            return PlaybackQualityReportComposer.Compose(
                CreateRequest(
                    referenceCase,
                    descriptor,
                    diagnostics,
                    metricsProvider,
                    startup,
                    environment));
        }
    }
}
