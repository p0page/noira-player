namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityRuntimeMetricsFactory
    {
        public static PlaybackQualityRuntimeMetrics Unavailable(string providerStatus)
        {
            return new PlaybackQualityRuntimeMetrics
            {
                Status = "unavailable",
                ProviderStatus = string.IsNullOrWhiteSpace(providerStatus)
                    ? "unknown"
                    : providerStatus,
                Reason = "Runtime metrics provider did not return a playback quality snapshot.",
                HasSnapshot = false,
                HasPlaybackSample = false
            };
        }

        public static PlaybackQualityRuntimeMetrics FromSnapshot(
            PlaybackQualityMetricsSnapshot metrics,
            string providerStatus)
        {
            var hasPlaybackSample = HasPlaybackSample(metrics);
            return new PlaybackQualityRuntimeMetrics
            {
                Status = hasPlaybackSample ? "captured" : "empty-snapshot",
                ProviderStatus = string.IsNullOrWhiteSpace(providerStatus)
                    ? "unknown"
                    : providerStatus,
                Reason = hasPlaybackSample
                    ? "Runtime metrics snapshot contains playback sample evidence."
                    : "Runtime metrics provider returned a snapshot, but no playback sample counters were populated.",
                HasSnapshot = true,
                HasPlaybackSample = hasPlaybackSample
            };
        }

        private static bool HasPlaybackSample(PlaybackQualityMetricsSnapshot metrics)
        {
            return metrics.RenderPasses > 0 ||
                metrics.DecodedVideoFrames > 0 ||
                metrics.RenderedVideoFrames > 0 ||
                metrics.SubmittedAudioFrames > 0 ||
                metrics.DroppedVideoFrames > 0 ||
                metrics.SeekPrerollDroppedFrames > 0 ||
                metrics.VideoAheadWaitCount > 0 ||
                metrics.AudioAheadWaitCount > 0 ||
                metrics.VideoClockWaitCount > 0 ||
                metrics.VideoStarvedPasses > 0 ||
                metrics.AudioStarvedPasses > 0 ||
                metrics.QueuedAudioBuffers > 0 ||
                metrics.AudioClockTicks != 0 ||
                metrics.VideoPositionTicks != 0 ||
                metrics.RenderIntervalMsP50 > 0 ||
                metrics.RenderIntervalMsP95 > 0 ||
                metrics.RenderIntervalMsP99 > 0 ||
                metrics.MaxFrameGapMs > 0 ||
                metrics.RenderIntervalSampleCount > 0 ||
                metrics.RenderIntervalOverExpected2MsCount > 0 ||
                metrics.RenderIntervalOverExpected4MsCount > 0 ||
                metrics.PresentDurationMsP50 > 0 ||
                metrics.PresentDurationMsP95 > 0 ||
                metrics.PresentDurationMsP99 > 0 ||
                metrics.PresentDurationMsMax > 0 ||
                metrics.AudioAheadWaitDurationMsP50 > 0 ||
                metrics.AudioAheadWaitDurationMsP95 > 0 ||
                metrics.AudioAheadWaitDurationMsP99 > 0 ||
                metrics.AudioAheadWaitDurationMsMax > 0 ||
                metrics.AudioAheadWaitTargetMsP50 > 0 ||
                metrics.AudioAheadWaitTargetMsP95 > 0 ||
                metrics.AudioAheadWaitTargetMsP99 > 0 ||
                metrics.AudioAheadWaitTargetMsMax > 0 ||
                metrics.AudioAheadWaitOversleepMsP50 > 0 ||
                metrics.AudioAheadWaitOversleepMsP95 > 0 ||
                metrics.AudioAheadWaitOversleepMsP99 > 0 ||
                metrics.AudioAheadWaitOversleepMsMax > 0 ||
                metrics.AudioAheadWaitFinalDeltaAbsMsP50 > 0 ||
                metrics.AudioAheadWaitFinalDeltaAbsMsP95 > 0 ||
                metrics.AudioAheadWaitFinalDeltaAbsMsP99 > 0 ||
                metrics.AudioAheadWaitFinalDeltaAbsMsMax > 0 ||
                metrics.FramePacingSourceFrameRate > 0 ||
                metrics.LateFrameDropToleranceMs > 0 ||
                metrics.AudioVideoDriftMsP50 > 0 ||
                metrics.AudioVideoDriftMsP95 > 0 ||
                metrics.AudioVideoDriftMsP99 > 0 ||
                metrics.AudioVideoDriftMsMax > 0;
        }
    }
}
