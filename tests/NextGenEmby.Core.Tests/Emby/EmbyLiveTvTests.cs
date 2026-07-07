using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyLiveTvTests
{
    [Fact]
    public async Task GetLiveTvInfoAsync_Parses_Enabled_State()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "IsEnabled": true,
              "EnabledUsers": [ "user-1", "user-2" ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var info = await client.GetLiveTvInfoAsync(Session());

        Assert.True(info.IsEnabled);
        Assert.Contains("user-1", info.EnabledUserIds);
        Assert.Equal("/LiveTv/Info", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("token-123", handler.LastRequest.EmbyToken);
    }

    [Fact]
    public async Task GetLiveTvChannelsAsync_Requests_Current_Program_And_Maps_Channel_Artwork()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "channel-1",
                  "Name": "Channel One",
                  "Number": "101",
                  "ChannelType": "TV",
                  "ImageTags": { "Primary": "channel-primary", "Thumb": "channel-thumb" },
                  "CurrentProgram": {
                    "Id": "program-1",
                    "Name": "Evening News",
                    "EpisodeTitle": "Local Edition",
                    "Overview": "Headlines and weather.",
                    "ImageTags": { "Primary": "program-primary", "Thumb": "program-thumb", "Banner": "program-banner" },
                    "BackdropImageTags": [ "program-backdrop" ],
                    "OfficialRating": "TV-PG",
                    "RunTimeTicks": 18000000000,
                    "StartDate": "2026-07-06T08:00:00.0000000Z",
                    "EndDate": "2026-07-06T08:30:00.0000000Z",
                    "IsNews": true,
                    "ChannelId": "channel-1"
                  }
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var channels = await client.GetLiveTvChannelsAsync(Session(), limit: 24);

        var channel = Assert.Single(channels);
        Assert.Equal("channel-1", channel.Id);
        Assert.Equal("Channel One", channel.Name);
        Assert.Equal("101", channel.Number);
        Assert.Equal("TV", channel.ChannelType);
        Assert.Equal("channel-primary", channel.PrimaryImageTag);
        Assert.Equal("channel-thumb", channel.ThumbImageTag);
        Assert.NotNull(channel.CurrentProgram);
        Assert.Equal("Evening News", channel.CurrentProgram!.Name);
        Assert.Equal("Local Edition", channel.CurrentProgram.EpisodeTitle);
        Assert.Equal("program-primary", channel.CurrentProgram.PrimaryImageTag);
        Assert.Equal("program-thumb", channel.CurrentProgram.ThumbImageTag);
        Assert.Equal("program-backdrop", channel.CurrentProgram.BackdropImageTag);
        Assert.Equal("program-banner", channel.CurrentProgram.BannerImageTag);
        Assert.True(channel.CurrentProgram.IsNews);
        Assert.Equal(new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero), channel.CurrentProgram.StartDate);
        Assert.Equal("/LiveTv/Channels", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("UserId=user-1", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Fields=CurrentProgram%2COverview%2CPrimaryImageAspectRatio%2CImageTags%2CBackdropImageTags", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=24", handler.LastRequest.RequestUri.Query);
        Assert.Contains("EnableImages=true", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetLiveTvProgramsAsync_Requests_Channel_Filter_And_Maps_Program_Flags()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "program-2",
                  "Name": "Movie Night",
                  "EpisodeTitle": "",
                  "Overview": "A feature presentation.",
                  "OfficialRating": "PG",
                  "RunTimeTicks": 72000000000,
                  "StartDate": "2026-07-06T09:00:00.0000000Z",
                  "EndDate": "2026-07-06T11:00:00.0000000Z",
                  "IsMovie": true,
                  "IsSports": false,
                  "IsNews": false,
                  "IsKids": false,
                  "IsSeries": false,
                  "ChannelId": "channel-1"
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var programs = await client.GetLiveTvProgramsAsync(Session(), "channel 1/slash", limit: 12);

        var program = Assert.Single(programs);
        Assert.Equal("program-2", program.Id);
        Assert.Equal("Movie Night", program.Name);
        Assert.True(program.IsMovie);
        Assert.False(program.IsSeries);
        Assert.Equal("channel-1", program.ChannelId);
        Assert.Equal("/LiveTv/Programs", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("UserId=user-1", handler.LastRequest.RequestUri.Query);
        Assert.Contains("ChannelIds=channel%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=12", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Fields=Overview", handler.LastRequest.RequestUri.Query);
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
