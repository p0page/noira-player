namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityCandidateEvidenceGatePolicy
    {
        public static bool CanContinueReportAnalysis(
            int blockedReportCount,
            bool hasActionableCoreTarget,
            bool hasOnlyIsolatedNonCoreBlockers)
        {
            if (blockedReportCount <= 0)
            {
                return true;
            }

            return hasActionableCoreTarget && hasOnlyIsolatedNonCoreBlockers;
        }
    }
}
