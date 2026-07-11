using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualitySourceFingerprintTests
{
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
}
