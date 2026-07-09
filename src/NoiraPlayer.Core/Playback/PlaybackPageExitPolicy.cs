namespace NoiraPlayer.Core.Playback
{
    public static class PlaybackPageExitPolicy
    {
        public static bool ShouldBackExit(PlaybackState state)
        {
            return state == PlaybackState.Opening ||
                state == PlaybackState.Stopped ||
                state == PlaybackState.Failed;
        }
    }
}
