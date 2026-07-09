using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class MediaDetailsActionNavigationPolicyTests
{
    [Fact]
    public void Right_Moves_Through_Visible_Actions_When_Restart_Is_Visible()
    {
        Assert.Equal(
            MediaDetailsActionButton.Restart,
            MediaDetailsActionNavigationPolicy.MoveHorizontal(
                MediaDetailsActionButton.Play,
                delta: 1,
                restartVisible: true));

        Assert.Equal(
            MediaDetailsActionButton.Favorite,
            MediaDetailsActionNavigationPolicy.MoveHorizontal(
                MediaDetailsActionButton.Restart,
                delta: 1,
                restartVisible: true));
    }

    [Fact]
    public void Hidden_Restart_Is_Skipped()
    {
        Assert.Equal(
            MediaDetailsActionButton.Favorite,
            MediaDetailsActionNavigationPolicy.MoveHorizontal(
                MediaDetailsActionButton.Play,
                delta: 1,
                restartVisible: false));

        Assert.Equal(
            MediaDetailsActionButton.Play,
            MediaDetailsActionNavigationPolicy.MoveHorizontal(
                MediaDetailsActionButton.Favorite,
                delta: -1,
                restartVisible: false));
    }

    [Fact]
    public void Movement_Stops_At_Action_Row_Edges()
    {
        Assert.Null(MediaDetailsActionNavigationPolicy.MoveHorizontal(
            MediaDetailsActionButton.Play,
            delta: -1,
            restartVisible: true));

        Assert.Null(MediaDetailsActionNavigationPolicy.MoveHorizontal(
            MediaDetailsActionButton.Refresh,
            delta: 1,
            restartVisible: true));
    }
}
