using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityThroughputAttributionPolicyTests
{
    [Fact]
    public void Assess_Attributes_Dominant_Transport_Wait_Even_When_Media_Window_Completes()
    {
        var report = new PlaybackQualityReport
        {
            Execution = new PlaybackQualityExecutionEvidence
            {
                RequestedSampleDurationMs = 30000
            }
        };
        report.Buffers.PlaybackTransportCallEvidenceStatus = "available";
        report.Buffers.PlaybackTransportReadWaitMs = 7500;

        var attribution = PlaybackQualityThroughputAttributionPolicy.Assess(
            report,
            sampleIncomplete: false);

        Assert.Equal("transport-wait-dominant", attribution.Attribution);
    }

    [Fact]
    public void Assess_Attributes_Transport_Wait_When_It_Can_Explain_The_Media_Shortfall()
    {
        var report = new PlaybackQualityReport
        {
            Execution = new PlaybackQualityExecutionEvidence
            {
                RequestedSampleDurationMs = 30000
            }
        };
        report.Source.FrameRate = 60;
        report.Timing.RenderedVideoFrames = 1642;
        report.Buffers.PlaybackTransportCallEvidenceStatus = "available";
        report.Buffers.PlaybackTransportReadWaitMs = 4765;

        var attribution = PlaybackQualityThroughputAttributionPolicy.Assess(
            report,
            sampleIncomplete: true);

        Assert.Equal("transport-wait-dominant", attribution.Attribution);
        Assert.Contains("media shortfall", attribution.Reason);
    }
}
