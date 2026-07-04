using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyLibraryTests
{
    [Fact]
    public async Task GetLatestItemsAsync_Parses_Movies_And_TV_Items()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            [
              {
                "Id": "movie-1",
                "Name": "Blade Runner",
                "Type": "Movie",
                "Overview": "Replicants and rain.",
                "ProductionYear": 1982,
                "RunTimeTicks": 70200000000,
                "ImageTags": { "Primary": "primary-tag" },
                "BackdropImageTags": [ "backdrop-tag" ]
              }
            ]
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetLatestItemsAsync(Session());

        var item = Assert.Single(items);
        Assert.Equal("movie-1", item.Id);
        Assert.Equal("Movie", item.Type);
        Assert.Equal("primary-tag", item.PrimaryImageTag);
        Assert.Equal("backdrop-tag", item.BackdropImageTag);

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("/Users/user-1/Items/Latest", request.RequestUri!.AbsolutePath);
        Assert.Equal("Emby", request.AuthorizationScheme);
        Assert.Equal(
            "UserId=\"user-1\", Client=\"Next Gen Xbox Emby\", Device=\"Next Gen Xbox Emby\", DeviceId=\"test-device\", Version=\"0.1.0\"",
            request.AuthorizationParameter);
        Assert.Equal("token-123", request.EmbyToken);
    }

    [Fact]
    public void GetImageUrl_Builds_Primary_Image_Url()
    {
        using var http = new HttpClient(new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var client = CreateClient(http);

        var url = client.GetImageUrl(Session(), "movie-1", "Primary", 600);

        Assert.Equal("http://emby.local:8096/Items/movie-1/Images/Primary?maxWidth=600&quality=90&api_key=token-123", url);
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
