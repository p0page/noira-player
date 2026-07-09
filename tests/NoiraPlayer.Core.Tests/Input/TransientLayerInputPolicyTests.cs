using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class TransientLayerInputPolicyTests
{
    [Fact]
    public void Back_Key_Dismisses_Visible_Layer()
    {
        var shouldDismiss = TransientLayerInputPolicy.ShouldDismiss(
            layerVisible: true,
            backKeyPressed: true);

        Assert.True(shouldDismiss);
    }

    [Fact]
    public void Back_Key_Does_Not_Dismiss_Hidden_Layer()
    {
        var shouldDismiss = TransientLayerInputPolicy.ShouldDismiss(
            layerVisible: false,
            backKeyPressed: true);

        Assert.False(shouldDismiss);
    }

    [Fact]
    public void Non_Back_Key_Does_Not_Dismiss_Visible_Layer()
    {
        var shouldDismiss = TransientLayerInputPolicy.ShouldDismiss(
            layerVisible: true,
            backKeyPressed: false);

        Assert.False(shouldDismiss);
    }
}
