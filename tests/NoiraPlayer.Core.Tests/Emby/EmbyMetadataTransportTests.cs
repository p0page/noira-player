using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NoiraPlayer.Core.Emby;
using Xunit;

namespace NoiraPlayer.Core.Tests.Emby;

public sealed class EmbyMetadataTransportTests
{
    [Fact]
    public async Task GetAsync_Reuses_Injected_Client_And_Applies_Current_Session_Per_Request()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            "{\"Items\":[]}"));
        using var http = new HttpClient(handler);
        using var transport = new EmbyMetadataTransport(http, disposeHttpClient: false);

        var first = await transport.GetAsync(
            new Uri("https://emby.example/Users/user-1/Views"),
            Options(),
            Session("token-1"));

        Assert.Equal(1, handler.RequestCount);
        Assert.False(handler.IsDisposed);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("https://emby.example/Users/user-1/Views", handler.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Equal("application/json", handler.LastRequest?.AcceptMediaType);
        Assert.Equal("Emby", handler.LastRequest?.AuthorizationScheme);
        Assert.Equal("token-1", handler.LastRequest?.EmbyToken);
        Assert.Equal(200, first.StatusCode);
        Assert.Equal("{\"Items\":[]}", first.Body);

        var second = await transport.GetAsync(
            new Uri("https://emby.example/Users/user-1/Items?Limit=50"),
            Options(),
            Session("token-2"));

        Assert.Equal(2, handler.RequestCount);
        Assert.False(handler.IsDisposed);
        Assert.Equal("token-2", handler.LastRequest?.EmbyToken);
        Assert.Equal(200, second.StatusCode);
    }

    [Fact]
    public async Task GetAsync_Returns_Http_Error_Body_And_Sanitized_Diagnostics()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.Unauthorized,
            "{\"error\":\"unauthorized\"}"));
        using var http = new HttpClient(handler);
        using var transport = new EmbyMetadataTransport(http, disposeHttpClient: false);

        var response = await transport.GetAsync(
            new Uri("https://emby.example/Users/user-1/Views"),
            Options(),
            Session("private-token"));

        Assert.Equal(401, response.StatusCode);
        Assert.Equal("Unauthorized", response.ReasonPhrase);
        Assert.Equal("{\"error\":\"unauthorized\"}", response.Body);
        Assert.True(response.NetworkDurationMilliseconds >= 0);
        Assert.True(response.BodyLengthBytes > 0);
        Assert.DoesNotContain("emby.example", response.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-token", response.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_Rejects_A_Request_Outside_The_Saved_Server()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        using var http = new HttpClient(handler);
        using var transport = new EmbyMetadataTransport(http, disposeHttpClient: false);

        await Assert.ThrowsAsync<ArgumentException>(() => transport.GetAsync(
            new Uri("https://other.example/Users/user-1/Views"),
            Options(),
            Session("private-token")));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public void Dispose_Disposes_Only_An_Owned_HttpClient()
    {
        var externalHandler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        using (var externalClient = new HttpClient(externalHandler))
        {
            var transport = new EmbyMetadataTransport(externalClient, disposeHttpClient: false);
            transport.Dispose();
            Assert.False(externalHandler.IsDisposed);
        }

        var ownedHandler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        var ownedClient = new HttpClient(ownedHandler);
        var ownedTransport = new EmbyMetadataTransport(ownedClient, disposeHttpClient: true);

        ownedTransport.Dispose();

        Assert.True(ownedHandler.IsDisposed);
    }

    private static EmbyClientOptions Options() => new()
    {
        ServerUrl = "https://emby.example",
        ClientName = "Noira",
        DeviceName = "Xbox",
        DeviceId = "device-1",
        ClientVersion = "0.1.0"
    };

    private static EmbySession Session(string accessToken) => new()
    {
        ServerUrl = "https://emby.example",
        UserId = "user-1",
        UserName = "User",
        AccessToken = accessToken
    };
}
