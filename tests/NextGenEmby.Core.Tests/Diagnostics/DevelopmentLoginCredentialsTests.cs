using NextGenEmby.Core.Diagnostics;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentLoginCredentialsTests
{
    [Fact]
    public void TryParseJson_Accepts_Complete_Config()
    {
        var json = """
        {
          "serverUrl": "https://emby.example:443/",
          "username": "test-user",
          "password": "secret"
        }
        """;

        var parsed = DevelopmentLoginCredentials.TryParseJson(
            json,
            out var credentials,
            out var error);

        Assert.True(parsed);
        Assert.Equal("", error);
        Assert.NotNull(credentials);
        Assert.Equal("https://emby.example:443", credentials!.ServerUrl);
        Assert.Equal("test-user", credentials.UserName);
        Assert.Equal("secret", credentials.Password);
    }

    [Theory]
    [InlineData("serverUrl", "")]
    [InlineData("username", "")]
    [InlineData("password", "")]
    public void TryParseJson_Rejects_Blank_Required_Value(string propertyName, string value)
    {
        var json = $$"""
        {
          "serverUrl": "https://emby.example:443",
          "username": "test-user",
          "password": "secret",
          "{{propertyName}}": "{{value}}"
        }
        """;

        var parsed = DevelopmentLoginCredentials.TryParseJson(
            json,
            out var credentials,
            out var error);

        Assert.False(parsed);
        Assert.Null(credentials);
        Assert.Contains(propertyName, error);
    }

    [Fact]
    public void TryParseJson_Rejects_Invalid_Json()
    {
        var parsed = DevelopmentLoginCredentials.TryParseJson(
            "{",
            out var credentials,
            out var error);

        Assert.False(parsed);
        Assert.Null(credentials);
        Assert.Contains("valid JSON", error);
    }
}
