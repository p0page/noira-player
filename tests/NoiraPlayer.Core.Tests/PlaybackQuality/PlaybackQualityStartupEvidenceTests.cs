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
            NativeFirstFrameTransportBytesRead = 1_250_000,
            StartupTransportProvider = "instrumented-ffmpeg-avio",
            StartupTransportCallEvidenceAvailable = true,
            FfmpegOpenInputTransportCalls = Calls(2, 1, 1200, 300, 4096),
            FfmpegStreamInfoTransportCalls = Calls(4, 2, 800, 200, 8192),
            NativeStartupSeekTransportCalls = Calls(1, 1, 400, 100, 16384),
            NativeFirstFrameTransportCalls = Calls(8, 0, 600, 0, 0)
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

        var transportComponents = stage.Components
            .Where(component => component.TransportCallEvidenceStatus == "measured")
            .ToArray();
        Assert.Equal(4, transportComponents.Length);
        Assert.All(transportComponents, component => Assert.Equal("instrumented-ffmpeg-avio", component.TransportProvider));
        Assert.Equal((2UL, 1UL, 1200D, 300D, 4096UL), CallValues(transportComponents[0]));
        Assert.Equal((4UL, 2UL, 800D, 200D, 8192UL), CallValues(transportComponents[1]));
        Assert.Equal((1UL, 1UL, 400D, 100D, 16384UL), CallValues(transportComponents[2]));
        Assert.Equal((8UL, 0UL, 600D, 0D, 0UL), CallValues(transportComponents[3]));
    }

    [Fact]
    public void EnrichNativeOpenBreakdown_Does_Not_Encode_Unavailable_Callbacks_As_Zero()
    {
        var startup = new PlaybackQualityStartup();
        startup.Stages.Add(new PlaybackQualityStartupStage { Name = "native.open", DurationMs = 100 });
        var metrics = new PlaybackQualityMetricsSnapshot
        {
            NativeGraphOpenDurationMs = 100,
            StartupTransportProvider = "ffmpeg-builtin",
            StartupTransportCallEvidenceAvailable = false
        };

        PlaybackQualityStartupEvidence.EnrichNativeOpenBreakdown(startup, metrics);

        var component = Assert.Single(startup.Stages[0].Components, value => value.Name == "ffmpeg.open-input");
        Assert.Equal("ffmpeg-builtin", component.TransportProvider);
        Assert.Equal("unavailable", component.TransportCallEvidenceStatus);
        Assert.Null(component.TransportReadCalls);
        Assert.Null(component.TransportSeekCalls);
        Assert.Null(component.TransportReadWaitMs);
        Assert.Null(component.TransportSeekWaitMs);
        Assert.Null(component.TransportSeekDistanceBytes);
    }

    [Fact]
    public void EnrichNativeOpenBreakdown_Attributes_Transport_Evidence_Per_Component()
    {
        var startup = new PlaybackQualityStartup();
        startup.Stages.Add(new PlaybackQualityStartupStage { Name = "native.open", DurationMs = 100 });
        var metrics = new PlaybackQualityMetricsSnapshot
        {
            NativeGraphOpenDurationMs = 100,
            StartupTransportProvider = "ffmpeg-builtin",
            StartupTransportCallEvidenceAvailable = false,
            FfmpegOpenInputTransportCalls = Calls(0, 0, 0, 0, 0),
            FfmpegStreamInfoTransportCalls = Calls(4, 2, 8, 3, 4096)
        };
        metrics.FfmpegOpenInputTransportCalls.Provider = "ffmpeg-builtin";
        metrics.FfmpegOpenInputTransportCalls.EvidenceAvailable = false;
        metrics.FfmpegStreamInfoTransportCalls.Provider = "instrumented-ffmpeg-avio";
        metrics.FfmpegStreamInfoTransportCalls.EvidenceAvailable = true;

        PlaybackQualityStartupEvidence.EnrichNativeOpenBreakdown(startup, metrics);

        var openInput = Assert.Single(startup.Stages[0].Components, value => value.Name == "ffmpeg.open-input");
        Assert.Equal("ffmpeg-builtin", openInput.TransportProvider);
        Assert.Equal("unavailable", openInput.TransportCallEvidenceStatus);
        Assert.Null(openInput.TransportReadCalls);

        var streamInfo = Assert.Single(startup.Stages[0].Components, value => value.Name == "ffmpeg.find-stream-info");
        Assert.Equal("instrumented-ffmpeg-avio", streamInfo.TransportProvider);
        Assert.Equal("measured", streamInfo.TransportCallEvidenceStatus);
        Assert.Equal(4UL, streamInfo.TransportReadCalls);
    }

    private static PlaybackQualityTransportCallSnapshot Calls(
        ulong reads,
        ulong seeks,
        double readWaitMs,
        double seekWaitMs,
        ulong seekDistanceBytes) => new PlaybackQualityTransportCallSnapshot
        {
            ReadCalls = reads,
            SeekCalls = seeks,
            ReadWaitMs = readWaitMs,
            SeekWaitMs = seekWaitMs,
            SeekDistanceBytes = seekDistanceBytes
        };

    private static (ulong, ulong, double, double, ulong) CallValues(PlaybackQualityStartupComponent component) =>
        (component.TransportReadCalls!.Value,
            component.TransportSeekCalls!.Value,
            component.TransportReadWaitMs!.Value,
            component.TransportSeekWaitMs!.Value,
            component.TransportSeekDistanceBytes!.Value);

}
