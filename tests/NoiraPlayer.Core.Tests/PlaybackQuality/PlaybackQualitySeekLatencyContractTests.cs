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
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekLockWaitDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekExecutionDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekQuiesceDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekReplayPreparationDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekStateResetDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekMediaRepositionDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekDependentDecoderFlushDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekPrerollRenderDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekWorkerRestartDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekPacketCacheEnabled"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekPacketCacheHit"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekPacketCachePacketCount"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekPacketCacheBytes"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekPacketCacheWindowDurationTicks"));
        Assert.NotNull(typeof(PlaybackQualityPosition).GetProperty("SeekFallbackReason"));
        Assert.NotNull(typeof(PlaybackQualityExpected).GetProperty("MaxSeekRecoveryDurationMs"));
    }

    [Fact]
    public void Current_Evaluation_Version_Includes_Explicit_Timeline_Target_Contract()
    {
        Assert.Equal("playback-quality-v0.21", PlaybackQualityRunResult.CurrentEvaluationVersion);
        Assert.NotNull(typeof(PlaybackQualityExecutionEvidence).GetProperty("RequestedSampleDurationMs"));
        Assert.NotNull(typeof(PlaybackQualityExecutionEvidence).GetProperty("ObservedSampleWallClockDurationMs"));
    }
}
