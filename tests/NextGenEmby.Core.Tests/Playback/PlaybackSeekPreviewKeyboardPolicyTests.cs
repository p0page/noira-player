using System;
using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class PlaybackSeekPreviewKeyboardPolicyTests
{
    [Fact]
    public void Prompt_Includes_Controller_And_Keyboard_Confirm_Cancel_Hints()
    {
        var prompt = PlaybackSeekPreviewPrompt.Format(TimeSpan.FromSeconds(65));

        Assert.Equal("Seek preview 00:01:05 - A/Enter Confirm / B/Escape Cancel", prompt);
    }

    [Fact]
    public void Shift_Left_Starts_Backward_Preview_When_Enabled()
    {
        var action = PlaybackSeekPreviewKeyboardPolicy.Decide(
            PlaybackDirectionalInput.Left,
            previewModifierDown: true,
            seekPreviewEnabled: true,
            moreVisible: false);

        Assert.Equal(PlaybackSeekPreviewKeyboardAction.PreviewBackward, action);
    }

    [Fact]
    public void Shift_Right_Starts_Forward_Preview_When_Enabled()
    {
        var action = PlaybackSeekPreviewKeyboardPolicy.Decide(
            PlaybackDirectionalInput.Right,
            previewModifierDown: true,
            seekPreviewEnabled: true,
            moreVisible: false);

        Assert.Equal(PlaybackSeekPreviewKeyboardAction.PreviewForward, action);
    }

    [Fact]
    public void Plain_Direction_Does_Not_Enter_Seek_Preview()
    {
        var action = PlaybackSeekPreviewKeyboardPolicy.Decide(
            PlaybackDirectionalInput.Right,
            previewModifierDown: false,
            seekPreviewEnabled: true,
            moreVisible: false);

        Assert.Equal(PlaybackSeekPreviewKeyboardAction.None, action);
    }

    [Fact]
    public void More_Drawer_Keeps_Direction_Inside_Drawer()
    {
        var action = PlaybackSeekPreviewKeyboardPolicy.Decide(
            PlaybackDirectionalInput.Right,
            previewModifierDown: true,
            seekPreviewEnabled: true,
            moreVisible: true);

        Assert.Equal(PlaybackSeekPreviewKeyboardAction.None, action);
    }

    [Fact]
    public void Disabled_Setting_Does_Not_Enter_Seek_Preview()
    {
        var action = PlaybackSeekPreviewKeyboardPolicy.Decide(
            PlaybackDirectionalInput.Left,
            previewModifierDown: true,
            seekPreviewEnabled: false,
            moreVisible: false);

        Assert.Equal(PlaybackSeekPreviewKeyboardAction.None, action);
    }
}
