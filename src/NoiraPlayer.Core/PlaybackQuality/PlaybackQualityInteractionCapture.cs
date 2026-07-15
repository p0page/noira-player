using System;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityInteractionCapture
    {
        public static PlaybackQualityInteractionEvidence CreatePauseResume(
            double requestedPauseDurationMs,
            double actualPauseDurationMs,
            double recoveryDurationMs,
            long positionBeforeTicks,
            long positionAfterTicks,
            ulong decodedVideoFramesBefore,
            ulong decodedVideoFramesAfter,
            ulong renderedVideoFramesBefore,
            ulong renderedVideoFramesAfter,
            bool playbackFailed)
        {
            EnsureDuration(requestedPauseDurationMs, nameof(requestedPauseDurationMs));
            EnsureDuration(actualPauseDurationMs, nameof(actualPauseDurationMs));
            EnsureDuration(recoveryDurationMs, nameof(recoveryDurationMs));
            if (positionBeforeTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(positionBeforeTicks));
            }

            if (positionAfterTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(positionAfterTicks));
            }

            return new PlaybackQualityInteractionEvidence
            {
                Scenario = PlaybackQualityExecutionScenario.PauseResume,
                Attempted = true,
                RequestedPauseDurationMs = requestedPauseDurationMs,
                ActualPauseDurationMs = actualPauseDurationMs,
                RecoveryDurationMs = recoveryDurationMs,
                PositionBeforeTicks = positionBeforeTicks,
                PositionAfterTicks = positionAfterTicks,
                PositionDeltaTicks = positionAfterTicks - positionBeforeTicks,
                DecodedVideoFramesBefore = decodedVideoFramesBefore,
                DecodedVideoFramesAfter = decodedVideoFramesAfter,
                DecodedVideoFrameDelta = NonNegativeDifference(
                    decodedVideoFramesAfter,
                    decodedVideoFramesBefore),
                RenderedVideoFramesBefore = renderedVideoFramesBefore,
                RenderedVideoFramesAfter = renderedVideoFramesAfter,
                RenderedVideoFrameDelta = NonNegativeDifference(
                    renderedVideoFramesAfter,
                    renderedVideoFramesBefore),
                PlaybackFailed = playbackFailed
            };
        }

        public static PlaybackQualityInteractionEvidence Create(
            string scenario,
            double operationDurationMs,
            double recoveryDurationMs,
            double? cueRenderDurationMs,
            PlaybackQualityMetricsSnapshot before,
            PlaybackQualityMetricsSnapshot after)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            if (after == null)
            {
                throw new ArgumentNullException(nameof(after));
            }

            if (scenario != "audio-switch" && scenario != "subtitle-switch")
            {
                throw new ArgumentOutOfRangeException(nameof(scenario));
            }

            if (after.LastInteractionSequence <= before.LastInteractionSequence ||
                !string.Equals(after.LastInteractionScenario, scenario, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Native interaction snapshot is stale or belongs to another scenario.");
            }

            EnsureDuration(operationDurationMs, nameof(operationDurationMs));
            EnsureDuration(recoveryDurationMs, nameof(recoveryDurationMs));
            if (cueRenderDurationMs.HasValue)
            {
                EnsureDuration(cueRenderDurationMs.Value, nameof(cueRenderDurationMs));
            }

            return new PlaybackQualityInteractionEvidence
            {
                Scenario = scenario,
                Attempted = true,
                OperationDurationMs = operationDurationMs,
                LockWaitDurationMs = after.LastInteractionLockWaitDurationMs,
                ExecutionDurationMs = after.LastInteractionExecutionDurationMs,
                QuiesceDurationMs = after.LastInteractionQuiesceDurationMs,
                SeekDurationMs = after.LastInteractionSeekDurationMs,
                DecoderOpenDurationMs = after.LastInteractionDecoderOpenDurationMs,
                RendererOpenDurationMs = after.LastInteractionRendererOpenDurationMs,
                PacketCacheHit = after.LastInteractionPacketCacheHit,
                PacketCacheEnabled = after.LastInteractionPacketCacheEnabled,
                PacketCachePacketCount = after.LastInteractionPacketCachePacketCount,
                PacketCacheBytes = after.LastInteractionPacketCacheBytes,
                PacketCacheWindowDurationTicks = after.LastInteractionPacketCacheWindowDurationTicks,
                RecoveryDurationMs = recoveryDurationMs,
                CueRenderDurationMs = cueRenderDurationMs,
                PositionDeltaTicks = Difference(after.VideoPositionTicks, before.VideoPositionTicks),
                SubmittedAudioFrameDelta = Difference(after.SubmittedAudioFrames, before.SubmittedAudioFrames),
                RenderedVideoFrameDelta = Difference(after.RenderedVideoFrames, before.RenderedVideoFrames),
                SubtitleCueRenderCountDelta = Difference(
                    after.SubtitleCueRenderCount,
                    before.SubtitleCueRenderCount)
            };
        }

        private static long? Difference(long after, long before)
        {
            return after >= before ? after - before : (long?)null;
        }

        private static ulong? Difference(ulong after, ulong before)
        {
            return after >= before ? after - before : (ulong?)null;
        }

        private static ulong NonNegativeDifference(ulong after, ulong before)
        {
            return after >= before ? after - before : 0;
        }

        private static void EnsureDuration(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
