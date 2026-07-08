namespace NoiraPlayer.Core.Input
{
    public enum MediaDetailsDefaultFocusTarget
    {
        Play,
        FirstEpisode,
        Refresh
    }

    public static class MediaDetailsDefaultFocusPolicy
    {
        public static MediaDetailsDefaultFocusTarget Decide(bool playEnabled, int episodeButtonCount)
        {
            if (playEnabled)
            {
                return MediaDetailsDefaultFocusTarget.Play;
            }

            return episodeButtonCount > 0
                ? MediaDetailsDefaultFocusTarget.FirstEpisode
                : MediaDetailsDefaultFocusTarget.Refresh;
        }
    }
}
