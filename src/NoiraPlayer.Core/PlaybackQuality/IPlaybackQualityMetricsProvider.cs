namespace NoiraPlayer.Core.PlaybackQuality
{
    public interface IPlaybackQualityMetricsProvider
    {
        bool TryGetQualityMetrics(out PlaybackQualityMetricsSnapshot metrics);
    }

    public interface IPlaybackQualityMetricsProviderIdentity
    {
        string PlaybackQualityMetricsProviderId { get; }
    }
}
