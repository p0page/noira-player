using NoiraPlayer.Core.Playback;
using Xunit;

namespace NoiraPlayer.Core.Tests.Playback;

public sealed class ManualDirectStreamLaunchOptionsTests
{
    [Fact]
    public void Constructor_Trims_Stream_Url_And_Preserves_AutoStart()
    {
        var options = new ManualDirectStreamLaunchOptions(
            " https://media.example.test/sample.mp4 ",
            autoStart: true);

        Assert.Equal("https://media.example.test/sample.mp4", options.StreamUrl);
        Assert.True(options.AutoStart);
    }

    [Fact]
    public void Constructor_Disables_AutoStart_When_Stream_Url_Is_Missing()
    {
        var options = new ManualDirectStreamLaunchOptions(" ", autoStart: true);

        Assert.Equal("", options.StreamUrl);
        Assert.False(options.AutoStart);
    }
}
