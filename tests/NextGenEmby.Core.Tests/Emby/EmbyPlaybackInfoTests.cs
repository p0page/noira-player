using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyPlaybackInfoTests
{
    [Fact]
    public async Task GetPlaybackInfoAsync_Parses_MediaVersions_Audio_And_Subtitles()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-4k",
                  "Name": "4K HDR",
                  "Container": "mkv",
                  "Bitrate": 76000000,
                  "Path": "/media/movie.mkv",
                  "MediaStreams": [
                    {
                      "Index": 0,
                      "Type": "Video",
                      "Codec": "hevc",
                      "Width": 3840,
                      "Height": 2160,
                      "VideoRange": "HDR10",
                      "DisplayTitle": "4K HEVC Main10 HDR10"
                    },
                    {
                      "Index": 1,
                      "Type": "Audio",
                      "Codec": "truehd",
                      "Language": "eng",
                      "ChannelLayout": "7.1",
                      "DisplayTitle": "English TrueHD 7.1 Atmos"
                    },
                    {
                      "Index": 2,
                      "Type": "Subtitle",
                      "Codec": "ass",
                      "Language": "chi",
                      "IsExternal": true,
                      "DisplayTitle": "Chinese ASS"
                    }
                  ]
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(Session(), "movie-1");

        var source = Assert.Single(sources);
        Assert.Equal("source-4k", source.Id);
        Assert.True(source.IsHdr);
        Assert.Equal(3840, source.Width);
        Assert.Equal(2160, source.Height);
        Assert.Equal("http://emby.local:8096/Videos/movie-1/stream?static=true&mediaSourceId=source-4k&api_key=token-123", source.DirectStreamUrl);
        Assert.Equal("hevc", source.VideoStreams.Single().Codec);
        Assert.Equal("truehd", source.AudioStreams.Single().Codec);
        Assert.True(source.SubtitleStreams.Single().IsExternal);

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/Items/movie-1/PlaybackInfo", request.RequestUri!.AbsolutePath);
        Assert.Equal("Emby", request.AuthorizationScheme);
        Assert.Equal(
            "UserId=\"user-1\", Client=\"Next Gen Xbox Emby\", Device=\"Next Gen Xbox Emby\", DeviceId=\"test-device\", Version=\"0.1.0\"",
            request.AuthorizationParameter);
        Assert.Equal("token-123", request.EmbyToken);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Escapes_Request_And_DirectStreamUrl_Components()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source 4k/atmos",
                  "Name": "Escaped Source",
                  "MediaStreams": []
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(
            Session(serverUrl: "http://emby.local:8096/", accessToken: "token+123/abc"),
            "movie 1/slash");

        var source = Assert.Single(sources);
        Assert.Equal("/Items/movie%201%2Fslash/PlaybackInfo", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(
            "http://emby.local:8096/Videos/movie%201%2Fslash/stream?static=true&mediaSourceId=source%204k%2Fatmos&api_key=token%2B123%2Fabc",
            source.DirectStreamUrl);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Maps_Null_MediaStreams_And_Missing_Optional_Fields()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-minimal",
                  "MediaStreams": null
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(Session(), "movie-1");

        var source = Assert.Single(sources);
        Assert.Equal("source-minimal", source.Id);
        Assert.Equal("source-minimal", source.Name);
        Assert.Empty(source.Streams);
        Assert.False(source.IsHdr);
    }

    private static EmbyApiClient CreateClient(HttpClient http) => new EmbyApiClient(http, new EmbyClientOptions
    {
        ServerUrl = "http://emby.local:8096",
        DeviceName = "Next Gen Xbox Emby",
        DeviceId = "test-device",
        ClientName = "Next Gen Xbox Emby",
        ClientVersion = "0.1.0"
    });

    private static EmbySession Session(
        string serverUrl = "http://emby.local:8096",
        string userId = "user-1",
        string accessToken = "token-123") => new EmbySession
    {
        ServerUrl = serverUrl,
        UserId = userId,
        UserName = "Alice",
        AccessToken = accessToken
    };
}
