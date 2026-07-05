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

    [Fact]
    public async Task GetLatestItemsAsync_Maps_Null_Image_Collections_To_Empty_Tags()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            [
              {
                "Id": "series-1",
                "Name": "Null Images",
                "Type": "Series",
                "ImageTags": null,
                "BackdropImageTags": null
              }
            ]
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetLatestItemsAsync(Session());

        var item = Assert.Single(items);
        Assert.Equal("", item.PrimaryImageTag);
        Assert.Equal("", item.BackdropImageTag);
    }

    [Fact]
    public async Task GetLatestItemsAsync_Escapes_User_Id_And_Builds_Expected_Query()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.GetLatestItemsAsync(Session(userId: "user 1/slash"));

        var request = handler.LastRequest!;
        Assert.Equal("/Users/user%201%2Fslash/Items/Latest", request.RequestUri!.AbsolutePath);
        Assert.Equal(
            "?IncludeItemTypes=Movie,Series,Episode&Fields=Overview,ProductionYear,RunTimeTicks,PrimaryImageAspectRatio&Limit=50",
            request.RequestUri.Query);
    }

    [Fact]
    public async Task GetUserViewsAsync_Parses_Movie_And_Tv_Libraries()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                { "Id": "movies", "Name": "Movies", "CollectionType": "movies" },
                { "Id": "tv", "Name": "TV Shows", "CollectionType": "tvshows" }
              ],
              "TotalRecordCount": 2
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var views = await client.GetUserViewsAsync(Session());

        Assert.Collection(
            views,
            view =>
            {
                Assert.Equal("movies", view.Id);
                Assert.Equal("Movies", view.Name);
                Assert.Equal("movies", view.CollectionType);
            },
            view =>
            {
                Assert.Equal("tv", view.Id);
                Assert.Equal("TV Shows", view.Name);
                Assert.Equal("tvshows", view.CollectionType);
            });
        Assert.Equal("/Users/user-1/Views", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetItemsAsync_Builds_Library_Query_And_Parses_UserData()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "movie-1",
                  "Name": "Movie One",
                  "Type": "Movie",
                  "ProductionYear": 2024,
                  "RunTimeTicks": 72000000000,
                  "UserData": {
                    "Played": false,
                    "PlaybackPositionTicks": 1230000000,
                    "PlayedPercentage": 17.5
                  }
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetItemsAsync(Session(), new EmbyItemsQuery
        {
            ParentId = "movies",
            IncludeItemTypes = "Movie",
            SortBy = "SortName",
            SortOrder = "Ascending",
            StartIndex = 20,
            Limit = 40,
            Recursive = true,
            Filters = "IsNotFolder"
        });

        var item = Assert.Single(items);
        Assert.Equal("movie-1", item.Id);
        Assert.False(item.UserData.Played);
        Assert.Equal(1230000000, item.UserData.PlaybackPositionTicks);
        Assert.Equal(17.5, item.UserData.PlayedPercentage);
        Assert.Equal("/Users/user-1/Items", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ParentId=movies", handler.LastRequest.RequestUri.Query);
        Assert.Contains("IncludeItemTypes=Movie", handler.LastRequest.RequestUri.Query);
        Assert.Contains("StartIndex=20", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=40", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetItemAsync_And_GetChildrenAsync_Parse_Detail_And_Episodes()
    {
        var calls = 0;
        var handler = new TestHttpMessageHandler(request =>
        {
            calls++;
            if (request.RequestUri!.AbsolutePath == "/Users/user-1/Items/series-1")
            {
                return TestHttpMessageHandler.Json(
                    HttpStatusCode.OK,
                    """
                    {
                      "Id": "series-1",
                      "Name": "Series One",
                      "Type": "Series",
                      "Overview": "A show.",
                      "ChildCount": 1
                    }
                    """);
            }

            return TestHttpMessageHandler.Json(
                HttpStatusCode.OK,
                """
                {
                  "Items": [
                    {
                      "Id": "episode-1",
                      "Name": "Pilot",
                      "Type": "Episode",
                      "IndexNumber": 1,
                      "ParentIndexNumber": 1
                    }
                  ],
                  "TotalRecordCount": 1
                }
                """);
        });
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var detail = await client.GetItemAsync(Session(), "series-1");
        var episodes = await client.GetChildrenAsync(Session(), "season-1", "Episode");

        Assert.Equal("Series One", detail.Name);
        Assert.Equal(1, detail.ChildCount);
        var episode = Assert.Single(episodes);
        Assert.Equal(1, episode.ParentIndexNumber);
        Assert.Equal(1, episode.IndexNumber);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void GetImageUrl_Handles_Trailing_Slash_And_Escapes_Components()
    {
        using var http = new HttpClient(new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var client = CreateClient(http);

        var url = client.GetImageUrl(
            Session(serverUrl: "http://emby.local:8096/", accessToken: "token+123/abc"),
            "movie 1",
            "Primary Image",
            600);

        Assert.Equal("http://emby.local:8096/Items/movie%201/Images/Primary%20Image?maxWidth=600&quality=90&api_key=token%2B123%2Fabc", url);
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
