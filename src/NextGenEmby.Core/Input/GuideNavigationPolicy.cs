namespace NextGenEmby.Core.Input
{
    public enum GuideNavigationAction
    {
        None,
        OpenGuide,
        CloseGuide,
        MoveSelection,
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
        Playlists,
        Music,
        Photos,
        Favorites,
        Unwatched,
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
        private static readonly GuideNavigationDestination[] DestinationOrder =
        {
            GuideNavigationDestination.Home,
            GuideNavigationDestination.Search,
            GuideNavigationDestination.Movies,
            GuideNavigationDestination.Tv,
            GuideNavigationDestination.LiveTv,
            GuideNavigationDestination.Collections,
            GuideNavigationDestination.Playlists,
            GuideNavigationDestination.Music,
            GuideNavigationDestination.Photos,
            GuideNavigationDestination.Favorites,
            GuideNavigationDestination.Unwatched,
            GuideNavigationDestination.Settings
        };

        public static GuideNavigationDecision GetDecision(
            bool eventAlreadyHandled,
            bool playbackPageActive,
            bool guideOpen,
            bool menuKeyPressed,
            bool backKeyPressed,
            bool selectKeyPressed,
            bool moveUpKeyPressed,
            bool moveDownKeyPressed,
            GuideNavigationDestination selectedDestination)
        {
            if (playbackPageActive)
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

                if (moveUpKeyPressed || moveDownKeyPressed)
                {
                    return new GuideNavigationDecision(
                        GuideNavigationAction.MoveSelection,
                        MoveSelection(selectedDestination, moveDownKeyPressed ? 1 : -1),
                        shouldRestorePreviousFocus: false);
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

            if (eventAlreadyHandled)
            {
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

        private static GuideNavigationDestination MoveSelection(
            GuideNavigationDestination selectedDestination,
            int offset)
        {
            var currentIndex = 0;
            for (var i = 0; i < DestinationOrder.Length; i++)
            {
                if (DestinationOrder[i] == selectedDestination)
                {
                    currentIndex = i;
                    break;
                }
            }

            var nextIndex = currentIndex + offset;
            if (nextIndex < 0)
            {
                nextIndex = 0;
            }
            else if (nextIndex >= DestinationOrder.Length)
            {
                nextIndex = DestinationOrder.Length - 1;
            }

            return DestinationOrder[nextIndex];
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
