using System.Linq;
using NextGenEmby.Core.Diagnostics;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentPlaybackOptionsFixtureTests
{
    [Fact]
    public void Create_Provides_Multi_Source_Audio_And_Subtitle_Options()
    {
        var fixture = DevelopmentPlaybackOptionsFixture.Create();

        Assert.Equal("Aurora Protocol", fixture.ItemName);
        Assert.Equal("fixture-source-4k", fixture.DefaultMediaSourceId);
        Assert.Equal(2, fixture.MediaSources.Count);
        Assert.All(fixture.MediaSources, source => Assert.NotEmpty(source.DirectStreamUrl));
        Assert.Contains(fixture.MediaSources, source => source.AudioStreams.Count() >= 2);
        Assert.Contains(fixture.MediaSources, source => source.SubtitleStreams.Count() >= 2);
    }

    [Fact]
    public void Create_Default_Stream_Indexes_Exist_On_Default_Source()
    {
        var fixture = DevelopmentPlaybackOptionsFixture.Create();
        var source = fixture.MediaSources.Single(candidate => candidate.Id == fixture.DefaultMediaSourceId);

        Assert.Contains(source.AudioStreams, stream => stream.Index == fixture.DefaultAudioStreamIndex);
        Assert.Contains(source.SubtitleStreams, stream => stream.Index == fixture.DefaultSubtitleStreamIndex);
    }
}
