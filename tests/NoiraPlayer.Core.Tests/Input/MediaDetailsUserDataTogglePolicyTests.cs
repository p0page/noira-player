using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class MediaDetailsUserDataTogglePolicyTests
{
    [Fact]
    public void ToggleFavorite_Flips_Favorite_And_Preserves_Played_State()
    {
        var current = new EmbyUserData
        {
            IsFavorite = false,
            Played = true,
            PlaybackPositionTicks = 123456789,
            PlayedPercentage = 72.5
        };

        var updated = MediaDetailsUserDataTogglePolicy.ToggleFavorite(current);

        Assert.True(updated.IsFavorite);
        Assert.True(updated.Played);
        Assert.Equal(123456789, updated.PlaybackPositionTicks);
        Assert.Equal(72.5, updated.PlayedPercentage);
    }

    [Fact]
    public void TogglePlayed_Flips_Played_And_Preserves_Favorite_State()
    {
        var current = new EmbyUserData
        {
            IsFavorite = true,
            Played = false,
            PlaybackPositionTicks = 987654321,
            PlayedPercentage = 18.25
        };

        var updated = MediaDetailsUserDataTogglePolicy.TogglePlayed(current);

        Assert.True(updated.IsFavorite);
        Assert.True(updated.Played);
        Assert.Equal(987654321, updated.PlaybackPositionTicks);
        Assert.Equal(18.25, updated.PlayedPercentage);
    }
}
