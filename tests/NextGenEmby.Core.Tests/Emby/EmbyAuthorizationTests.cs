using System.Linq;
using System.Net.Http;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyAuthorizationTests
{
    [Fact]
    public void Apply_LoginRequest_Adds_ClientIdentity_Without_Token()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "Items");

        EmbyAuthorization.Apply(request, CreateOptions());

        Assert.Equal("Emby", request.Headers.Authorization!.Scheme);
        Assert.Equal(
            "Client=\"Next Gen Xbox Emby\", Device=\"Next Gen Xbox Emby\", DeviceId=\"test-device\", Version=\"0.1.0\"",
            request.Headers.Authorization.Parameter);
        Assert.False(request.Headers.Contains("X-Emby-Token"));
    }

    [Fact]
    public void Apply_AuthenticatedRequest_Adds_UserIdentity_And_Token()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "Items");
        var session = new EmbySession
        {
            ServerUrl = "http://emby.local:8096",
            UserId = "user-1",
            UserName = "Alice",
            AccessToken = "token-123"
        };

        EmbyAuthorization.Apply(request, CreateOptions(), session);

        Assert.Equal("Emby", request.Headers.Authorization!.Scheme);
        Assert.Equal(
            "UserId=\"user-1\", Client=\"Next Gen Xbox Emby\", Device=\"Next Gen Xbox Emby\", DeviceId=\"test-device\", Version=\"0.1.0\"",
            request.Headers.Authorization.Parameter);
        Assert.True(request.Headers.TryGetValues("X-Emby-Token", out var values));
        Assert.Equal("token-123", values.Single());
    }

    private static EmbyClientOptions CreateOptions()
    {
        return new EmbyClientOptions
        {
            ServerUrl = "http://emby.local:8096",
            DeviceName = "Next Gen Xbox Emby",
            DeviceId = "test-device",
            ClientName = "Next Gen Xbox Emby",
            ClientVersion = "0.1.0"
        };
    }
}
