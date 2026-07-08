namespace NextGenEmby.Core.Input
{
    public enum SearchFocusArea
    {
        Other,
        SearchBox,
        SearchAction,
        ScopeRail,
        RecentTerms,
        ResultGrid,
        EmptyState
    }

    public enum SearchFocusNavigationAction
    {
        None,
        FocusSearchBox,
        FocusSelectedScope,
        FocusRecentTerms,
        FocusFirstResult,
        FocusEmptyState,
        MoveScopeLeft,
        MoveScopeRight,
        MoveRecentLeft,
        MoveRecentRight
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
            bool focusedResultInFirstRow,
            bool emptyStateVisible = false,
            bool recentTermsVisible = false)
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
                    if (recentTermsVisible)
                    {
                        return Decision(SearchFocusNavigationAction.FocusRecentTerms);
                    }

                    if (emptyStateVisible)
                    {
                        return Decision(SearchFocusNavigationAction.FocusEmptyState);
                    }

                    return Decision(SearchFocusNavigationAction.FocusFirstResult);
                }

                if (focusArea == SearchFocusArea.RecentTerms)
                {
                    if (emptyStateVisible)
                    {
                        return Decision(SearchFocusNavigationAction.FocusEmptyState);
                    }

                    return Decision(SearchFocusNavigationAction.FocusFirstResult);
                }
            }

            if (moveUpKeyPressed)
            {
                if (focusArea == SearchFocusArea.ScopeRail)
                {
                    return Decision(SearchFocusNavigationAction.FocusSearchBox);
                }

                if (focusArea == SearchFocusArea.RecentTerms)
                {
                    return Decision(SearchFocusNavigationAction.FocusSelectedScope);
                }

                if (focusArea == SearchFocusArea.ResultGrid && focusedResultInFirstRow)
                {
                    if (recentTermsVisible)
                    {
                        return Decision(SearchFocusNavigationAction.FocusRecentTerms);
                    }

                    return Decision(SearchFocusNavigationAction.FocusSelectedScope);
                }

                if (focusArea == SearchFocusArea.EmptyState)
                {
                    if (recentTermsVisible)
                    {
                        return Decision(SearchFocusNavigationAction.FocusRecentTerms);
                    }

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

            if (focusArea == SearchFocusArea.RecentTerms)
            {
                if (moveLeftKeyPressed)
                {
                    return Decision(SearchFocusNavigationAction.MoveRecentLeft);
                }

                if (moveRightKeyPressed)
                {
                    return Decision(SearchFocusNavigationAction.MoveRecentRight);
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
