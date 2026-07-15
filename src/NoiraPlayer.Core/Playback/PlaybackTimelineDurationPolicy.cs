using System;

namespace NoiraPlayer.Core.Playback
{
    public static class PlaybackTimelineDurationPolicy
    {
        public static long Resolve(
            long nativeDurationTicks,
            long mediaSourceDurationTicks,
            long itemDurationTicks)
        {
            if (nativeDurationTicks > 0)
            {
                return nativeDurationTicks;
            }

            if (mediaSourceDurationTicks > 0)
            {
                return mediaSourceDurationTicks;
            }

            return Math.Max(0, itemDurationTicks);
        }
    }
}
