using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualitySourceFingerprintTests
{
    [Fact]
    public void ComputeOpenedMediaSignature_Uses_Observed_Source_And_Tracks_Not_Locator()
    {
        var first = CreateObservedReport("item-a", "session-one", "eac3");
        var sameMedia = CreateObservedReport("item-b", "session-two", "eac3");
        var differentAudio = CreateObservedReport("item-c", "session-three", "truehd");

        Assert.Equal(
            PlaybackQualitySourceFingerprint.ComputeOpenedMediaSignature(first),
            PlaybackQualitySourceFingerprint.ComputeOpenedMediaSignature(sameMedia));
        Assert.NotEqual(
            PlaybackQualitySourceFingerprint.ComputeOpenedMediaSignature(first),
            PlaybackQualitySourceFingerprint.ComputeOpenedMediaSignature(differentAudio));
    }

    [Fact]
    public void ComputeOpenedSource_Ignores_Emby_PlaySessionId()
    {
        var first = PlaybackQualitySourceFingerprint.ComputeOpenedSource(
            "https://media.example/videos/42/stream.mkv?MediaSourceId=source-a&PlaySessionId=session-one&Static=true");
        var second = PlaybackQualitySourceFingerprint.ComputeOpenedSource(
            "https://media.example/videos/42/stream.mkv?MediaSourceId=source-a&PlaySessionId=session-two&Static=true");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeOpenedSource_Preserves_Stable_Media_Source_Identity()
    {
        var first = PlaybackQualitySourceFingerprint.ComputeOpenedSource(
            "https://media.example/videos/42/stream.mkv?MediaSourceId=source-a&PlaySessionId=session&Static=true");
        var second = PlaybackQualitySourceFingerprint.ComputeOpenedSource(
            "https://media.example/videos/42/stream.mkv?MediaSourceId=source-b&PlaySessionId=session&Static=true");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputeOpenedSource_Preserves_NonHttp_Locator_Identity()
    {
        var first = PlaybackQualitySourceFingerprint.ComputeOpenedSource("file:///C:/media/first.mkv");
        var second = PlaybackQualitySourceFingerprint.ComputeOpenedSource("file:///C:/media/second.mkv");

        Assert.NotEqual(first, second);
    }

    private static PlaybackQualityReport CreateObservedReport(
        string itemId,
        string session,
        string audioCodec)
    {
        var report = new PlaybackQualityReport
        {
            Source = new PlaybackQualitySource
            {
                ItemId = itemId,
                MediaSourceId = session,
                Container = "mkv",
                DurationTicks = 60_000_000,
                ContainerStartTimeTicks = 0,
                VideoStreamStartTimeTicks = 0,
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 23.976,
                HdrKind = "Hdr10",
                VideoRange = "HDR10",
                ColorPrimaries = "bt2020",
                ColorTransfer = "smpte2084",
                ColorSpace = "bt2020nc"
            }
        };
        report.Tracks.Video.Add(new PlaybackQualityTrack
        {
            Index = 0,
            Kind = "Video",
            Codec = "hevc",
            RealFrameRate = 23.976
        });
        report.Tracks.Audio.Add(new PlaybackQualityTrack
        {
            Index = 1,
            Kind = "Audio",
            Codec = audioCodec,
            Channels = 8,
            ChannelLayout = "7.1"
        });
        return report;
    }
}
