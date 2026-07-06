namespace NextGenEmby.Core.Input
{
    public sealed class MediaDetailsActionState
    {
        public string PlayLabel { get; set; } = "Play";
        public bool ShowRestart { get; set; }
        public string FavoriteLabel { get; set; } = "Add favorite";
        public string WatchedLabel { get; set; } = "Mark watched";
    }

    public static class MediaDetailsActionPolicy
    {
        public static MediaDetailsActionState Decide(
            bool canPlay,
            bool isFavorite,
            bool isPlayed,
            long playbackPositionTicks)
        {
            return new MediaDetailsActionState
            {
                PlayLabel = canPlay && playbackPositionTicks > 0 ? "Resume" : "Play",
                ShowRestart = canPlay && playbackPositionTicks > 0,
                FavoriteLabel = isFavorite ? "Remove favorite" : "Add favorite",
                WatchedLabel = isPlayed ? "Mark unwatched" : "Mark watched"
            };
        }
    }
}
