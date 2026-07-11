using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NoiraPlayer.Core.Emby;
using Xunit;

namespace NoiraPlayer.Core.Tests.Emby;

public sealed class EmbyAuthenticationTests
{
    [Fact]
    public void CreateHeaderValue_Uses_The_Same_Authenticated_Identity_The_Web_Client_Needs()
    {
        var options = new EmbyClientOptions
        {
            ClientName = "Noira",
            ClientVersion = "0.1.0",
            DeviceName = "Xbox",
            DeviceId = "device-42"
        };
        var session = new EmbySession
        {
            UserId = "user-7",
            AccessToken = "token-123"
        };

        var value = EmbyAuthorization.CreateHeaderValue(options, session);

        Assert.Equal(
            "Emby UserId=\"user-7\", Client=\"Noira\", Device=\"Xbox\", DeviceId=\"device-42\", Version=\"0.1.0\"",
            value);
    }

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
            DeviceName = "Noira",
            DeviceId = "test-device",
            ClientName = "Noira",
            ClientVersion = "0.1.0"
        });

        var session = await client.AuthenticateAsync("alice", "secret");

        Assert.Equal("http://emby.local:8096", session.ServerUrl);
        Assert.Equal("user-1", session.UserId);
        Assert.Equal("Alice", session.UserName);
        Assert.Equal("token-123", session.AccessToken);
        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/Users/AuthenticateByName", request.RequestUri!.AbsolutePath);
        Assert.Equal("Emby", request.AuthorizationScheme);
        Assert.Equal(
            "Client=\"Noira\", Device=\"Noira\", DeviceId=\"test-device\", Version=\"0.1.0\"",
            request.AuthorizationParameter);
        Assert.Null(request.EmbyToken);
        Assert.Equal("application/json", request.ContentTypeMediaType);
        Assert.Equal("utf-8", request.ContentTypeCharSet);

        using var document = JsonDocument.Parse(request.Body!);
        Assert.Equal("alice", document.RootElement.GetProperty("Username").GetString());
        Assert.Equal("secret", document.RootElement.GetProperty("Pw").GetString());
    }
}
