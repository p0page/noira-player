namespace NextGenEmby.Core.Emby
{
    public sealed class PlaybackProgressRequest : PlaybackSessionRequest
    {
        public PlaybackProgressEvent EventName { get; set; } = PlaybackProgressEvent.TimeUpdate;
    }
}
