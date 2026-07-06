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
    [InlineData("LiveTv", "livetv")]
    [InlineData("Music", "music")]
    [InlineData("Photos", "photos")]
    [InlineData("Playlists", "playlists")]
    [InlineData("Favorites", "favorites")]
    [InlineData("Unwatched", "unwatched")]
    [InlineData("LiveTv-Unsupported", "livetv-unsupported")]
    [InlineData("Music-Unsupported", "music-unsupported")]
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
    public void TryParseJson_Accepts_QualityRun_With_Expected_Thresholds()
    {
        const string json = """
        {
          "route": "quality-run",
          "runId": "hdr10-007-start0",
          "itemId": "677521",
          "itemName": "007",
          "mediaSourceId": "source-hdr10",
          "startPositionTicks": 123,
          "durationSeconds": 60,
          "forceSdrOutput": true,
          "expected": {
            "frameRate": 23.976,
            "hdrOutput": "Hdr10",
            "dxgiInput": "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
            "dxgiOutput": "RGB_FULL_G2084_NONE_P2020",
            "maxDroppedFrames": 1,
            "maxFrameGapMs": 105,
            "maxAudioVideoDriftMsP95": 40,
            "maxVideoStarvedPasses": 0,
            "maxAudioStarvedPasses": 0
          }
        }
        """;

        var ok = DevelopmentNavigationCommand.TryParseJson(
            json,
            out var command,
            out var error);

        Assert.True(ok, error);
        Assert.NotNull(command);
        Assert.Equal("quality-run", command!.Route);
        Assert.Equal("hdr10-007-start0", command.RunId);
        Assert.Equal("677521", command.ItemId);
        Assert.Equal("source-hdr10", command.MediaSourceId);
        Assert.Equal(123, command.StartPositionTicks);
        Assert.Equal(60, command.DurationSeconds);
        Assert.True(command.ForceSdrOutput);
        Assert.NotNull(command.Expected);
        Assert.Equal("Hdr10", command.Expected!.HdrOutput);
        Assert.Equal(23.976, command.Expected.FrameRate);
    }

    [Fact]
    public void TryParseJson_Rejects_QualityRun_Without_ItemId()
    {
        const string json = """
        {
          "route": "quality-run",
          "runId": "missing-item",
          "mediaSourceId": "source",
          "durationSeconds": 5
        }
        """;

        var ok = DevelopmentNavigationCommand.TryParseJson(
            json,
            out var command,
            out var error);

        Assert.False(ok);
        Assert.Null(command);
        Assert.Equal("dev-command.json route requires itemId.", error);
    }

    [Fact]
    public void TryParseJson_Clamps_QualityRun_Duration_To_Safe_Range()
    {
        const string json = """
        {
          "route": "quality-run",
          "runId": "short",
          "itemId": "item",
          "durationSeconds": 0
        }
        """;

        var ok = DevelopmentNavigationCommand.TryParseJson(
            json,
            out var command,
            out var error);

        Assert.True(ok, error);
        Assert.Equal(10, command!.DurationSeconds);
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
