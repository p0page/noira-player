using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class PlaybackOverlayInputPolicyTests
{
    [Theory]
    [InlineData(PlaybackOverlayShortcut.Accept)]
    [InlineData(PlaybackOverlayShortcut.Pointer)]
    public void Accept_And_Pointer_Show_Overlay_When_Seek_Preview_Is_Not_Active(PlaybackOverlayShortcut shortcut)
    {
        var action = PlaybackOverlayInputPolicy.Decide(shortcut, seekPreviewActive: false, moreVisible: false, overlayVisible: false);

        Assert.Equal(PlaybackOverlayInputAction.ShowOverlay, action);
    }

    [Theory]
    [InlineData(PlaybackOverlayShortcut.Accept)]
    [InlineData(PlaybackOverlayShortcut.Cancel)]
    public void Accept_And_Cancel_Resolve_Seek_Preview_Before_Changing_Overlay(PlaybackOverlayShortcut shortcut)
    {
        var action = PlaybackOverlayInputPolicy.Decide(shortcut, seekPreviewActive: true, moreVisible: true, overlayVisible: true);

        Assert.Equal(
            shortcut == PlaybackOverlayShortcut.Accept
                ? PlaybackOverlayInputAction.ConfirmSeekPreview
                : PlaybackOverlayInputAction.CancelSeekPreview,
            action);
    }

    [Fact]
    public void More_Opens_Drawer()
    {
        var action = PlaybackOverlayInputPolicy.Decide(PlaybackOverlayShortcut.More, seekPreviewActive: false, moreVisible: false, overlayVisible: false);

        Assert.Equal(PlaybackOverlayInputAction.ShowMore, action);
    }

    [Fact]
    public void Accept_Activates_Focused_Control_When_More_Drawer_Is_Open()
    {
        var action = PlaybackOverlayInputPolicy.Decide(PlaybackOverlayShortcut.Accept, seekPreviewActive: false, moreVisible: true, overlayVisible: true);

        Assert.Equal(PlaybackOverlayInputAction.ActivateFocusedControl, action);
    }

    [Fact]
    public void Focused_Controls_Handle_Navigation_When_More_Drawer_Is_Open()
    {
        Assert.True(PlaybackOverlayInputPolicy.ShouldRouteFocusedControlInput(moreVisible: true, seekPreviewActive: false));
    }

    [Fact]
    public void Seek_Preview_Keeps_Global_Input_When_More_Drawer_Is_Open()
    {
        Assert.False(PlaybackOverlayInputPolicy.ShouldRouteFocusedControlInput(moreVisible: true, seekPreviewActive: true));
    }

    [Fact]
    public void Cancel_Closes_More_Before_Hiding_Overlay()
    {
        var action = PlaybackOverlayInputPolicy.Decide(PlaybackOverlayShortcut.Cancel, seekPreviewActive: false, moreVisible: true, overlayVisible: true);

        Assert.Equal(PlaybackOverlayInputAction.CloseMore, action);
    }

    [Fact]
    public void Cancel_Hides_Overlay_When_Drawer_Is_Closed()
    {
        var action = PlaybackOverlayInputPolicy.Decide(PlaybackOverlayShortcut.Cancel, seekPreviewActive: false, moreVisible: false, overlayVisible: true);

        Assert.Equal(PlaybackOverlayInputAction.HideOverlay, action);
    }

    [Fact]
    public void Cancel_Goes_Back_When_Back_Should_Exit_Playback_Page()
    {
        var action = PlaybackOverlayInputPolicy.Decide(
            PlaybackOverlayShortcut.Cancel,
            seekPreviewActive: false,
            moreVisible: false,
            overlayVisible: true,
            preferBackWhenOverlayVisible: true);

        Assert.Equal(PlaybackOverlayInputAction.GoBack, action);
    }

    [Fact]
    public void Cancel_Closes_More_Before_Exiting_Playback_Page()
    {
        var action = PlaybackOverlayInputPolicy.Decide(
            PlaybackOverlayShortcut.Cancel,
            seekPreviewActive: false,
            moreVisible: true,
            overlayVisible: true,
            preferBackWhenOverlayVisible: true);

        Assert.Equal(PlaybackOverlayInputAction.CloseMore, action);
    }

    [Fact]
    public void Cancel_Goes_Back_When_Overlay_Is_Hidden()
    {
        var action = PlaybackOverlayInputPolicy.Decide(PlaybackOverlayShortcut.Cancel, seekPreviewActive: false, moreVisible: false, overlayVisible: false);

        Assert.Equal(PlaybackOverlayInputAction.GoBack, action);
    }
}
