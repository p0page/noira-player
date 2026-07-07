namespace NextGenEmby.Core.PlaybackQuality
{
    public interface IPlaybackQualityMetricsProvider
    {
        bool TryGetQualityMetrics(out PlaybackQualityMetricsSnapshot metrics);
    }
}
