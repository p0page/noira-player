using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class PhotoViewerInputPolicyTests
{
    [Fact]
    public void Back_Key_Returns_To_Previous_Page_When_History_Exists()
    {
        var shouldGoBack = PhotoViewerInputPolicy.ShouldGoBack(
            frameCanGoBack: true,
            backKeyPressed: true);

        Assert.True(shouldGoBack);
    }

    [Fact]
    public void Back_Key_Does_Not_Return_When_No_History_Exists()
    {
        var shouldGoBack = PhotoViewerInputPolicy.ShouldGoBack(
            frameCanGoBack: false,
            backKeyPressed: true);

        Assert.False(shouldGoBack);
    }

    [Fact]
    public void Non_Back_Key_Does_Not_Return()
    {
        var shouldGoBack = PhotoViewerInputPolicy.ShouldGoBack(
            frameCanGoBack: true,
            backKeyPressed: false);

        Assert.False(shouldGoBack);
    }
}
