namespace NoiraPlayer.Core.Emby
{
    public class PlaybackSessionRequest
    {
        public string ItemId { get; set; } = "";
        public string MediaSourceId { get; set; } = "";
        public string? PlaySessionId { get; set; }
        public long PositionTicks { get; set; }
        public bool IsPaused { get; set; }
        public PlaybackPlayMethod PlayMethod { get; set; } = PlaybackPlayMethod.DirectPlay;
        public int? AudioStreamIndex { get; set; }
        public int? SubtitleStreamIndex { get; set; }
    }
}
