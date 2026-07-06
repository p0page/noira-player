using NextGenEmby.Core.Input;
using Xunit;

namespace NextGenEmby.Core.Tests.Input;

public sealed class MediaDetailsActionPolicyTests
{
    [Fact]
    public void Resume_Item_Shows_Restart_And_Add_Favorite_Actions()
    {
        var actionState = MediaDetailsActionPolicy.Decide(
            canPlay: true,
            isFavorite: false,
            isPlayed: false,
            playbackPositionTicks: 1200000000);

        Assert.Equal("Resume", actionState.PlayLabel);
        Assert.True(actionState.ShowRestart);
        Assert.Equal("Add favorite", actionState.FavoriteLabel);
        Assert.Equal("Mark watched", actionState.WatchedLabel);
    }

    [Fact]
    public void Played_Favorite_Item_Uses_Remove_And_Unwatched_Actions()
    {
        var actionState = MediaDetailsActionPolicy.Decide(
            canPlay: true,
            isFavorite: true,
            isPlayed: true,
            playbackPositionTicks: 0);

        Assert.Equal("Play", actionState.PlayLabel);
        Assert.False(actionState.ShowRestart);
        Assert.Equal("Remove favorite", actionState.FavoriteLabel);
        Assert.Equal("Mark unwatched", actionState.WatchedLabel);
    }

    [Fact]
    public void Non_Playable_Item_Hides_Restart()
    {
        var actionState = MediaDetailsActionPolicy.Decide(
            canPlay: false,
            isFavorite: false,
            isPlayed: false,
            playbackPositionTicks: 1200000000);

        Assert.Equal("Play", actionState.PlayLabel);
        Assert.False(actionState.ShowRestart);
    }
}
