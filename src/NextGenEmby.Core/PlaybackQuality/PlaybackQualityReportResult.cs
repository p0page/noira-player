namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityReportResult
    {
        public const string Pass = "pass";
        public const string Fail = "fail";
        public const string Skip = "skip";
        public const string Unsupported = "unsupported";
        public const string Error = "error";

        public static readonly string[] KnownResults =
        {
            Pass,
            Fail,
            Skip,
            Unsupported,
            Error
        };

        public static bool IsKnown(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                return false;
            }

            foreach (var known in KnownResults)
            {
                if (string.Equals(
                    known,
                    result,
                    System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
