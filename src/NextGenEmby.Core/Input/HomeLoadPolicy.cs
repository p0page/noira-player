namespace NextGenEmby.Core.Input
{
    public sealed class HomeLoadDecision
    {
        public HomeLoadDecision(bool shouldLoad, bool shouldClearExistingContent, string statusText)
        {
            ShouldLoad = shouldLoad;
            ShouldClearExistingContent = shouldClearExistingContent;
            StatusText = statusText ?? "";
        }

        public bool ShouldLoad { get; }

        public bool ShouldClearExistingContent { get; }

        public string StatusText { get; }
    }

    public static class HomeLoadPolicy
    {
        public static HomeLoadDecision ForPageLoaded(bool hasRenderedContent, bool isLoading)
        {
            if (hasRenderedContent || isLoading)
            {
                return new HomeLoadDecision(false, false, "");
            }

            return new HomeLoadDecision(true, true, "Loading...");
        }

        public static HomeLoadDecision ForRefreshRequested(bool hasRenderedContent)
        {
            return hasRenderedContent
                ? new HomeLoadDecision(true, false, "Refreshing...")
                : new HomeLoadDecision(true, true, "Loading...");
        }

        public static HomeLoadDecision ForLoadFailure(bool hasRenderedContent)
        {
            return hasRenderedContent
                ? new HomeLoadDecision(false, false, "Unable to refresh home. Showing last loaded content.")
                : new HomeLoadDecision(false, true, "Unable to load home.");
        }
    }
}
