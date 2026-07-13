using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityCandidateEvidenceGatePolicyTests
{
    [Fact]
    public void CanContinueReportAnalysis_Allows_No_Blockers()
    {
        Assert.True(PlaybackQualityCandidateEvidenceGatePolicy.CanContinueReportAnalysis(
            blockedReportCount: 0,
            hasActionableCoreTarget: false,
            hasOnlyIsolatedNonCoreBlockers: false));
    }

    [Fact]
    public void CanContinueReportAnalysis_Allows_Isolated_NonCore_Blockers_With_Core_Targets()
    {
        Assert.True(PlaybackQualityCandidateEvidenceGatePolicy.CanContinueReportAnalysis(
            blockedReportCount: 2,
            hasActionableCoreTarget: true,
            hasOnlyIsolatedNonCoreBlockers: true));
    }

    [Fact]
    public void CanContinueReportAnalysis_Rejects_Isolated_Blockers_Without_Core_Targets()
    {
        Assert.False(PlaybackQualityCandidateEvidenceGatePolicy.CanContinueReportAnalysis(
            blockedReportCount: 2,
            hasActionableCoreTarget: false,
            hasOnlyIsolatedNonCoreBlockers: true));
    }

    [Fact]
    public void CanContinueReportAnalysis_Rejects_Mixed_Blockers()
    {
        Assert.False(PlaybackQualityCandidateEvidenceGatePolicy.CanContinueReportAnalysis(
            blockedReportCount: 2,
            hasActionableCoreTarget: true,
            hasOnlyIsolatedNonCoreBlockers: false));
    }
}
