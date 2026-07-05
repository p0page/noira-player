using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyProgressTests
{
    [Fact]
    public async Task ReportPlaybackStartAsync_Posts_PlaybackStart_To_Emby()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.NoContent, ""));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.ReportPlaybackStartAsync(Session(), new PlaybackSessionRequest
        {
            ItemId = "movie-1",
            MediaSourceId = "source-4k",
            PlaySessionId = "play-session-1",
            PositionTicks = 10_000_000,
            IsPaused = false,
            PlayMethod = PlaybackPlayMethod.DirectPlay,
            AudioStreamIndex = 1,
            SubtitleStreamIndex = 2
        });

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/Sessions/Playing", request.RequestUri!.AbsolutePath);
        Assert.Equal("application/json", request.ContentTypeMediaType);

        using var body = JsonDocument.Parse(request.Body!);
        var root = body.RootElement;
        Assert.Equal("movie-1", root.GetProperty("ItemId").GetString());
        Assert.Equal("source-4k", root.GetProperty("MediaSourceId").GetString());
        Assert.Equal("play-session-1", root.GetProperty("PlaySessionId").GetString());
        Assert.Equal(10_000_000, root.GetProperty("PositionTicks").GetInt64());
        Assert.False(root.GetProperty("IsPaused").GetBoolean());
        Assert.Equal("DirectPlay", root.GetProperty("PlayMethod").GetString());
        Assert.Equal(1, root.GetProperty("AudioStreamIndex").GetInt32());
        Assert.Equal(2, root.GetProperty("SubtitleStreamIndex").GetInt32());
        Assert.False(root.TryGetProperty("EventName", out _));
    }

    [Fact]
    public async Task ReportPlaybackStoppedAsync_Posts_PlaybackStop_To_Emby()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.NoContent, ""));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.ReportPlaybackStoppedAsync(Session(), new PlaybackSessionRequest
        {
            ItemId = "movie-1",
            MediaSourceId = "source-4k",
            PlaySessionId = "play-session-1",
            PositionTicks = 65_000_000,
            PlayMethod = PlaybackPlayMethod.DirectPlay,
            AudioStreamIndex = 1
        });

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/Sessions/Playing/Stopped", request.RequestUri!.AbsolutePath);

        using var body = JsonDocument.Parse(request.Body!);
        var root = body.RootElement;
        Assert.Equal("movie-1", root.GetProperty("ItemId").GetString());
        Assert.Equal("source-4k", root.GetProperty("MediaSourceId").GetString());
        Assert.Equal("play-session-1", root.GetProperty("PlaySessionId").GetString());
        Assert.Equal(65_000_000, root.GetProperty("PositionTicks").GetInt64());
        Assert.Equal("DirectPlay", root.GetProperty("PlayMethod").GetString());
        Assert.Equal(1, root.GetProperty("AudioStreamIndex").GetInt32());
        Assert.False(root.TryGetProperty("SubtitleStreamIndex", out _));
        Assert.False(root.TryGetProperty("EventName", out _));
    }

    [Fact]
    public async Task ReportProgressAsync_Posts_PlaybackProgress_To_Emby()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, ""));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.ReportProgressAsync(Session(), new PlaybackProgressRequest
        {
            ItemId = "movie-1",
            MediaSourceId = "source-4k",
            PlaySessionId = "play-session-1",
            PositionTicks = 12_000_000,
            IsPaused = false,
            EventName = PlaybackProgressEvent.TimeUpdate,
            PlayMethod = PlaybackPlayMethod.DirectPlay,
            AudioStreamIndex = 1,
            SubtitleStreamIndex = 2
        });

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/Sessions/Playing/Progress", request.RequestUri!.AbsolutePath);
        Assert.Equal("Emby", request.AuthorizationScheme);
        Assert.Equal(
            "UserId=\"user-1\", Client=\"Next Gen Xbox Emby\", Device=\"Next Gen Xbox Emby\", DeviceId=\"test-device\", Version=\"0.1.0\"",
            request.AuthorizationParameter);
        Assert.Equal("token-123", request.EmbyToken);
        Assert.Equal("application/json", request.ContentTypeMediaType);
        Assert.Equal("utf-8", request.ContentTypeCharSet);

        using var body = JsonDocument.Parse(request.Body!);
        var root = body.RootElement;
        Assert.Equal("movie-1", root.GetProperty("ItemId").GetString());
        Assert.Equal("source-4k", root.GetProperty("MediaSourceId").GetString());
        Assert.Equal("play-session-1", root.GetProperty("PlaySessionId").GetString());
        Assert.Equal(12_000_000, root.GetProperty("PositionTicks").GetInt64());
        Assert.False(root.GetProperty("IsPaused").GetBoolean());
        Assert.Equal("TimeUpdate", root.GetProperty("EventName").GetString());
        Assert.Equal("DirectPlay", root.GetProperty("PlayMethod").GetString());
        Assert.Equal(1, root.GetProperty("AudioStreamIndex").GetInt32());
        Assert.Equal(2, root.GetProperty("SubtitleStreamIndex").GetInt32());
    }

    [Fact]
    public async Task ReportProgressAsync_Omits_Null_Stream_Indexes()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.NoContent, ""));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.ReportProgressAsync(Session(), new PlaybackProgressRequest
        {
            ItemId = "movie-1",
            MediaSourceId = "source-4k",
            PositionTicks = 1,
            IsPaused = true,
            EventName = PlaybackProgressEvent.Pause
        });

        using var body = JsonDocument.Parse(handler.LastRequest!.Body!);
        var root = body.RootElement;
        Assert.False(root.TryGetProperty("PlaySessionId", out _));
        Assert.False(root.TryGetProperty("AudioStreamIndex", out _));
        Assert.False(root.TryGetProperty("SubtitleStreamIndex", out _));
        Assert.Equal("Pause", root.GetProperty("EventName").GetString());
    }

    [Fact]
    public async Task ReportProgressAsync_Requires_ItemId_And_MediaSourceId()
    {
        var handler = new TestHttpMessageHandler(_ => throw new InvalidOperationException("Request should not be sent."));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await Assert.ThrowsAsync<ArgumentException>(() => client.ReportProgressAsync(Session(), new PlaybackProgressRequest
        {
            ItemId = null!,
            MediaSourceId = "source-4k"
        }));

        await Assert.ThrowsAsync<ArgumentException>(() => client.ReportProgressAsync(Session(), new PlaybackProgressRequest
        {
            ItemId = "movie-1",
            MediaSourceId = " "
        }));
    }

    [Fact]
    public async Task ReportProgressAsync_Rejects_Unsupported_Enum_Values()
    {
        var handler = new TestHttpMessageHandler(_ => throw new InvalidOperationException("Request should not be sent."));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReportProgressAsync(Session(), new PlaybackProgressRequest
        {
            ItemId = "movie-1",
            MediaSourceId = "source-4k",
            EventName = (PlaybackProgressEvent)999
        }));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ReportProgressAsync(Session(), new PlaybackProgressRequest
        {
            ItemId = "movie-1",
            MediaSourceId = "source-4k",
            PlayMethod = (PlaybackPlayMethod)999
        }));
    }

    private static EmbyApiClient CreateClient(HttpClient http) => new EmbyApiClient(http, new EmbyClientOptions
    {
        ServerUrl = "http://emby.local:8096",
        DeviceName = "Next Gen Xbox Emby",
        DeviceId = "test-device",
        ClientName = "Next Gen Xbox Emby",
        ClientVersion = "0.1.0"
    });

    private static EmbySession Session() => new EmbySession
    {
        ServerUrl = "http://emby.local:8096",
        UserId = "user-1",
        UserName = "Alice",
        AccessToken = "token-123"
    };
}
