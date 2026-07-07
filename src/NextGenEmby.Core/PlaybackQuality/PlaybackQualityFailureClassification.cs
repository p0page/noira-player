namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityFailureClassification
    {
        public const string PlayerCoreBug = "player-core bug";
        public const string UnsupportedByCurrentMvp = "unsupported by current MVP";
        public const string EvaluationHarnessBug = "evaluation harness bug";
        public const string SampleIssue = "sample issue";
        public const string EnvironmentIssue = "environment issue";
        public const string ExternalServiceOrProtocolIssue = "external service/protocol issue";
        public const string InsufficientInstrumentation = "insufficient instrumentation";
        public const string AmbiguousExpectation = "ambiguous expectation";
        public const string FlakyOrNondeterministic = "flaky / nondeterministic";
        public const string NeedsHumanConfirmation = "needs human confirmation";

        public static readonly string[] KnownFailureClasses =
        {
            PlayerCoreBug,
            UnsupportedByCurrentMvp,
            EvaluationHarnessBug,
            SampleIssue,
            EnvironmentIssue,
            ExternalServiceOrProtocolIssue,
            InsufficientInstrumentation,
            AmbiguousExpectation,
            FlakyOrNondeterministic,
            NeedsHumanConfirmation
        };

        public static bool IsKnown(string failureClass)
        {
            if (string.IsNullOrWhiteSpace(failureClass))
            {
                return false;
            }

            foreach (var known in KnownFailureClasses)
            {
                if (string.Equals(
                    known,
                    failureClass,
                    System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static string Classify(PlaybackQualityCheck check)
        {
            if (check == null || check.Status != "fail")
            {
                return "";
            }

            if (!string.IsNullOrWhiteSpace(check.FailureClass))
            {
                return check.FailureClass;
            }

            if (IsMissingEvidence(check))
            {
                return InsufficientInstrumentation;
            }

            if (IsUnsupportedByCurrentMvp(check))
            {
                return UnsupportedByCurrentMvp;
            }

            if (string.IsNullOrWhiteSpace(check.FailureArea) ||
                string.IsNullOrWhiteSpace(check.Expected))
            {
                return AmbiguousExpectation;
            }

            return PlayerCoreBug;
        }

        private static bool IsMissingEvidence(PlaybackQualityCheck check)
        {
            return string.IsNullOrWhiteSpace(check.Actual) ||
                check.Message.IndexOf(
                    " is missing ",
                    System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUnsupportedByCurrentMvp(PlaybackQualityCheck check)
        {
            if (check.FailureArea != "unsupported-source")
            {
                return false;
            }

            if (check.Name == "ExpectedIsDirectPlayable" &&
                check.Expected == "True" &&
                check.Actual == "False")
            {
                return true;
            }

            return check.Name == "ExpectedHdrPlaybackStrategy" &&
                check.Actual.IndexOf(
                    "Unsupported",
                    System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
