using System.Collections.Generic;
using NoiraPlayer.Core.Diagnostics;
using NoiraPlayer.Core.Emby;
using Xunit;

namespace NoiraPlayer.Core.Tests.Diagnostics;

public sealed class DevelopmentRealDetailsSampleSelectorTests
{
    [Fact]
    public void SelectFirstSupported_Returns_First_Item_With_Hero_Artwork()
    {
        var items = new[]
        {
            Item("no-art", ""),
            Item("dark", "dark-backdrop"),
            Item("bright", "bright-backdrop")
        };

        var sample = DevelopmentRealDetailsSampleSelector.SelectFirstSupported(items);

        Assert.NotNull(sample);
        Assert.Equal("dark", sample!.Id);
    }

    [Fact]
    public void SelectBrightestSupported_Returns_Highest_Scored_Item_With_Hero_Artwork()
    {
        var items = new[]
        {
            Item("no-art", ""),
            Item("dark", "dark-backdrop"),
            Item("bright", "bright-backdrop"),
            Item("missing-score", "missing-score-backdrop")
        };
        var scores = new Dictionary<string, double>
        {
            ["dark"] = 0.18,
            ["bright"] = 0.74
        };

        var sample = DevelopmentRealDetailsSampleSelector.SelectBrightestSupported(items, scores);

        Assert.NotNull(sample);
        Assert.Equal("bright", sample!.Id);
    }

    [Fact]
    public void SelectBrightestSupported_Falls_Back_To_First_Supported_When_No_Scores_Are_Available()
    {
        var items = new[]
        {
            Item("no-art", ""),
            Item("first-supported", "first-backdrop"),
            Item("second-supported", "second-backdrop")
        };

        var sample = DevelopmentRealDetailsSampleSelector.SelectBrightestSupported(
            items,
            new Dictionary<string, double>());

        Assert.NotNull(sample);
        Assert.Equal("first-supported", sample!.Id);
    }

    private static EmbyMediaItem Item(string id, string backdropTag)
    {
        return new EmbyMediaItem
        {
            Id = id,
            Name = id,
            Type = "Movie",
            BackdropImageTag = backdropTag,
            BackdropImageItemId = id
        };
    }
}
