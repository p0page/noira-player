using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class MediaDetailsDefaultFocusPolicyTests
{
    [Fact]
    public void Playable_Item_Focuses_Play()
    {
        Assert.Equal(
            MediaDetailsDefaultFocusTarget.Play,
            MediaDetailsDefaultFocusPolicy.Decide(playEnabled: true, episodeButtonCount: 8));
    }

    [Fact]
    public void Non_Playable_Series_Focuses_First_Episode_When_Available()
    {
        Assert.Equal(
            MediaDetailsDefaultFocusTarget.FirstEpisode,
            MediaDetailsDefaultFocusPolicy.Decide(playEnabled: false, episodeButtonCount: 1));
    }

    [Fact]
    public void Non_Playable_Item_Falls_Back_To_Refresh()
    {
        Assert.Equal(
            MediaDetailsDefaultFocusTarget.Refresh,
            MediaDetailsDefaultFocusPolicy.Decide(playEnabled: false, episodeButtonCount: 0));
    }
}
