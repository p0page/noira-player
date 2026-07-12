using System;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityInteractionEvidencePolicy
    {
        private static readonly long PausePositionToleranceTicks =
            TimeSpan.FromMilliseconds(20).Ticks;

        public static bool IsPauseResumeRecovered(
            long positionBeforePauseTicks,
            long positionDuringPauseTicks,
            long positionBeforeResumeTicks,
            long positionAfterResumeTicks,
            ulong renderedVideoFramesBefore,
            ulong renderedVideoFramesAfter)
        {
            return Math.Abs(positionDuringPauseTicks - positionBeforePauseTicks) <=
                    PausePositionToleranceTicks &&
                positionAfterResumeTicks > positionBeforeResumeTicks &&
                renderedVideoFramesAfter > renderedVideoFramesBefore;
        }

        public static bool IsAudioSwitchRecovered(
            int targetStreamIndex,
            int selectedStreamIndex,
            long positionBeforeTicks,
            long positionAfterTicks,
            ulong submittedAudioFramesBefore,
            ulong submittedAudioFramesAfter)
        {
            return selectedStreamIndex == targetStreamIndex &&
                positionAfterTicks > positionBeforeTicks &&
                submittedAudioFramesAfter > submittedAudioFramesBefore;
        }
    }
}
