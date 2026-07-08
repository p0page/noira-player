namespace NoiraPlayer.Core.Emby
{
    public static class SearchResultStatusTextPolicy
    {
        public static string Create(int resultCount, string scopeLabel)
        {
            var suffix = resultCount == 1 ? "result" : "results";
            return resultCount + " " + suffix + " / " + (scopeLabel ?? "");
        }
    }
}
