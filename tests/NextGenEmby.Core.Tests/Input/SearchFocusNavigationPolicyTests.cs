using NextGenEmby.Core.Input;
using Xunit;

namespace NextGenEmby.Core.Tests.Input;

public sealed class SearchFocusNavigationPolicyTests
{
    [Fact]
    public void Handled_Down_From_Search_Box_Focuses_Selected_Scope()
    {
        var decision = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.SearchBox,
            moveUpKeyPressed: false,
            moveDownKeyPressed: true,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: false);

        Assert.Equal(SearchFocusNavigationAction.FocusSelectedScope, decision.Action);
    }

    [Fact]
    public void Down_From_Scope_Focuses_First_Result()
    {
        var decision = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.ScopeRail,
            moveUpKeyPressed: false,
            moveDownKeyPressed: true,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: false);

        Assert.Equal(SearchFocusNavigationAction.FocusFirstResult, decision.Action);
    }

    [Fact]
    public void Down_From_Scope_Focuses_Recent_Terms_When_Visible()
    {
        var decision = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.ScopeRail,
            moveUpKeyPressed: false,
            moveDownKeyPressed: true,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: false,
            emptyStateVisible: false,
            recentTermsVisible: true);

        Assert.Equal(SearchFocusNavigationAction.FocusRecentTerms, decision.Action);
    }

    [Fact]
    public void Down_From_Recent_Terms_Focuses_First_Result()
    {
        var decision = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.RecentTerms,
            moveUpKeyPressed: false,
            moveDownKeyPressed: true,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: false,
            emptyStateVisible: false,
            recentTermsVisible: true);

        Assert.Equal(SearchFocusNavigationAction.FocusFirstResult, decision.Action);
    }

    [Fact]
    public void Down_From_Recent_Terms_Focuses_Empty_State_When_Visible()
    {
        var decision = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.RecentTerms,
            moveUpKeyPressed: false,
            moveDownKeyPressed: true,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: false,
            emptyStateVisible: true,
            recentTermsVisible: true);

        Assert.Equal(SearchFocusNavigationAction.FocusEmptyState, decision.Action);
    }

    [Fact]
    public void Down_From_Scope_Focuses_Empty_State_When_Visible()
    {
        var decision = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.ScopeRail,
            moveUpKeyPressed: false,
            moveDownKeyPressed: true,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: false,
            emptyStateVisible: true);

        Assert.Equal(SearchFocusNavigationAction.FocusEmptyState, decision.Action);
    }

    [Fact]
    public void Up_From_Empty_State_Returns_To_Selected_Scope()
    {
        var decision = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.EmptyState,
            moveUpKeyPressed: true,
            moveDownKeyPressed: false,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: false,
            emptyStateVisible: true);

        Assert.Equal(SearchFocusNavigationAction.FocusSelectedScope, decision.Action);
    }

    [Fact]
    public void Up_From_Recent_Terms_Returns_To_Selected_Scope()
    {
        var decision = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.RecentTerms,
            moveUpKeyPressed: true,
            moveDownKeyPressed: false,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: false,
            emptyStateVisible: false,
            recentTermsVisible: true);

        Assert.Equal(SearchFocusNavigationAction.FocusSelectedScope, decision.Action);
    }

    [Fact]
    public void Right_And_Left_Move_Only_Inside_Scope_Rail()
    {
        var right = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.ScopeRail,
            moveUpKeyPressed: false,
            moveDownKeyPressed: false,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: true,
            focusedResultInFirstRow: false);

        var left = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.ScopeRail,
            moveUpKeyPressed: false,
            moveDownKeyPressed: false,
            moveLeftKeyPressed: true,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: false);

        var resultRight = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: false,
            focusArea: SearchFocusArea.ResultGrid,
            moveUpKeyPressed: false,
            moveDownKeyPressed: false,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: true,
            focusedResultInFirstRow: false);

        Assert.Equal(SearchFocusNavigationAction.MoveScopeRight, right.Action);
        Assert.Equal(SearchFocusNavigationAction.MoveScopeLeft, left.Action);
        Assert.Equal(SearchFocusNavigationAction.None, resultRight.Action);
    }

    [Fact]
    public void Right_And_Left_Move_Inside_Recent_Terms()
    {
        var right = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.RecentTerms,
            moveUpKeyPressed: false,
            moveDownKeyPressed: false,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: true,
            focusedResultInFirstRow: false,
            emptyStateVisible: false,
            recentTermsVisible: true);

        var left = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.RecentTerms,
            moveUpKeyPressed: false,
            moveDownKeyPressed: false,
            moveLeftKeyPressed: true,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: false,
            emptyStateVisible: false,
            recentTermsVisible: true);

        Assert.Equal(SearchFocusNavigationAction.MoveRecentLeft, left.Action);
        Assert.Equal(SearchFocusNavigationAction.MoveRecentRight, right.Action);
    }

    [Fact]
    public void Up_From_First_Result_Row_Returns_To_Selected_Scope()
    {
        var decision = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: false,
            focusArea: SearchFocusArea.ResultGrid,
            moveUpKeyPressed: true,
            moveDownKeyPressed: false,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: true);

        Assert.Equal(SearchFocusNavigationAction.FocusSelectedScope, decision.Action);
    }

    [Fact]
    public void Up_From_First_Result_Row_Returns_To_Recent_Terms_When_Visible()
    {
        var decision = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: false,
            focusArea: SearchFocusArea.ResultGrid,
            moveUpKeyPressed: true,
            moveDownKeyPressed: false,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: true,
            emptyStateVisible: false,
            recentTermsVisible: true);

        Assert.Equal(SearchFocusNavigationAction.FocusRecentTerms, decision.Action);
    }

    [Fact]
    public void Handled_Event_Outside_Search_Controls_Is_Ignored()
    {
        var decision = SearchFocusNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            focusArea: SearchFocusArea.Other,
            moveUpKeyPressed: false,
            moveDownKeyPressed: true,
            moveLeftKeyPressed: false,
            moveRightKeyPressed: false,
            focusedResultInFirstRow: false);

        Assert.Equal(SearchFocusNavigationAction.None, decision.Action);
    }
}
