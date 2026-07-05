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

    [Theory]
    [InlineData("details")]
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
