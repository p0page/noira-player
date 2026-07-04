namespace NextGenEmby.Core.Emby
{
    public enum PlaybackProgressEvent
    {
        TimeUpdate,
        Pause,
        Unpause,
        VolumeChange,
        AudioTrackChange,
        SubtitleTrackChange,
        QualityChange,
        StateChange,
        SubtitleOffsetChange,
        PlaybackRateChange
    }
}
