using System;
using System.Collections.Generic;
using System.Linq;

namespace NextGenEmby.Core.Playback
{
    public enum PlaybackMoreDrawerFocusTarget
    {
        Source,
        Audio,
        Subtitles,
        Info
    }

    public enum PlaybackMoreDrawerFocusDirection
    {
        Up,
        Down
    }

    public static class PlaybackMoreDrawerFocusPolicy
    {
        public static PlaybackMoreDrawerFocusTarget GetDefaultTarget(
            bool sourceEnabled,
            bool audioEnabled,
            bool subtitlesEnabled)
        {
            return GetEnabledTargets(sourceEnabled, audioEnabled, subtitlesEnabled)[0];
        }

        public static PlaybackMoreDrawerFocusTarget Move(
            PlaybackMoreDrawerFocusTarget current,
            PlaybackMoreDrawerFocusDirection direction,
            bool sourceEnabled,
            bool audioEnabled,
            bool subtitlesEnabled)
        {
            var targets = GetEnabledTargets(sourceEnabled, audioEnabled, subtitlesEnabled);
            var currentIndex = targets.IndexOf(current);
            if (currentIndex < 0)
            {
                return targets[0];
            }

            var delta = direction == PlaybackMoreDrawerFocusDirection.Down ? 1 : -1;
            var nextIndex = Math.Max(0, Math.Min(targets.Count - 1, currentIndex + delta));
            return targets[nextIndex];
        }

        private static List<PlaybackMoreDrawerFocusTarget> GetEnabledTargets(
            bool sourceEnabled,
            bool audioEnabled,
            bool subtitlesEnabled)
        {
            var targets = new[]
                {
                    sourceEnabled ? PlaybackMoreDrawerFocusTarget.Source : (PlaybackMoreDrawerFocusTarget?)null,
                    audioEnabled ? PlaybackMoreDrawerFocusTarget.Audio : (PlaybackMoreDrawerFocusTarget?)null,
                    subtitlesEnabled ? PlaybackMoreDrawerFocusTarget.Subtitles : (PlaybackMoreDrawerFocusTarget?)null,
                    PlaybackMoreDrawerFocusTarget.Info
                }
                .Where(target => target.HasValue)
                .Select(target => target!.Value)
                .ToList();

            return targets;
        }
    }
}
