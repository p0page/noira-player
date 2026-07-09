namespace NoiraPlayer.Core.Input
{
    public enum HomeRenderFocusBehavior
    {
        FocusDailyStart,
        RestoreExistingFocus
    }

    public sealed class HomeLoadDecision
    {
        public HomeLoadDecision(
            bool shouldLoad,
            bool shouldClearExistingContent,
            bool shouldRestoreContentFocus,
            string statusText)
        {
            ShouldLoad = shouldLoad;
            ShouldClearExistingContent = shouldClearExistingContent;
            ShouldRestoreContentFocus = shouldRestoreContentFocus;
            StatusText = statusText ?? "";
        }

        public bool ShouldLoad { get; }

        public bool ShouldClearExistingContent { get; }

        public bool ShouldRestoreContentFocus { get; }

        public string StatusText { get; }
    }

    public static class HomeLoadPolicy
    {
        public static HomeLoadDecision ForPageLoaded(bool hasRenderedContent, bool isLoading)
        {
            if (hasRenderedContent || isLoading)
            {
                return new HomeLoadDecision(false, false, hasRenderedContent, "");
            }

            return new HomeLoadDecision(true, true, false, "Loading...");
        }

        public static HomeLoadDecision ForRefreshRequested(bool hasRenderedContent)
        {
            return hasRenderedContent
                ? new HomeLoadDecision(true, false, false, "Refreshing...")
                : new HomeLoadDecision(true, true, false, "Loading...");
        }

        public static HomeLoadDecision ForLoadFailure(bool hasRenderedContent)
        {
            return hasRenderedContent
                ? new HomeLoadDecision(false, false, false, "Unable to refresh home. Showing last loaded content.")
                : new HomeLoadDecision(false, true, false, "Unable to load home.");
        }

        public static HomeRenderFocusBehavior ForRenderCompleted(
            bool hadRenderedContentBeforeRender,
            bool isSupplementalRender)
        {
            return hadRenderedContentBeforeRender && isSupplementalRender
                ? HomeRenderFocusBehavior.RestoreExistingFocus
                : HomeRenderFocusBehavior.FocusDailyStart;
        }
    }
}
