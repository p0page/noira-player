using System.Linq;
using NoiraPlayer.Core.Diagnostics;
using Xunit;

namespace NoiraPlayer.Core.Tests.Diagnostics;

public sealed class DevelopmentLiveTvFixtureTests
{
    [Fact]
    public void Create_Provides_Channels_And_Current_Programs()
    {
        var fixture = DevelopmentLiveTvFixture.Create();

        Assert.True(fixture.Channels.Count >= 4);
        Assert.Contains(fixture.Channels, channel => channel.Number == "101" && channel.CurrentProgram != null);
        Assert.Contains(fixture.Channels, channel => channel.CurrentProgram != null && channel.CurrentProgram.IsNews);
        Assert.Contains(fixture.Channels, channel => channel.CurrentProgram != null && channel.CurrentProgram.IsSports);
        Assert.Contains(fixture.Channels, channel => channel.CurrentProgram != null && channel.CurrentProgram.IsKids);
        Assert.Empty(fixture.ArtworkUris);
    }

    [Fact]
    public void Create_Does_Not_Depend_On_Packaged_Current_Program_Artwork()
    {
        var fixture = DevelopmentLiveTvFixture.Create();

        foreach (var channel in fixture.Channels)
        {
            Assert.NotNull(channel.CurrentProgram);
            var program = channel.CurrentProgram!;
            Assert.False(string.IsNullOrWhiteSpace(program.Id));
            Assert.True(string.IsNullOrWhiteSpace(program.ThumbImageTag));
            Assert.False(fixture.ArtworkUris.ContainsKey(DevelopmentLiveTvFixture.ArtworkKey(program.Id, "Thumb")));
        }
    }

    [Fact]
    public void ArtworkUris_Are_Empty_After_Removing_Packaged_Qa_Assets()
    {
        var fixture = DevelopmentLiveTvFixture.Create();

        Assert.Empty(fixture.ArtworkUris);
        Assert.All(fixture.Channels, channel => Assert.True(string.IsNullOrWhiteSpace(channel.PrimaryImageTag)));
    }
}
