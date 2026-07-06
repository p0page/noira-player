using NextGenEmby.Core.Input;
using Xunit;

namespace NextGenEmby.Core.Tests.Input;

public sealed class HomeFocusInputPolicyTests
{
    [Fact]
    public void Down_From_Library_Enters_First_Content_Row()
    {
        var next = HomeFocusInputPolicy.Move(
            new HomeFocusTarget(HomeFocusZone.Library, 0),
            HomeFocusDirection.Down,
            libraryCount: 4,
            rowCount: 3);

        Assert.NotNull(next);
        Assert.Equal(HomeFocusZone.Row, next!.Zone);
        Assert.Equal(0, next.Index);
    }

    [Fact]
    public void Up_From_First_Content_Row_Returns_To_First_Library()
    {
        var next = HomeFocusInputPolicy.Move(
            new HomeFocusTarget(HomeFocusZone.Row, 0),
            HomeFocusDirection.Up,
            libraryCount: 4,
            rowCount: 3);

        Assert.NotNull(next);
        Assert.Equal(HomeFocusZone.Library, next!.Zone);
        Assert.Equal(0, next.Index);
    }

    [Fact]
    public void Down_From_Content_Row_Enters_Next_Row()
    {
        var next = HomeFocusInputPolicy.Move(
            new HomeFocusTarget(HomeFocusZone.Row, 0),
            HomeFocusDirection.Down,
            libraryCount: 4,
            rowCount: 3);

        Assert.NotNull(next);
        Assert.Equal(HomeFocusZone.Row, next!.Zone);
        Assert.Equal(1, next.Index);
    }

    [Fact]
    public void Hero_Down_Prefers_Library_Before_Content_Rows()
    {
        var next = HomeFocusInputPolicy.Move(
            new HomeFocusTarget(HomeFocusZone.HeroPlay, 0),
            HomeFocusDirection.Down,
            libraryCount: 4,
            rowCount: 3);

        Assert.NotNull(next);
        Assert.Equal(HomeFocusZone.Library, next!.Zone);
        Assert.Equal(0, next.Index);
    }

    [Fact]
    public void Library_Right_Stops_At_Last_Library()
    {
        var next = HomeFocusInputPolicy.Move(
            new HomeFocusTarget(HomeFocusZone.Library, 3),
            HomeFocusDirection.Right,
            libraryCount: 4,
            rowCount: 3);

        Assert.Null(next);
    }
}
