namespace NextGenEmby.Core.Input
{
    public enum SearchFocusArea
    {
        Other,
        SearchBox,
        SearchAction,
        ScopeRail,
        ResultGrid
    }

    public enum SearchFocusNavigationAction
    {
        None,
        FocusSearchBox,
        FocusSelectedScope,
        FocusFirstResult,
        MoveScopeLeft,
        MoveScopeRight
    }

    public sealed class SearchFocusNavigationDecision
    {
        public SearchFocusNavigationDecision(SearchFocusNavigationAction action)
        {
            Action = action;
        }

        public SearchFocusNavigationAction Action { get; }
    }

    public static class SearchFocusNavigationPolicy
    {
        public static SearchFocusNavigationDecision GetDecision(
            bool eventAlreadyHandled,
            SearchFocusArea focusArea,
            bool moveUpKeyPressed,
            bool moveDownKeyPressed,
            bool moveLeftKeyPressed,
            bool moveRightKeyPressed,
            bool focusedResultInFirstRow)
        {
            if (moveDownKeyPressed)
            {
                if (focusArea == SearchFocusArea.SearchBox ||
                    focusArea == SearchFocusArea.SearchAction)
                {
                    return Decision(SearchFocusNavigationAction.FocusSelectedScope);
                }

                if (focusArea == SearchFocusArea.ScopeRail)
                {
                    return Decision(SearchFocusNavigationAction.FocusFirstResult);
                }
            }

            if (moveUpKeyPressed)
            {
                if (focusArea == SearchFocusArea.ScopeRail)
                {
                    return Decision(SearchFocusNavigationAction.FocusSearchBox);
                }

                if (focusArea == SearchFocusArea.ResultGrid && focusedResultInFirstRow)
                {
                    return Decision(SearchFocusNavigationAction.FocusSelectedScope);
                }
            }

            if (focusArea == SearchFocusArea.ScopeRail)
            {
                if (moveLeftKeyPressed)
                {
                    return Decision(SearchFocusNavigationAction.MoveScopeLeft);
                }

                if (moveRightKeyPressed)
                {
                    return Decision(SearchFocusNavigationAction.MoveScopeRight);
                }
            }

            if (eventAlreadyHandled)
            {
                return Decision(SearchFocusNavigationAction.None);
            }

            return Decision(SearchFocusNavigationAction.None);
        }

        private static SearchFocusNavigationDecision Decision(SearchFocusNavigationAction action)
        {
            return new SearchFocusNavigationDecision(action);
        }
    }
}
