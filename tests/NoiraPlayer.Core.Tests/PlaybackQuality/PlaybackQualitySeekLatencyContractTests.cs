using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualitySeekLatencyContractTests
{
    [Fact]
    public void Report_Contract_Exposes_Seek_Operation_And_Recovery_Durations()
    {
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekOperationDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekRecoveryDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekPacketCacheEnabled"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekPacketCacheHit"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekPacketCachePacketCount"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekPacketCacheBytes"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekPacketCacheWindowDurationTicks"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekFallbackReason"));
        Assert.NotNull(typeof(PlaybackQualityExpected).GetProperty("MaxSeekRecoveryDurationMs"));
    }

    [Fact]
    public void Seek_Latency_Contract_Uses_A_New_Evaluation_Version()
    {
        Assert.Equal("playback-quality-v0.4", PlaybackQualityRunResult.CurrentEvaluationVersion);
    }
}
