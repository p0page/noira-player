namespace NextGenEmby.Core.Playback
{
    public enum PlaybackDirectionalInput
    {
        Other,
        Left,
        Right
    }

    public enum PlaybackSeekPreviewKeyboardAction
    {
        None,
        PreviewBackward,
        PreviewForward
    }

    public static class PlaybackSeekPreviewKeyboardPolicy
    {
        public static PlaybackSeekPreviewKeyboardAction Decide(
            PlaybackDirectionalInput input,
            bool previewModifierDown,
            bool seekPreviewEnabled,
            bool moreVisible)
        {
            if (!previewModifierDown ||
                !seekPreviewEnabled ||
                moreVisible)
            {
                return PlaybackSeekPreviewKeyboardAction.None;
            }

            switch (input)
            {
                case PlaybackDirectionalInput.Left:
                    return PlaybackSeekPreviewKeyboardAction.PreviewBackward;

                case PlaybackDirectionalInput.Right:
                    return PlaybackSeekPreviewKeyboardAction.PreviewForward;

                default:
                    return PlaybackSeekPreviewKeyboardAction.None;
            }
        }
    }
}
