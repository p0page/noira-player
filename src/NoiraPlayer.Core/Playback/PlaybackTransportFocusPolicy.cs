using System;
using System.Collections.Generic;
using System.Linq;

namespace NoiraPlayer.Core.Playback
{
    public enum PlaybackTransportFocusTarget
    {
        Pause,
        Resume,
        SeekBack,
        SeekForward,
        More,
        Stop
    }

    public enum PlaybackTransportFocusDirection
    {
        Left,
        Right
    }

    public static class PlaybackTransportFocusPolicy
    {
        public static PlaybackTransportFocusTarget GetDefaultTarget(
            PlaybackState state,
            bool pauseEnabled,
            bool resumeEnabled,
            bool seekBackEnabled,
            bool seekForwardEnabled,
            bool moreEnabled,
            bool stopEnabled)
        {
            var targets = GetEnabledTargets(
                pauseEnabled,
                resumeEnabled,
                seekBackEnabled,
                seekForwardEnabled,
                moreEnabled,
                stopEnabled);

            var preferred = state == PlaybackState.Paused
                ? PlaybackTransportFocusTarget.Resume
                : PlaybackTransportFocusTarget.Pause;
            return targets.Contains(preferred) ? preferred : targets[0];
        }

        public static PlaybackTransportFocusTarget Move(
            PlaybackTransportFocusTarget current,
            PlaybackTransportFocusDirection direction,
            bool pauseEnabled,
            bool resumeEnabled,
            bool seekBackEnabled,
            bool seekForwardEnabled,
            bool moreEnabled,
            bool stopEnabled)
        {
            var targets = GetEnabledTargets(
                pauseEnabled,
                resumeEnabled,
                seekBackEnabled,
                seekForwardEnabled,
                moreEnabled,
                stopEnabled);
            var currentIndex = targets.IndexOf(current);
            if (currentIndex < 0)
            {
                return targets[0];
            }

            var delta = direction == PlaybackTransportFocusDirection.Right ? 1 : -1;
            var nextIndex = Math.Max(0, Math.Min(targets.Count - 1, currentIndex + delta));
            return targets[nextIndex];
        }

        private static List<PlaybackTransportFocusTarget> GetEnabledTargets(
            bool pauseEnabled,
            bool resumeEnabled,
            bool seekBackEnabled,
            bool seekForwardEnabled,
            bool moreEnabled,
            bool stopEnabled)
        {
            return new[]
                {
                    pauseEnabled ? PlaybackTransportFocusTarget.Pause : (PlaybackTransportFocusTarget?)null,
                    resumeEnabled ? PlaybackTransportFocusTarget.Resume : (PlaybackTransportFocusTarget?)null,
                    seekBackEnabled ? PlaybackTransportFocusTarget.SeekBack : (PlaybackTransportFocusTarget?)null,
                    seekForwardEnabled ? PlaybackTransportFocusTarget.SeekForward : (PlaybackTransportFocusTarget?)null,
                    moreEnabled ? PlaybackTransportFocusTarget.More : (PlaybackTransportFocusTarget?)null,
                    stopEnabled ? PlaybackTransportFocusTarget.Stop : (PlaybackTransportFocusTarget?)null
                }
                .Where(target => target.HasValue)
                .Select(target => target!.Value)
                .DefaultIfEmpty(PlaybackTransportFocusTarget.More)
                .ToList();
        }
    }
}
