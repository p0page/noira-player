using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackRefreshRatePolicyTests
{
    [Theory]
    [InlineData(23.976024, 23.976)]
    [InlineData(24.0, 23.976)]
    [InlineData(50.0, 25.0)]
    [InlineData(59.94006, 23.976)]
    [InlineData(60.0, 24.0)]
    [InlineData(59.94006, 29.97003)]
    [InlineData(60.0, 30.0)]
    [InlineData(119.88012, 23.976)]
    [InlineData(120.0, 24.0)]
    [InlineData(100.0, 25.0)]
    [InlineData(120.0, 30.0)]
    public void MatchesVideoFrameRate_Accepts_Native_Playback_Ratios(
        double displayRefreshRate,
        double videoFrameRate)
    {
        Assert.True(PlaybackRefreshRatePolicy.MatchesVideoFrameRate(displayRefreshRate, videoFrameRate));
    }

    [Theory]
    [InlineData(50.0, 23.976)]
    [InlineData(60.0, 25.0)]
    [InlineData(0.0, 24.0)]
    [InlineData(60.0, 0.0)]
    public void MatchesVideoFrameRate_Rejects_Mismatched_Or_Unusable_Rates(
        double displayRefreshRate,
        double videoFrameRate)
    {
        Assert.False(PlaybackRefreshRatePolicy.MatchesVideoFrameRate(displayRefreshRate, videoFrameRate));
    }

    [Fact]
    public void IsBetterRefreshRateForVideo_Prefers_Exact_Or_Ntsc_Compatible_Cadence()
    {
        Assert.True(PlaybackRefreshRatePolicy.IsBetterRefreshRateForVideo(23.976024, 59.94006, 23.976));
        Assert.True(PlaybackRefreshRatePolicy.IsBetterRefreshRateForVideo(24.0, 59.94006, 23.976));
        Assert.True(PlaybackRefreshRatePolicy.IsBetterRefreshRateForVideo(24.0, 60.0, 24.0));
        Assert.True(PlaybackRefreshRatePolicy.IsBetterRefreshRateForVideo(59.94006, 60.0, 23.976));
        Assert.True(PlaybackRefreshRatePolicy.IsBetterRefreshRateForVideo(60.0, 59.94006, 24.0));
        Assert.True(PlaybackRefreshRatePolicy.IsBetterRefreshRateForVideo(119.88012, 120.0, 23.976));
        Assert.False(PlaybackRefreshRatePolicy.IsBetterRefreshRateForVideo(59.94006, 23.976024, 23.976));
    }

    [Fact]
    public void AssessCadence_Reports_Kodi_Style_Clock_Speed_Adjustment()
    {
        var ntscCinemaFrameRate = 24000.0 / 1001.0;

        var exact = PlaybackRefreshRatePolicy.AssessCadence(
            displayRefreshRate: ntscCinemaFrameRate,
            videoFrameRate: ntscCinemaFrameRate);
        var adjusted24Hz = PlaybackRefreshRatePolicy.AssessCadence(
            displayRefreshRate: 24.0,
            videoFrameRate: ntscCinemaFrameRate);
        var adjusted60Hz = PlaybackRefreshRatePolicy.AssessCadence(
            displayRefreshRate: 60.0,
            videoFrameRate: ntscCinemaFrameRate);

        Assert.Equal("matched", exact.Status);
        Assert.Equal(1.0, exact.ClockSpeedMultiplier, precision: 6);
        Assert.Equal(0.0, exact.ClockSpeedAdjustmentPercent, precision: 6);
        Assert.False(exact.IsClockSpeedAdjustmentRequired);

        Assert.Equal("matched", adjusted24Hz.Status);
        Assert.Equal(1.0, adjusted24Hz.BestMultiplier, precision: 3);
        Assert.Equal(1.001, adjusted24Hz.ClockSpeedMultiplier, precision: 3);
        Assert.Equal(0.1001, adjusted24Hz.ClockSpeedAdjustmentPercent, precision: 3);
        Assert.True(adjusted24Hz.IsClockSpeedAdjustmentRequired);

        Assert.Equal("matched", adjusted60Hz.Status);
        Assert.Equal(2.5, adjusted60Hz.BestMultiplier, precision: 3);
        Assert.Equal(1.001, adjusted60Hz.ClockSpeedMultiplier, precision: 3);
        Assert.Equal(0.1001, adjusted60Hz.ClockSpeedAdjustmentPercent, precision: 3);
        Assert.True(adjusted60Hz.IsClockSpeedAdjustmentRequired);
    }
}
