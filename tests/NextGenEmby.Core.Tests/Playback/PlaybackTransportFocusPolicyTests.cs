using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class PlaybackTransportFocusPolicyTests
{
    [Fact]
    public void Default_Target_Uses_Pause_While_Playing()
    {
        var target = PlaybackTransportFocusPolicy.GetDefaultTarget(
            PlaybackState.Playing,
            pauseEnabled: true,
            resumeEnabled: false,
            seekBackEnabled: true,
            seekForwardEnabled: true,
            moreEnabled: true,
            stopEnabled: true);

        Assert.Equal(PlaybackTransportFocusTarget.Pause, target);
    }

    [Fact]
    public void Default_Target_Uses_Resume_While_Paused()
    {
        var target = PlaybackTransportFocusPolicy.GetDefaultTarget(
            PlaybackState.Paused,
            pauseEnabled: false,
            resumeEnabled: true,
            seekBackEnabled: true,
            seekForwardEnabled: true,
            moreEnabled: true,
            stopEnabled: true);

        Assert.Equal(PlaybackTransportFocusTarget.Resume, target);
    }

    [Fact]
    public void Right_Skips_Disabled_Controls_And_Stops_At_Stop()
    {
        var first = PlaybackTransportFocusPolicy.Move(
            PlaybackTransportFocusTarget.Pause,
            PlaybackTransportFocusDirection.Right,
            pauseEnabled: true,
            resumeEnabled: false,
            seekBackEnabled: true,
            seekForwardEnabled: true,
            moreEnabled: true,
            stopEnabled: true);

        var second = PlaybackTransportFocusPolicy.Move(
            first,
            PlaybackTransportFocusDirection.Right,
            pauseEnabled: true,
            resumeEnabled: false,
            seekBackEnabled: true,
            seekForwardEnabled: true,
            moreEnabled: true,
            stopEnabled: true);

        var third = PlaybackTransportFocusPolicy.Move(
            PlaybackTransportFocusTarget.Stop,
            PlaybackTransportFocusDirection.Right,
            pauseEnabled: true,
            resumeEnabled: false,
            seekBackEnabled: true,
            seekForwardEnabled: true,
            moreEnabled: true,
            stopEnabled: true);

        Assert.Equal(PlaybackTransportFocusTarget.SeekBack, first);
        Assert.Equal(PlaybackTransportFocusTarget.SeekForward, second);
        Assert.Equal(PlaybackTransportFocusTarget.Stop, third);
    }

    [Fact]
    public void Left_Skips_Disabled_Controls_And_Stops_At_First_Enabled()
    {
        var first = PlaybackTransportFocusPolicy.Move(
            PlaybackTransportFocusTarget.Stop,
            PlaybackTransportFocusDirection.Left,
            pauseEnabled: false,
            resumeEnabled: true,
            seekBackEnabled: true,
            seekForwardEnabled: true,
            moreEnabled: true,
            stopEnabled: true);

        var second = PlaybackTransportFocusPolicy.Move(
            first,
            PlaybackTransportFocusDirection.Left,
            pauseEnabled: false,
            resumeEnabled: true,
            seekBackEnabled: true,
            seekForwardEnabled: true,
            moreEnabled: true,
            stopEnabled: true);

        var third = PlaybackTransportFocusPolicy.Move(
            PlaybackTransportFocusTarget.Resume,
            PlaybackTransportFocusDirection.Left,
            pauseEnabled: false,
            resumeEnabled: true,
            seekBackEnabled: true,
            seekForwardEnabled: true,
            moreEnabled: true,
            stopEnabled: true);

        Assert.Equal(PlaybackTransportFocusTarget.More, first);
        Assert.Equal(PlaybackTransportFocusTarget.SeekForward, second);
        Assert.Equal(PlaybackTransportFocusTarget.Resume, third);
    }
}
