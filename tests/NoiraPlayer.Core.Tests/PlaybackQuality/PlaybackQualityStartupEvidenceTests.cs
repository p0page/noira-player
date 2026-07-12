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
            FfmpegStreamInfoDurationMs = 500
        };

        PlaybackQualityStartupEvidence.EnrichNativeOpenBreakdown(startup, metrics);

        var stage = Assert.Single(startup.Stages, value => value.Name == "native.open");
        Assert.Collection(
            stage.Components,
            component => Assert.Equal(("ffmpeg.open-input", 4000, "measured"), (component.Name, component.DurationMs, component.Status)),
            component => Assert.Equal(("ffmpeg.find-stream-info", 500, "measured"), (component.Name, component.DurationMs, component.Status)),
            component => Assert.Equal(("native.initialize-first-frame", 300), (component.Name, component.DurationMs)),
            component => Assert.Equal(("host.dispatch-overhead", 200), (component.Name, component.DurationMs)));
    }

}
