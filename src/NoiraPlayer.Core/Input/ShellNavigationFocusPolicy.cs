namespace NoiraPlayer.Core.Input
{
    public enum ShellNavigationFocusTarget
    {
        None,
        Shell,
        Content
    }

    public static class ShellNavigationFocusPolicy
    {
        public static ShellNavigationFocusTarget GetFocusTarget(
            bool isPlaybackPage,
            bool isBackNavigation,
            bool hasContentFocusTarget)
        {
            return GetFocusTarget(
                isPlaybackPage ? ShellContentMode.Playback : ShellContentMode.Standard,
                isBackNavigation,
                hasContentFocusTarget);
        }

        public static ShellNavigationFocusTarget GetFocusTarget(
            ShellContentMode contentMode,
            bool isBackNavigation,
            bool hasContentFocusTarget)
        {
            if (contentMode == ShellContentMode.Playback)
            {
                return ShellNavigationFocusTarget.None;
            }

            if ((contentMode == ShellContentMode.Login ||
                    isBackNavigation ||
                    contentMode == ShellContentMode.MediaDetails ||
                    contentMode == ShellContentMode.PhotoViewer) &&
                hasContentFocusTarget)
            {
                return ShellNavigationFocusTarget.Content;
            }

            return ShellNavigationFocusTarget.Shell;
        }
    }
}
