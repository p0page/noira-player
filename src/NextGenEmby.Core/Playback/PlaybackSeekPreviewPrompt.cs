using System;

namespace NextGenEmby.Core.Playback
{
    public static class PlaybackSeekPreviewPrompt
    {
        public static string Format(TimeSpan position)
        {
            var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            return string.Format(
                "Seek preview {0:D2}:{1:D2}:{2:D2} - A/Enter Confirm / B/Escape Cancel",
                (int)clamped.TotalHours,
                clamped.Minutes,
                clamped.Seconds);
        }
    }
}
