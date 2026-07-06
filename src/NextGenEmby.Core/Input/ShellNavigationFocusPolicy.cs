namespace NextGenEmby.Core.Input
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
            if (isPlaybackPage)
            {
                return ShellNavigationFocusTarget.None;
            }

            if (isBackNavigation && hasContentFocusTarget)
            {
                return ShellNavigationFocusTarget.Content;
            }

            return ShellNavigationFocusTarget.Shell;
        }
    }
}
