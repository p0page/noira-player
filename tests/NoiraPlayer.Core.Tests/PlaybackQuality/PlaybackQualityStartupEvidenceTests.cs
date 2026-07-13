using System.Linq;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityStartupEvidenceTests
{
    [Fact]
    public void EnrichNativeOpenBreakdown_Attributes_Ffmpeg_Graph_And_Host_Durations()
    {
        var startup = new PlaybackQualityStartup();
        startup.Stages.Add(new PlaybackQualityStartupStage
        {
            Name = "native.open",
            DurationMs = 5000
        });
        var metrics = new PlaybackQualityMetricsSnapshot
        {
            NativeGraphOpenDurationMs = 4800,
            FfmpegOpenInputDurationMs = 4000,
            FfmpegStreamInfoDurationMs = 500,
            NativeStartupSeekDurationMs = 125,
            FfmpegOpenInputBytesRead = 65_536,
            FfmpegStreamInfoBytesRead = 1_048_576,
            NativeStartupSeekBytesRead = 16_777_216,
            NativeFirstFrameDurationMs = 100,
            NativeFirstFrameDemuxReadDurationMs = 60,
            NativeFirstFramePresentDurationMs = 5,
            NativeFirstFrameDemuxPacketCount = 200,
            NativeFirstFrameDemuxBytes = 1_000_000,
            NativeFirstFrameTransportBytesRead = 1_250_000
        };

        PlaybackQualityStartupEvidence.EnrichNativeOpenBreakdown(startup, metrics);

        var stage = Assert.Single(startup.Stages, value => value.Name == "native.open");
        Assert.Collection(
            stage.Components,
            component => Assert.Equal(("ffmpeg.open-input", 4000, "measured", 65_536UL), (component.Name, component.DurationMs, component.Status, component.TransportBytes)),
            component => Assert.Equal(("ffmpeg.find-stream-info", 500, "measured", 1_048_576UL), (component.Name, component.DurationMs, component.Status, component.TransportBytes)),
            component => Assert.Equal(("native.initialize-components", 75), (component.Name, component.DurationMs)),
            component => Assert.Equal(("native.startup-seek", 125, 16_777_216UL), (component.Name, component.DurationMs, component.TransportBytes)),
            component => Assert.Equal(("native.first-frame.demux-read", 60, 200UL, 1_250_000UL, 1_000_000UL), (component.Name, component.DurationMs, component.PacketCount, component.TransportBytes, component.PacketPayloadBytes)),
            component => Assert.Equal(("native.first-frame.decode-control", 35), (component.Name, component.DurationMs)),
            component => Assert.Equal(("native.first-frame.present", 5), (component.Name, component.DurationMs)),
            component => Assert.Equal(("host.dispatch-overhead", 200), (component.Name, component.DurationMs)));
    }

}
