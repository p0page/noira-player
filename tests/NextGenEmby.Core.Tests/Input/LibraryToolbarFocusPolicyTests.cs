using NextGenEmby.Core.Input;
using Xunit;

namespace NextGenEmby.Core.Tests.Input;

public sealed class LibraryToolbarFocusPolicyTests
{
    [Fact]
    public void Right_Moves_From_Sort_To_Filter_To_Refresh()
    {
        Assert.Equal(
            LibraryToolbarFocusTarget.Filter,
            LibraryToolbarFocusPolicy.Move(
                LibraryToolbarFocusTarget.Sort,
                LibraryToolbarFocusDirection.Right,
                sectionRequest: false));

        Assert.Equal(
            LibraryToolbarFocusTarget.Refresh,
            LibraryToolbarFocusPolicy.Move(
                LibraryToolbarFocusTarget.Filter,
                LibraryToolbarFocusDirection.Right,
                sectionRequest: false));
    }

    [Fact]
    public void Left_Moves_From_Refresh_To_Filter_To_Sort()
    {
        Assert.Equal(
            LibraryToolbarFocusTarget.Filter,
            LibraryToolbarFocusPolicy.Move(
                LibraryToolbarFocusTarget.Refresh,
                LibraryToolbarFocusDirection.Left,
                sectionRequest: false));

        Assert.Equal(
            LibraryToolbarFocusTarget.Sort,
            LibraryToolbarFocusPolicy.Move(
                LibraryToolbarFocusTarget.Filter,
                LibraryToolbarFocusDirection.Left,
                sectionRequest: false));
    }

    [Fact]
    public void Section_Request_Does_Not_Move_Between_Hidden_Filter_Controls()
    {
        Assert.Equal(
            LibraryToolbarFocusTarget.None,
            LibraryToolbarFocusPolicy.Move(
                LibraryToolbarFocusTarget.Refresh,
                LibraryToolbarFocusDirection.Left,
                sectionRequest: true));
    }
}
