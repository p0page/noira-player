using NextGenEmby.Core.Input;
using Xunit;

namespace NextGenEmby.Core.Tests.Input;

public sealed class LibraryOptionSheetPolicyTests
{
    [Fact]
    public void Open_Clamps_Preview_Index_To_Available_Options()
    {
        var decision = LibraryOptionSheetPolicy.Open(optionCount: 3, committedIndex: 9);

        Assert.Equal(2, decision.Index);
        Assert.False(decision.ShouldReload);
        Assert.False(decision.ShouldClose);
    }

    [Fact]
    public void MovePreview_Stops_At_First_And_Last_Option()
    {
        Assert.Equal(0, LibraryOptionSheetPolicy.MovePreviewIndex(currentIndex: 0, optionCount: 3, offset: -1));
        Assert.Equal(2, LibraryOptionSheetPolicy.MovePreviewIndex(currentIndex: 2, optionCount: 3, offset: 1));
    }

    [Fact]
    public void Confirm_Reloads_Only_When_Selected_Option_Changed()
    {
        var changed = LibraryOptionSheetPolicy.Confirm(committedIndex: 0, previewIndex: 1, optionCount: 3);
        var unchanged = LibraryOptionSheetPolicy.Confirm(committedIndex: 1, previewIndex: 1, optionCount: 3);

        Assert.Equal(1, changed.Index);
        Assert.True(changed.ShouldReload);
        Assert.True(changed.ShouldClose);
        Assert.False(unchanged.ShouldReload);
        Assert.True(unchanged.ShouldClose);
    }

    [Fact]
    public void Cancel_Keeps_Committed_Option_And_Closes_Sheet()
    {
        var decision = LibraryOptionSheetPolicy.Cancel(committedIndex: 1, optionCount: 3);

        Assert.Equal(1, decision.Index);
        Assert.False(decision.ShouldReload);
        Assert.True(decision.ShouldClose);
    }
}
