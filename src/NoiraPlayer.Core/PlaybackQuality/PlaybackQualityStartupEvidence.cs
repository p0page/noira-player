using System;
using System.Linq;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityStartupEvidence
    {
        public static void EnrichNativeOpenBreakdown(
            PlaybackQualityStartup startup,
            PlaybackQualityMetricsSnapshot metrics)
        {
            if (startup == null || metrics == null || metrics.NativeGraphOpenDurationMs <= 0)
            {
                return;
            }

            var nativeOpen = startup.Stages.FirstOrDefault(
                stage => string.Equals(stage.Name, "native.open", StringComparison.Ordinal));
            if (nativeOpen == null || nativeOpen.DurationMs <= 0)
            {
                return;
            }

            var openInputMs = Math.Max(0, metrics.FfmpegOpenInputDurationMs);
            var streamInfoMs = Math.Max(0, metrics.FfmpegStreamInfoDurationMs);
            var graphOtherMs = Math.Max(
                0,
                metrics.NativeGraphOpenDurationMs - openInputMs - streamInfoMs);
            var hostDispatchMs = Math.Max(
                0,
                nativeOpen.DurationMs - metrics.NativeGraphOpenDurationMs);

            nativeOpen.Components.Clear();
            Add(nativeOpen, "ffmpeg.open-input", openInputMs);
            Add(nativeOpen, "ffmpeg.find-stream-info", streamInfoMs);
            Add(nativeOpen, "native.initialize-first-frame", graphOtherMs);
            Add(nativeOpen, "host.dispatch-overhead", hostDispatchMs);
        }

        private static void Add(
            PlaybackQualityStartupStage stage,
            string name,
            double durationMs)
        {
            stage.Components.Add(new PlaybackQualityStartupComponent
            {
                Name = name,
                DurationMs = durationMs
            });
        }
    }
}
