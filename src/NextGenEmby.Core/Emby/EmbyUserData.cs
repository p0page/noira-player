namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyUserData
    {
        public bool IsFavorite { get; set; }
        public bool Played { get; set; }
        public long PlaybackPositionTicks { get; set; }
        public double? PlayedPercentage { get; set; }
    }
}
