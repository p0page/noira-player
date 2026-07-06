using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class PlaybackMoreDrawerFocusPolicyTests
{
    [Fact]
    public void Default_Target_Uses_First_Enabled_Stream_Control()
    {
        var target = PlaybackMoreDrawerFocusPolicy.GetDefaultTarget(
            sourceEnabled: false,
            audioEnabled: true,
            subtitlesEnabled: true);

        Assert.Equal(PlaybackMoreDrawerFocusTarget.Audio, target);
    }

    [Fact]
    public void Default_Target_Falls_Back_To_Info_When_No_Stream_Control_Is_Enabled()
    {
        var target = PlaybackMoreDrawerFocusPolicy.GetDefaultTarget(
            sourceEnabled: false,
            audioEnabled: false,
            subtitlesEnabled: false);

        Assert.Equal(PlaybackMoreDrawerFocusTarget.Info, target);
    }

    [Fact]
    public void Down_Skips_Disabled_Targets_And_Stops_At_Info()
    {
        var first = PlaybackMoreDrawerFocusPolicy.Move(
            PlaybackMoreDrawerFocusTarget.Source,
            PlaybackMoreDrawerFocusDirection.Down,
            sourceEnabled: false,
            audioEnabled: true,
            subtitlesEnabled: false);

        var second = PlaybackMoreDrawerFocusPolicy.Move(
            first,
            PlaybackMoreDrawerFocusDirection.Down,
            sourceEnabled: false,
            audioEnabled: true,
            subtitlesEnabled: false);

        var third = PlaybackMoreDrawerFocusPolicy.Move(
            second,
            PlaybackMoreDrawerFocusDirection.Down,
            sourceEnabled: false,
            audioEnabled: true,
            subtitlesEnabled: false);

        Assert.Equal(PlaybackMoreDrawerFocusTarget.Audio, first);
        Assert.Equal(PlaybackMoreDrawerFocusTarget.Info, second);
        Assert.Equal(PlaybackMoreDrawerFocusTarget.Info, third);
    }

    [Fact]
    public void Down_From_Subtitles_Moves_To_Info_When_All_Targets_Are_Enabled()
    {
        var target = PlaybackMoreDrawerFocusPolicy.Move(
            PlaybackMoreDrawerFocusTarget.Subtitles,
            PlaybackMoreDrawerFocusDirection.Down,
            sourceEnabled: true,
            audioEnabled: true,
            subtitlesEnabled: true);

        Assert.Equal(PlaybackMoreDrawerFocusTarget.Info, target);
    }

    [Fact]
    public void Up_Skips_Disabled_Targets_And_Stops_At_First_Enabled_Target()
    {
        var first = PlaybackMoreDrawerFocusPolicy.Move(
            PlaybackMoreDrawerFocusTarget.Info,
            PlaybackMoreDrawerFocusDirection.Up,
            sourceEnabled: false,
            audioEnabled: true,
            subtitlesEnabled: true);

        var second = PlaybackMoreDrawerFocusPolicy.Move(
            first,
            PlaybackMoreDrawerFocusDirection.Up,
            sourceEnabled: false,
            audioEnabled: true,
            subtitlesEnabled: true);

        var third = PlaybackMoreDrawerFocusPolicy.Move(
            second,
            PlaybackMoreDrawerFocusDirection.Up,
            sourceEnabled: false,
            audioEnabled: true,
            subtitlesEnabled: true);

        Assert.Equal(PlaybackMoreDrawerFocusTarget.Subtitles, first);
        Assert.Equal(PlaybackMoreDrawerFocusTarget.Audio, second);
        Assert.Equal(PlaybackMoreDrawerFocusTarget.Audio, third);
    }
}
