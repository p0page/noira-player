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
        CancelSeekPreview
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
                    return seekPreviewActive
                        ? PlaybackOverlayInputAction.ConfirmSeekPreview
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

                    if (!overlayVisible || preferBackWhenOverlayVisible)
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
    }
}
