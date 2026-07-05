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
    public void Cancel_Goes_Back_When_Overlay_Is_Hidden()
    {
        var action = PlaybackOverlayInputPolicy.Decide(PlaybackOverlayShortcut.Cancel, seekPreviewActive: false, moreVisible: false, overlayVisible: false);

        Assert.Equal(PlaybackOverlayInputAction.GoBack, action);
    }
}
