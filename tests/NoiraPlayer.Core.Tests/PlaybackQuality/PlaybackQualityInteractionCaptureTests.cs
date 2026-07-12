using System;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityInteractionCaptureTests
{
    [Fact]
    public void Create_AudioSwitch_Combines_App_Recovery_With_Native_Phases()
    {
        var before = Snapshot(sequence: 4, position: 10_000_000, audio: 20, video: 30, cues: 0);
        var after = Snapshot(sequence: 5, position: 12_000_000, audio: 27, video: 34, cues: 0);
        after.LastInteractionScenario = "audio-switch";
        after.LastInteractionExecutionDurationMs = 38.5;
        after.LastInteractionSeekDurationMs = 0;
        after.LastInteractionPacketCacheEnabled = true;
        after.LastInteractionPacketCacheHit = true;
        after.LastInteractionPacketCachePacketCount = 176;
        after.LastInteractionPacketCacheBytes = 180224;
        after.LastInteractionPacketCacheWindowDurationTicks = 18_660_000;

        var evidence = PlaybackQualityInteractionCapture.Create(
            "audio-switch", 40.0, 150.0, null, before, after);

        Assert.True(evidence.Attempted);
        Assert.Equal("audio-switch", evidence.Scenario);
        Assert.Equal(40.0, evidence.OperationDurationMs);
        Assert.Equal(38.5, evidence.ExecutionDurationMs);
        Assert.Equal(0, evidence.SeekDurationMs);
        Assert.True(evidence.PacketCacheEnabled);
        Assert.True(evidence.PacketCacheHit);
        Assert.Equal(2_000_000, evidence.PositionDeltaTicks);
        Assert.Equal(7UL, evidence.SubmittedAudioFrameDelta);
        Assert.Equal(4UL, evidence.RenderedVideoFrameDelta);
        Assert.Equal(0UL, evidence.SubtitleCueRenderCountDelta);
    }

    [Fact]
    public void Create_SubtitleSwitch_Preserves_Cue_Duration_And_Deltas()
    {
        var before = Snapshot(sequence: 9, position: 20_000_000, audio: 40, video: 50, cues: 2);
        var after = Snapshot(sequence: 10, position: 21_000_000, audio: 42, video: 53, cues: 4);
        after.LastInteractionScenario = "subtitle-switch";
        after.LastInteractionPacketCacheEnabled = true;
        after.LastInteractionPacketCacheHit = true;

        var evidence = PlaybackQualityInteractionCapture.Create(
            "subtitle-switch", 0.8, 160.0, 175.0, before, after);

        Assert.Equal(175.0, evidence.CueRenderDurationMs);
        Assert.Equal(3UL, evidence.RenderedVideoFrameDelta);
        Assert.Equal(2UL, evidence.SubtitleCueRenderCountDelta);
    }

    [Theory]
    [InlineData("audio-switch", "subtitle-switch", 5, 6)]
    [InlineData("audio-switch", "audio-switch", 5, 5)]
    public void Create_Rejects_Mismatched_Or_Stale_Native_Interaction(
        string requestedScenario,
        string nativeScenario,
        ulong beforeSequence,
        ulong afterSequence)
    {
        var before = Snapshot(beforeSequence, 1, 1, 1, 1);
        var after = Snapshot(afterSequence, 2, 2, 2, 2);
        after.LastInteractionScenario = nativeScenario;

        Assert.Throws<InvalidOperationException>(() => PlaybackQualityInteractionCapture.Create(
            requestedScenario, 1, 2, null, before, after));
    }

    private static PlaybackQualityMetricsSnapshot Snapshot(
        ulong sequence,
        long position,
        ulong audio,
        ulong video,
        ulong cues)
    {
        return new PlaybackQualityMetricsSnapshot
        {
            LastInteractionSequence = sequence,
            VideoPositionTicks = position,
            SubmittedAudioFrames = audio,
            RenderedVideoFrames = video,
            SubtitleCueRenderCount = cues
        };
    }
}
