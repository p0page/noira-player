using NextGenEmby.Core.Diagnostics;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentNavigationCommandTests
{
    [Fact]
    public void TryParseJson_Accepts_Library_Route()
    {
        var parsed = DevelopmentNavigationCommand.TryParseJson(
            """
            {
              "route": "Movies"
            }
            """,
            out var command,
            out var error);

        Assert.True(parsed);
        Assert.Equal("", error);
        Assert.NotNull(command);
        Assert.Equal("movies", command!.Route);
        Assert.Equal("", command.ItemId);
    }

    [Theory]
    [InlineData("Login", "login")]
    [InlineData("LiveTv", "livetv")]
    [InlineData("LiveTv-Fixture", "livetv-fixture")]
    [InlineData("Music", "music")]
    [InlineData("Photos", "photos")]
    [InlineData("Playlists", "playlists")]
    [InlineData("Favorites", "favorites")]
    [InlineData("Unwatched", "unwatched")]
    [InlineData("LiveTv-Unsupported", "livetv-unsupported")]
    [InlineData("Music-Unsupported", "music-unsupported")]
    [InlineData("Music-Fixture", "music-fixture")]
    [InlineData("Photos-Fixture", "photos-fixture")]
    [InlineData("Collections-Fixture", "collections-fixture")]
    [InlineData("Playlists-Fixture", "playlists-fixture")]
    [InlineData("Movies-Fixture", "movies-fixture")]
    [InlineData("Home-Fixture", "home-fixture")]
    [InlineData("Search-Fixture", "search-fixture")]
    [InlineData("Search-Error", "search-error")]
    [InlineData("Details-Fixture", "details-fixture")]
    [InlineData("Details-Real-Sample", "details-real-sample")]
    [InlineData("Details-Real-Bright-Sample", "details-real-bright-sample")]
    [InlineData("Details-Long-Source-Fixture", "details-long-source-fixture")]
    [InlineData("Playback-Options-Fixture", "playback-options-fixture")]
    public void TryParseJson_Accepts_Guide_Routes(string route, string normalizedRoute)
    {
        var parsed = DevelopmentNavigationCommand.TryParseJson(
            $$"""
            {
              "route": "{{route}}"
            }
            """,
            out var command,
            out var error);

        Assert.True(parsed);
        Assert.Equal("", error);
        Assert.NotNull(command);
        Assert.Equal(normalizedRoute, command!.Route);
    }

    [Fact]
    public void TryParseJson_Accepts_Playback_Route()
    {
        var parsed = DevelopmentNavigationCommand.TryParseJson(
            """
            {
              "route": "playback",
              "itemId": "item-123",
              "itemName": "Example Movie",
              "startPositionTicks": -100,
              "mediaSourceId": "source-1",
              "forceSdrOutput": true
            }
            """,
            out var command,
            out var error);

        Assert.True(parsed);
        Assert.Equal("", error);
        Assert.NotNull(command);
        Assert.Equal("playback", command!.Route);
        Assert.Equal("item-123", command.ItemId);
        Assert.Equal("Example Movie", command.ItemName);
        Assert.Equal(0, command.StartPositionTicks);
        Assert.Equal("source-1", command.MediaSourceId);
        Assert.True(command.ForceSdrOutput);
    }

    [Fact]
    public void TryParseJson_Accepts_Manual_Playback_Route_Without_ItemId()
    {
        var parsed = DevelopmentNavigationCommand.TryParseJson(
            """
            {
              "route": "manual-playback"
            }
            """,
            out var command,
            out var error);

        Assert.True(parsed);
        Assert.Equal("", error);
        Assert.NotNull(command);
        Assert.Equal("manual-playback", command!.Route);
        Assert.Equal("", command.ItemId);
    }

    [Fact]
    public void TryParseJson_Accepts_Manual_Playback_Stream_Url_And_AutoStart()
    {
        var parsed = DevelopmentNavigationCommand.TryParseJson(
            """
            {
              "route": "manual-playback",
              "streamUrl": " https://media.example.test/sample.mp4 ",
              "autoStart": true
            }
            """,
            out var command,
            out var error);

        Assert.True(parsed);
        Assert.Equal("", error);
        Assert.NotNull(command);
        Assert.Equal("manual-playback", command!.Route);
        Assert.Equal("https://media.example.test/sample.mp4", command.StreamUrl);
        Assert.True(command.AutoStart);
    }

    [Fact]
    public void TryParseJson_Accepts_Photo_Route()
    {
        var parsed = DevelopmentNavigationCommand.TryParseJson(
            """
            {
              "route": "photo",
              "itemId": "photo-123",
              "itemName": "Vacation"
            }
            """,
            out var command,
            out var error);

        Assert.True(parsed);
        Assert.Equal("", error);
        Assert.NotNull(command);
        Assert.Equal("photo", command!.Route);
        Assert.Equal("photo-123", command.ItemId);
        Assert.Equal("Vacation", command.ItemName);
    }

    [Theory]
    [InlineData("details")]
    [InlineData("photo")]
    [InlineData("playback")]
    public void TryParseJson_Rejects_Item_Route_Without_ItemId(string route)
    {
        var parsed = DevelopmentNavigationCommand.TryParseJson(
            $$"""
            {
              "route": "{{route}}"
            }
            """,
            out var command,
            out var error);

        Assert.False(parsed);
        Assert.Null(command);
        Assert.Contains("itemId", error);
    }

    [Fact]
    public void TryParseJson_Rejects_Unsupported_Route()
    {
        var parsed = DevelopmentNavigationCommand.TryParseJson(
            """
            {
              "route": "unsupported"
            }
            """,
            out var command,
            out var error);

        Assert.False(parsed);
        Assert.Null(command);
        Assert.Contains("route", error);
    }
}
