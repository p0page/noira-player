using NoiraPlayer.Core.Playback;
using Xunit;

namespace NoiraPlayer.Core.Tests.Playback;

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

    [Theory]
    [InlineData(true, false, false, true, true)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(true, false, false, false, false)]
    public void Collapsed_More_Drawer_ComboBox_Routes_Vertical_Navigation_To_Drawer_Focus(
        bool moreVisible,
        bool seekPreviewActive,
        bool comboBoxOpen,
        bool verticalNavigation,
        bool expected)
    {
        var shouldRoute = PlaybackOverlayInputPolicy.ShouldRouteMoreDrawerComboBoxDirectionalInput(
            moreVisible,
            seekPreviewActive,
            comboBoxOpen,
            verticalNavigation);

        Assert.Equal(expected, shouldRoute);
    }

    [Fact]
    public void Handled_Cancel_Still_Routes_When_More_Drawer_Is_Open()
    {
        var shouldRoute = PlaybackOverlayInputPolicy.ShouldRouteHandledShortcutInput(
            PlaybackOverlayShortcut.Cancel,
            seekPreviewActive: false,
            moreVisible: true,
            moreDrawerComboBoxOpen: false);

        Assert.True(shouldRoute);
    }

    [Fact]
    public void Handled_Cancel_Stays_With_Open_Combo_Box()
    {
        var shouldRoute = PlaybackOverlayInputPolicy.ShouldRouteHandledShortcutInput(
            PlaybackOverlayShortcut.Cancel,
            seekPreviewActive: false,
            moreVisible: true,
            moreDrawerComboBoxOpen: true);

        Assert.False(shouldRoute);
    }

    [Fact]
    public void Handled_Accept_Stays_With_Focused_More_Drawer_Control()
    {
        var shouldRoute = PlaybackOverlayInputPolicy.ShouldRouteHandledShortcutInput(
            PlaybackOverlayShortcut.Accept,
            seekPreviewActive: false,
            moreVisible: true,
            moreDrawerComboBoxOpen: false);

        Assert.False(shouldRoute);
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
    public void Cancel_Hides_Visible_Overlay_Even_When_Back_Should_Exit_Playback_Page()
    {
        var action = PlaybackOverlayInputPolicy.Decide(
            PlaybackOverlayShortcut.Cancel,
            seekPreviewActive: false,
            moreVisible: false,
            overlayVisible: true,
            preferBackWhenOverlayVisible: true);

        Assert.Equal(PlaybackOverlayInputAction.HideOverlay, action);
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

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void Overlay_Should_Pin_When_More_Seek_Or_Manual_Debug_Is_Active(
        bool moreVisible,
        bool seekPreviewActive,
        bool manualDebugVisible,
        bool expected)
    {
        var shouldPin = PlaybackOverlayInputPolicy.ShouldKeepOverlayPinned(
            moreVisible,
            seekPreviewActive,
            manualDebugVisible);

        Assert.Equal(expected, shouldPin);
    }

    [Fact]
    public void Overlay_Should_Pin_While_Playback_Is_Opening_Or_Busy()
    {
        var shouldPin = PlaybackOverlayInputPolicy.ShouldKeepOverlayPinned(
            moreVisible: false,
            seekPreviewActive: false,
            manualDebugVisible: false,
            playbackOpeningOrBusy: true);

        Assert.True(shouldPin);
    }

    [Fact]
    public void Overlay_Should_Pin_When_Playback_Needs_Attention()
    {
        var shouldPin = PlaybackOverlayInputPolicy.ShouldKeepOverlayPinned(
            moreVisible: false,
            seekPreviewActive: false,
            manualDebugVisible: false,
            playbackNeedsAttention: true);

        Assert.True(shouldPin);
    }
}
