using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyAuthenticationTests
{
    [Fact]
    public async Task AuthenticateAsync_Posts_Credentials_And_Returns_Session()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "AccessToken": "token-123",
              "User": {
                "Id": "user-1",
                "Name": "Alice"
              }
            }
            """));
        using var http = new HttpClient(handler);
        var client = new EmbyApiClient(http, new EmbyClientOptions
        {
            ServerUrl = "http://emby.local:8096",
            DeviceName = "Next Gen Xbox Emby",
            DeviceId = "test-device",
            ClientName = "Next Gen Xbox Emby",
            ClientVersion = "0.1.0"
        });

        var session = await client.AuthenticateAsync("alice", "secret");

        Assert.Equal("http://emby.local:8096", session.ServerUrl);
        Assert.Equal("user-1", session.UserId);
        Assert.Equal("Alice", session.UserName);
        Assert.Equal("token-123", session.AccessToken);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/Users/AuthenticateByName", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("Emby", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal(
            "Client=\"Next Gen Xbox Emby\", Device=\"Next Gen Xbox Emby\", DeviceId=\"test-device\", Version=\"0.1.0\"",
            handler.LastRequest.Headers.Authorization.Parameter);
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", handler.LastRequest.Content.Headers.ContentType.CharSet);

        using var document = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("alice", document.RootElement.GetProperty("Username").GetString());
        Assert.Equal("secret", document.RootElement.GetProperty("Pw").GetString());
    }
}
