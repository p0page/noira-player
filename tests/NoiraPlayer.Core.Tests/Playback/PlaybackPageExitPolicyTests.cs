using NoiraPlayer.Core.Playback;
using Xunit;

namespace NoiraPlayer.Core.Tests.Playback;

public sealed class PlaybackPageExitPolicyTests
{
    [Theory]
    [InlineData(PlaybackState.Opening)]
    [InlineData(PlaybackState.Stopped)]
    [InlineData(PlaybackState.Failed)]
    public void Back_Exits_When_Playback_Is_Not_Interactively_Playing(PlaybackState state)
    {
        Assert.True(PlaybackPageExitPolicy.ShouldBackExit(state));
    }

    [Theory]
    [InlineData(PlaybackState.Playing)]
    [InlineData(PlaybackState.Paused)]
    public void Back_Does_Not_Exit_When_Playback_Is_Active(PlaybackState state)
    {
        Assert.False(PlaybackPageExitPolicy.ShouldBackExit(state));
    }
}
