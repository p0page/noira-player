using System.Linq;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyModelTests
{
    [Fact]
    public void MediaSource_Exposes_Hdr_Audio_And_Subtitle_Metadata()
    {
        var source = new EmbyMediaSource
        {
            Id = "source-4k",
            Name = "4K HDR",
            Container = "mkv",
            Bitrate = 76_000_000,
            Width = 3840,
            Height = 2160,
            IsHdr = true,
            Streams =
            {
                new EmbyMediaStream
                {
                    Index = 0,
                    Kind = EmbyStreamKind.Video,
                    Codec = "hevc",
                    DisplayTitle = "4K HEVC Main10 HDR10"
                },
                new EmbyMediaStream
                {
                    Index = 1,
                    Kind = EmbyStreamKind.Audio,
                    Language = "jpn",
                    Codec = "truehd",
                    ChannelLayout = "7.1",
                    DisplayTitle = "Japanese TrueHD 7.1 Atmos"
                },
                new EmbyMediaStream
                {
                    Index = 2,
                    Kind = EmbyStreamKind.Subtitle,
                    Language = "chi",
                    Codec = "ass",
                    IsExternal = true,
                    DisplayTitle = "Chinese ASS"
                }
            }
        };

        Assert.Equal("4K HDR", source.Name);
        Assert.True(source.IsHdr);
        Assert.Equal("hevc", source.VideoStreams.Single().Codec);
        Assert.Equal("truehd", source.AudioStreams.Single().Codec);
        Assert.True(source.SubtitleStreams.Single().IsExternal);
    }
}
