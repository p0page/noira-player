namespace NextGenEmby.Core.Input
{
    public enum GuideNavigationAction
    {
        None,
        OpenGuide,
        CloseGuide,
        Navigate
    }

    public enum GuideNavigationDestination
    {
        Home,
        Search,
        Movies,
        Tv,
        LiveTv,
        Collections,
        Music,
        Photos,
        Settings
    }

    public sealed class GuideNavigationDecision
    {
        public GuideNavigationDecision(
            GuideNavigationAction action,
            GuideNavigationDestination destination,
            bool shouldRestorePreviousFocus)
        {
            Action = action;
            Destination = destination;
            ShouldRestorePreviousFocus = shouldRestorePreviousFocus;
        }

        public GuideNavigationAction Action { get; }

        public GuideNavigationDestination Destination { get; }

        public bool ShouldRestorePreviousFocus { get; }
    }

    public static class GuideNavigationPolicy
    {
        public static GuideNavigationDecision GetDecision(
            bool eventAlreadyHandled,
            bool playbackPageActive,
            bool guideOpen,
            bool menuKeyPressed,
            bool backKeyPressed,
            bool selectKeyPressed,
            GuideNavigationDestination selectedDestination)
        {
            if (eventAlreadyHandled || playbackPageActive)
            {
                return None(selectedDestination);
            }

            if (guideOpen)
            {
                if (backKeyPressed)
                {
                    return new GuideNavigationDecision(
                        GuideNavigationAction.CloseGuide,
                        selectedDestination,
                        shouldRestorePreviousFocus: true);
                }

                if (selectKeyPressed)
                {
                    return new GuideNavigationDecision(
                        GuideNavigationAction.Navigate,
                        selectedDestination,
                        shouldRestorePreviousFocus: false);
                }

                return None(selectedDestination);
            }

            if (menuKeyPressed)
            {
                return new GuideNavigationDecision(
                    GuideNavigationAction.OpenGuide,
                    selectedDestination,
                    shouldRestorePreviousFocus: false);
            }

            return None(selectedDestination);
        }

        private static GuideNavigationDecision None(GuideNavigationDestination selectedDestination)
        {
            return new GuideNavigationDecision(
                GuideNavigationAction.None,
                selectedDestination,
                shouldRestorePreviousFocus: false);
        }
    }
}
