namespace NextGenEmby.Core.Playback
{
    public enum PlaybackOverlayShortcut
    {
        Accept,
        Cancel,
        More,
        Pointer
    }

    public enum PlaybackOverlayInputAction
    {
        None,
        ShowOverlay,
        ShowMore,
        CloseMore,
        HideOverlay,
        GoBack,
        ConfirmSeekPreview,
        CancelSeekPreview,
        ActivateFocusedControl
    }

    public static class PlaybackOverlayInputPolicy
    {
        public static PlaybackOverlayInputAction Decide(
            PlaybackOverlayShortcut shortcut,
            bool seekPreviewActive,
            bool moreVisible,
            bool overlayVisible,
            bool preferBackWhenOverlayVisible = false)
        {
            switch (shortcut)
            {
                case PlaybackOverlayShortcut.Accept:
                    if (seekPreviewActive)
                    {
                        return PlaybackOverlayInputAction.ConfirmSeekPreview;
                    }

                    return moreVisible
                        ? PlaybackOverlayInputAction.ActivateFocusedControl
                        : PlaybackOverlayInputAction.ShowOverlay;

                case PlaybackOverlayShortcut.Cancel:
                    if (seekPreviewActive)
                    {
                        return PlaybackOverlayInputAction.CancelSeekPreview;
                    }

                    if (moreVisible)
                    {
                        return PlaybackOverlayInputAction.CloseMore;
                    }

                    if (!overlayVisible)
                    {
                        return PlaybackOverlayInputAction.GoBack;
                    }

                    return PlaybackOverlayInputAction.HideOverlay;

                case PlaybackOverlayShortcut.More:
                    return PlaybackOverlayInputAction.ShowMore;

                case PlaybackOverlayShortcut.Pointer:
                    return PlaybackOverlayInputAction.ShowOverlay;

                default:
                    return PlaybackOverlayInputAction.None;
            }
        }

        public static bool ShouldRouteFocusedControlInput(
            bool moreVisible,
            bool seekPreviewActive)
        {
            return moreVisible && !seekPreviewActive;
        }

        public static bool ShouldRouteHandledShortcutInput(
            PlaybackOverlayShortcut shortcut,
            bool seekPreviewActive,
            bool moreVisible,
            bool moreDrawerComboBoxOpen)
        {
            if (moreDrawerComboBoxOpen)
            {
                return false;
            }

            if (seekPreviewActive)
            {
                return shortcut == PlaybackOverlayShortcut.Accept ||
                    shortcut == PlaybackOverlayShortcut.Cancel;
            }

            return moreVisible && shortcut == PlaybackOverlayShortcut.Cancel;
        }

        public static bool ShouldKeepOverlayPinned(
            bool moreVisible,
            bool seekPreviewActive,
            bool manualDebugVisible,
            bool playbackOpeningOrBusy = false,
            bool playbackNeedsAttention = false)
        {
            return moreVisible ||
                seekPreviewActive ||
                manualDebugVisible ||
                playbackOpeningOrBusy ||
                playbackNeedsAttention;
        }
    }
}
