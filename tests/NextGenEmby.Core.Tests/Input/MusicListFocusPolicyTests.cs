using NextGenEmby.Core.Input;
using Xunit;

namespace NextGenEmby.Core.Tests.Input;

public sealed class MusicListFocusPolicyTests
{
    [Theory]
    [InlineData(0, 3, true, false, 1)]
    [InlineData(1, 3, true, false, 2)]
    [InlineData(2, 3, true, false, null)]
    [InlineData(2, 3, false, true, 1)]
    [InlineData(1, 3, false, true, 0)]
    [InlineData(0, 3, false, true, null)]
    [InlineData(0, 0, true, false, null)]
    public void GetVerticalTargetIndex_Moves_Within_List_Bounds(
        int currentIndex,
        int itemCount,
        bool moveDown,
        bool moveUp,
        int? expected)
    {
        var target = MusicListFocusPolicy.GetVerticalTargetIndex(
            currentIndex,
            itemCount,
            moveDown,
            moveUp);

        Assert.Equal(expected, target);
    }
}
