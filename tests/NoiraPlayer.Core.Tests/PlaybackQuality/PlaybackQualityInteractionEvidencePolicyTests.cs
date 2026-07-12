using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityInteractionEvidencePolicyTests
{
    [Theory]
    [InlineData(1_000_000, 1_000_000, 1_000_000, 2_000_000, 10, 11, true)]
    [InlineData(1_000_000, 1_200_001, 1_200_001, 2_000_000, 10, 11, false)]
    [InlineData(1_000_000, 1_000_000, 1_000_000, 1_000_000, 10, 11, false)]
    [InlineData(1_000_000, 1_000_000, 1_000_000, 2_000_000, 10, 10, false)]
    public void PauseResume_Requires_Frozen_Pause_And_PostResume_Progress(
        long positionBeforePause,
        long positionDuringPause,
        long positionBeforeResume,
        long positionAfterResume,
        ulong renderedFramesBefore,
        ulong renderedFramesAfter,
        bool expected)
    {
        Assert.Equal(
            expected,
            PlaybackQualityInteractionEvidencePolicy.IsPauseResumeRecovered(
                positionBeforePause,
                positionDuringPause,
                positionBeforeResume,
                positionAfterResume,
                renderedFramesBefore,
                renderedFramesAfter));
    }

    [Theory]
    [InlineData(2, 2, 1_000_000, 2_000_000, 100, 101, true)]
    [InlineData(2, 1, 1_000_000, 2_000_000, 100, 101, false)]
    [InlineData(2, 2, 1_000_000, 1_000_000, 100, 101, false)]
    [InlineData(2, 2, 1_000_000, 2_000_000, 100, 100, false)]
    public void AudioSwitch_Requires_Target_Selection_Timeline_And_Audio_Progress(
        int targetStreamIndex,
        int selectedStreamIndex,
        long positionBefore,
        long positionAfter,
        ulong submittedAudioFramesBefore,
        ulong submittedAudioFramesAfter,
        bool expected)
    {
        Assert.Equal(
            expected,
            PlaybackQualityInteractionEvidencePolicy.IsAudioSwitchRecovered(
                targetStreamIndex,
                selectedStreamIndex,
                positionBefore,
                positionAfter,
                submittedAudioFramesBefore,
                submittedAudioFramesAfter));
    }
}
