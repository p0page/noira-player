using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Input
{
    public static class MediaDetailsUserDataTogglePolicy
    {
        public static EmbyUserData ToggleFavorite(EmbyUserData current)
        {
            var updated = Copy(current);
            updated.IsFavorite = !updated.IsFavorite;
            return updated;
        }

        public static EmbyUserData TogglePlayed(EmbyUserData current)
        {
            var updated = Copy(current);
            updated.Played = !updated.Played;
            return updated;
        }

        private static EmbyUserData Copy(EmbyUserData current)
        {
            current = current ?? new EmbyUserData();
            return new EmbyUserData
            {
                IsFavorite = current.IsFavorite,
                Played = current.Played,
                PlaybackPositionTicks = current.PlaybackPositionTicks,
                PlayedPercentage = current.PlayedPercentage
            };
        }
    }
}
