using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class GlobalBackInputPolicyTests
{
    [Fact]
    public void Does_Not_Go_Back_When_Event_Was_Already_Handled()
    {
        var shouldGoBack = GlobalBackInputPolicy.ShouldGoBack(
            eventAlreadyHandled: true,
            playbackPageActive: false,
            frameCanGoBack: true,
            backKeyPressed: true);

        Assert.False(shouldGoBack);
    }

    [Fact]
    public void Goes_Back_For_Unhandled_Back_Key_When_Frame_Can_Go_Back()
    {
        var shouldGoBack = GlobalBackInputPolicy.ShouldGoBack(
            eventAlreadyHandled: false,
            playbackPageActive: false,
            frameCanGoBack: true,
            backKeyPressed: true);

        Assert.True(shouldGoBack);
    }
}
