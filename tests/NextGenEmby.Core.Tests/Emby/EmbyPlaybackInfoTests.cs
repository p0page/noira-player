using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
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
                  "RunTimeTicks": 70200000000,
                  "Path": "/media/movie.mkv",
                  "MediaStreams": [
                    {
                      "Index": 0,
                      "Type": "Video",
                      "Codec": "hevc",
                      "Width": 3840,
                      "Height": 2160,
                      "RealFrameRate": 23.976,
                      "AverageFrameRate": 24.0,
                      "VideoRange": "HDR10",
                      "ColorPrimaries": "bt2020",
                      "ColorTransfer": "smpte2084",
                      "ColorSpace": "bt2020nc",
                      "DisplayTitle": "4K HEVC Main10 HDR10"
                    },
                    {
                      "Index": 1,
                      "Type": "Audio",
                      "Codec": "truehd",
                      "Language": "eng",
                      "ChannelLayout": "7.1",
                      "Channels": 8,
                      "IsDefault": true,
                      "IsForced": false,
                      "DisplayTitle": "English TrueHD 7.1 Atmos"
                    },
                    {
                      "Index": 2,
                      "Type": "Subtitle",
                      "Codec": "ass",
                      "Language": "chi",
                      "IsExternal": true,
                      "IsDefault": false,
                      "IsForced": true,
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
        Assert.Equal(HdrPlaybackKind.Hdr10, source.HdrProfile.Kind);
        Assert.Equal("HDR10", source.HdrProfile.PlaybackStrategy);
        Assert.Equal(70_200_000_000, source.RunTimeTicks);
        Assert.Equal(3840, source.Width);
        Assert.Equal(2160, source.Height);
        Assert.Equal(23.976, source.VideoFrameRate);
        Assert.Equal("http://emby.local:8096/Videos/movie-1/stream?static=true&mediaSourceId=source-4k&api_key=token-123&container=mkv", source.DirectStreamUrl);
        var video = source.VideoStreams.Single();
        Assert.Equal("hevc", video.Codec);
        Assert.Equal("HDR10", video.VideoRange);
        Assert.Equal("bt2020", video.ColorPrimaries);
        Assert.Equal("smpte2084", video.ColorTransfer);
        Assert.Equal("bt2020nc", video.ColorSpace);
        Assert.Equal(23.976, video.RealFrameRate);
        Assert.Equal(24.0, video.AverageFrameRate);
        var audio = source.AudioStreams.Single();
        Assert.Equal("truehd", audio.Codec);
        Assert.Equal(8, audio.Channels);
        Assert.True(audio.IsDefault);
        Assert.False(audio.IsForced);
        var subtitle = source.SubtitleStreams.Single();
        Assert.True(subtitle.IsExternal);
        Assert.False(subtitle.IsDefault);
        Assert.True(subtitle.IsForced);

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/Items/movie-1/PlaybackInfo", request.RequestUri!.AbsolutePath);
        Assert.Equal("?UserId=user-1", request.RequestUri.Query);
        Assert.Equal("Emby", request.AuthorizationScheme);
        Assert.Equal(
            "UserId=\"user-1\", Client=\"Next Gen Xbox Emby\", Device=\"Next Gen Xbox Emby\", DeviceId=\"test-device\", Version=\"0.1.0\"",
            request.AuthorizationParameter);
        Assert.Equal("token-123", request.EmbyToken);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Parses_Chapters_When_Server_Returns_Them()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-with-chapters",
                  "MediaStreams": [],
                  "Chapters": [
                    {
                      "Name": "Opening",
                      "StartPositionTicks": 0,
                      "ImageTag": "chapter-image-0"
                    },
                    {
                      "Name": "Act 1",
                      "StartPositionTicks": 900000000
                    }
                  ]
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var source = Assert.Single(await client.GetPlaybackInfoAsync(Session(), "movie-1"));

        Assert.True(source.HasChapterMetadata);
        Assert.Equal(2, source.Chapters.Count);
        Assert.Equal("Opening", source.Chapters[0].Name);
        Assert.Equal(0, source.Chapters[0].StartPositionTicks);
        Assert.Equal("chapter-image-0", source.Chapters[0].ImageTag);
        Assert.Equal("Act 1", source.Chapters[1].Name);
        Assert.Equal(900_000_000, source.Chapters[1].StartPositionTicks);
        Assert.Equal("", source.Chapters[1].ImageTag);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Distinguishes_Missing_And_Explicit_Empty_Chapters()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-missing-chapters",
                  "MediaStreams": []
                },
                {
                  "Id": "source-empty-chapters",
                  "MediaStreams": [],
                  "Chapters": []
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(Session(), "movie-1");

        var missing = sources.Single(source => source.Id == "source-missing-chapters");
        Assert.False(missing.HasChapterMetadata);
        Assert.Empty(missing.Chapters);

        var empty = sources.Single(source => source.Id == "source-empty-chapters");
        Assert.True(empty.HasChapterMetadata);
        Assert.Empty(empty.Chapters);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Treats_Pq_Bt2020_Video_As_Hdr_Even_When_VideoRange_Is_Sdr()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-pq",
                  "Name": "4K / 28 Mbps",
                  "MediaStreams": [
                    {
                      "Index": 0,
                      "Type": "Video",
                      "Codec": "hevc",
                      "Width": 3840,
                      "Height": 1608,
                      "VideoRange": "SDR",
                      "ColorPrimaries": "bt2020",
                      "ColorTransfer": "smpte2084",
                      "ColorSpace": "bt2020nc"
                    }
                  ]
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var source = Assert.Single(await client.GetPlaybackInfoAsync(Session(), "movie-1"));

        Assert.True(source.IsHdr);
        Assert.Equal(HdrPlaybackKind.Hdr10, source.HdrProfile.Kind);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Maps_DolbyVision_Profile_8_1_To_Hdr10_Fallback()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-dv81",
                  "Name": "4K DoVi HDR10",
                  "MediaStreams": [
                    {
                      "Index": 0,
                      "Type": "Video",
                      "Codec": "hevc",
                      "Width": 3840,
                      "Height": 2160,
                      "VideoRange": "HDR10 Dolby Vision",
                      "ColorPrimaries": "bt2020",
                      "ColorTransfer": "smpte2084",
                      "ColorSpace": "bt2020nc",
                      "DisplayTitle": "4K HEVC DoVi Profile 8.1 HDR10"
                    }
                  ]
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var source = Assert.Single(await client.GetPlaybackInfoAsync(Session(), "movie-1"));

        Assert.True(source.IsHdr);
        Assert.Equal(HdrPlaybackKind.DolbyVisionWithHdr10Fallback, source.HdrProfile.Kind);
        Assert.True(source.HdrProfile.IsDolbyVision);
        Assert.True(source.HdrProfile.HasHdr10BaseLayer);
        Assert.Null(source.HdrProfile.DolbyVisionProfile);
        Assert.Null(source.HdrProfile.DolbyVisionCompatibilityId);
        Assert.True(source.HdrProfile.IsDirectPlayable);
        Assert.Equal("HDR10 fallback from Dolby Vision", source.HdrProfile.PlaybackStrategy);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Maps_DolbyVision_Profile_5_To_Unsupported()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-dv5",
                  "Name": "4K DoVi Profile 5",
                  "MediaStreams": [
                    {
                      "Index": 0,
                      "Type": "Video",
                      "Codec": "dvhe.05",
                      "Width": 3840,
                      "Height": 2160,
                      "VideoRange": "Dolby Vision",
                      "DisplayTitle": "DoVi Profile 5"
                    }
                  ]
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var source = Assert.Single(await client.GetPlaybackInfoAsync(Session(), "movie-1"));

        Assert.True(source.IsHdr);
        Assert.Equal(HdrPlaybackKind.DolbyVisionUnsupported, source.HdrProfile.Kind);
        Assert.True(source.HdrProfile.IsDolbyVision);
        Assert.False(source.HdrProfile.IsDirectPlayable);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Does_Not_Trust_MediaSource_Name_For_DolbyVision_When_Stream_Metadata_Is_Minimal()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-dv-name-only",
                  "Name": "4K / 12 Mbps, HEVC - DV • DDP5.1",
                  "MediaStreams": [
                    {
                      "Index": 0,
                      "Type": "Video",
                      "Codec": "hevc",
                      "Width": 3840,
                      "Height": 1608,
                      "VideoRange": "PC",
                      "DisplayTitle": "4K HEVC"
                    }
                  ]
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var source = Assert.Single(await client.GetPlaybackInfoAsync(Session(), "movie-1"));

        Assert.Equal(HdrPlaybackKind.Sdr, source.HdrProfile.Kind);
        Assert.False(source.HdrProfile.IsDolbyVision);
        Assert.True(source.HdrProfile.IsDirectPlayable);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Does_Not_Trust_Stream_DisplayTitle_For_DolbyVision_When_Stream_Metadata_Is_Minimal()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-dv-title-only",
                  "Name": "4K / 12 Mbps",
                  "MediaStreams": [
                    {
                      "Index": 0,
                      "Type": "Video",
                      "Codec": "hevc",
                      "Width": 3840,
                      "Height": 1608,
                      "VideoRange": "PC",
                      "DisplayTitle": "4K HEVC DoVi Profile 5 HDR10"
                    }
                  ]
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var source = Assert.Single(await client.GetPlaybackInfoAsync(Session(), "movie-1"));

        Assert.Equal(HdrPlaybackKind.Sdr, source.HdrProfile.Kind);
        Assert.False(source.HdrProfile.IsDolbyVision);
        Assert.True(source.HdrProfile.IsDirectPlayable);
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
            Session(serverUrl: "http://emby.local:8096/", userId: "user 1/slash", accessToken: "token+123/abc"),
            "movie 1/slash");

        var source = Assert.Single(sources);
        Assert.Equal("/Items/movie%201%2Fslash/PlaybackInfo", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("?UserId=user%201%2Fslash", handler.LastRequest.RequestUri.Query);
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
        Assert.False(source.HasChapterMetadata);
        Assert.Empty(source.Chapters);
        Assert.False(source.IsHdr);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Skips_Unsupported_Stream_Types()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-1",
                  "MediaStreams": [
                    { "Index": 0, "Type": "Subtitle", "Codec": "srt" },
                    { "Index": 1, "Type": "Data", "Codec": "bin" },
                    { "Index": 2, "Type": "Attachment", "Codec": "ttf" },
                    { "Index": 3, "Type": "EmbeddedImage", "Codec": "mjpeg" },
                    { "Index": 4, "Type": "Unknown", "Codec": "mystery" },
                    { "Index": 5, "Type": "", "Codec": "empty" },
                    { "Index": 6, "Codec": "missing" }
                  ]
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(Session(), "movie-1");

        var source = Assert.Single(sources);
        var subtitle = Assert.Single(source.SubtitleStreams);
        Assert.Equal(0, subtitle.Index);
        Assert.Single(source.Streams);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Resolves_Relative_DirectStreamUrl_And_Appends_Api_Key_When_Requested()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-1",
                  "DirectStreamUrl": "/emby/videos/custom-stream?existing=1",
                  "AddApiKeyToDirectStreamUrl": true,
                  "MediaStreams": []
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(
            Session(serverUrl: "http://emby.local:8096/", accessToken: "token+123/abc"),
            "movie-1");

        var source = Assert.Single(sources);
        Assert.Equal(
            "http://emby.local:8096/emby/videos/custom-stream?existing=1&api_key=token%2B123%2Fabc",
            source.DirectStreamUrl);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Preserves_Absolute_DirectStreamUrl_And_Appends_Api_Key_When_Requested()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-1",
                  "DirectStreamUrl": "https://cdn.emby.example/streams/movie-1?existing=1",
                  "AddApiKeyToDirectStreamUrl": true,
                  "MediaStreams": []
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(
            Session(accessToken: "token+123/abc"),
            "movie-1");

        var source = Assert.Single(sources);
        Assert.Equal(
            "https://cdn.emby.example/streams/movie-1?existing=1&api_key=token%2B123%2Fabc",
            source.DirectStreamUrl);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Preserves_Encoded_DirectStreamUrl_When_Appending_Api_Key()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-1",
                  "DirectStreamUrl": "https://cdn.emby.example/streams/movie%2Fpart.mkv?signature=a%2Fb%2Bc&name=space%20value",
                  "AddApiKeyToDirectStreamUrl": true,
                  "MediaStreams": []
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(
            Session(accessToken: "token+123/abc"),
            "movie-1");

        var source = Assert.Single(sources);
        Assert.Equal(
            "https://cdn.emby.example/streams/movie%2Fpart.mkv?signature=a%2Fb%2Bc&name=space%20value&api_key=token%2B123%2Fabc",
            source.DirectStreamUrl);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Preserves_Encoded_Relative_DirectStreamUrl_When_Appending_Api_Key()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-1",
                  "DirectStreamUrl": "/emby/videos/movie%2Fpart.mkv?signature=a%2Fb%2Bc&name=space%20value",
                  "AddApiKeyToDirectStreamUrl": true,
                  "MediaStreams": []
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(
            Session(accessToken: "token+123/abc"),
            "movie-1");

        var source = Assert.Single(sources);
        Assert.Equal(
            "http://emby.local:8096/emby/videos/movie%2Fpart.mkv?signature=a%2Fb%2Bc&name=space%20value&api_key=token%2B123%2Fabc",
            source.DirectStreamUrl);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Does_Not_Append_Api_Key_When_DirectStreamUrl_Does_Not_Request_It()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-1",
                  "DirectStreamUrl": "/emby/videos/no-key?existing=1",
                  "AddApiKeyToDirectStreamUrl": false,
                  "MediaStreams": []
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(Session(), "movie-1");

        var source = Assert.Single(sources);
        Assert.Equal("http://emby.local:8096/emby/videos/no-key?existing=1", source.DirectStreamUrl);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Does_Not_Append_Duplicate_Api_Key_When_DirectStreamUrl_Already_Has_One()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": [
                {
                  "Id": "source-1",
                  "DirectStreamUrl": "/emby/videos/existing-key?api_key=server-key&existing=1",
                  "AddApiKeyToDirectStreamUrl": true,
                  "MediaStreams": []
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(Session(), "movie-1");

        var source = Assert.Single(sources);
        Assert.Equal("http://emby.local:8096/emby/videos/existing-key?api_key=server-key&existing=1", source.DirectStreamUrl);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Adds_Top_Level_PlaySessionId_To_Fallback_DirectStreamUrl()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "PlaySessionId": "play session/1",
              "MediaSources": [
                {
                  "Id": "source-1",
                  "MediaStreams": []
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(Session(), "movie-1");

        var source = Assert.Single(sources);
        Assert.Equal("play session/1", source.PlaySessionId);
        Assert.Equal(
            "http://emby.local:8096/Videos/movie-1/stream?static=true&mediaSourceId=source-1&api_key=token-123&PlaySessionId=play%20session%2F1",
            source.DirectStreamUrl);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Prefers_MediaSource_PlaySessionId_Over_Top_Level_For_Fallback_DirectStreamUrl()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "PlaySessionId": "top-level",
              "MediaSources": [
                {
                  "Id": "source-1",
                  "PlaySessionId": "source session/1",
                  "MediaStreams": []
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(Session(), "movie-1");

        var source = Assert.Single(sources);
        Assert.Equal("source session/1", source.PlaySessionId);
        Assert.Equal(
            "http://emby.local:8096/Videos/movie-1/stream?static=true&mediaSourceId=source-1&api_key=token-123&PlaySessionId=source%20session%2F1",
            source.DirectStreamUrl);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_Null_Top_Level_MediaSources_Returns_Empty_List()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "MediaSources": null
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sources = await client.GetPlaybackInfoAsync(Session(), "movie-1");

        Assert.Empty(sources);
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
