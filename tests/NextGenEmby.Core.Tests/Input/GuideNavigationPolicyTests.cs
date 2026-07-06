using NextGenEmby.Core.Input;
using Xunit;

namespace NextGenEmby.Core.Tests.Input;

public sealed class GuideNavigationPolicyTests
{
    [Fact]
    public void Menu_Opens_Guide_When_Content_Page_Is_Active()
    {
        var decision = GuideNavigationPolicy.GetDecision(
            eventAlreadyHandled: false,
            playbackPageActive: false,
            guideOpen: false,
            menuKeyPressed: true,
            backKeyPressed: false,
            selectKeyPressed: false,
            moveUpKeyPressed: false,
            moveDownKeyPressed: false,
            selectedDestination: GuideNavigationDestination.Home);

        Assert.Equal(GuideNavigationAction.OpenGuide, decision.Action);
        Assert.False(decision.ShouldRestorePreviousFocus);
    }

    [Fact]
    public void Menu_Does_Not_Open_Guide_Over_Playback()
    {
        var decision = GuideNavigationPolicy.GetDecision(
            eventAlreadyHandled: false,
            playbackPageActive: true,
            guideOpen: false,
            menuKeyPressed: true,
            backKeyPressed: false,
            selectKeyPressed: false,
            moveUpKeyPressed: false,
            moveDownKeyPressed: false,
            selectedDestination: GuideNavigationDestination.Home);

        Assert.Equal(GuideNavigationAction.None, decision.Action);
    }

    [Fact]
    public void Back_Closes_Open_Guide_And_Restores_Previous_Focus()
    {
        var decision = GuideNavigationPolicy.GetDecision(
            eventAlreadyHandled: false,
            playbackPageActive: false,
            guideOpen: true,
            menuKeyPressed: false,
            backKeyPressed: true,
            selectKeyPressed: false,
            moveUpKeyPressed: false,
            moveDownKeyPressed: false,
            selectedDestination: GuideNavigationDestination.Search);

        Assert.Equal(GuideNavigationAction.CloseGuide, decision.Action);
        Assert.True(decision.ShouldRestorePreviousFocus);
    }

    [Fact]
    public void Select_Navigates_To_Focused_Guide_Destination()
    {
        var decision = GuideNavigationPolicy.GetDecision(
            eventAlreadyHandled: false,
            playbackPageActive: false,
            guideOpen: true,
            menuKeyPressed: false,
            backKeyPressed: false,
            selectKeyPressed: true,
            moveUpKeyPressed: false,
            moveDownKeyPressed: false,
            selectedDestination: GuideNavigationDestination.Movies);

        Assert.Equal(GuideNavigationAction.Navigate, decision.Action);
        Assert.Equal(GuideNavigationDestination.Movies, decision.Destination);
        Assert.False(decision.ShouldRestorePreviousFocus);
    }

    [Fact]
    public void Handled_Select_Still_Navigates_When_Guide_Is_Open()
    {
        var decision = GuideNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            playbackPageActive: false,
            guideOpen: true,
            menuKeyPressed: false,
            backKeyPressed: false,
            selectKeyPressed: true,
            moveUpKeyPressed: false,
            moveDownKeyPressed: false,
            selectedDestination: GuideNavigationDestination.Search);

        Assert.Equal(GuideNavigationAction.Navigate, decision.Action);
        Assert.Equal(GuideNavigationDestination.Search, decision.Destination);
    }

    [Fact]
    public void Down_Moves_Open_Guide_Selection_In_Menu_Order()
    {
        var decision = GuideNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            playbackPageActive: false,
            guideOpen: true,
            menuKeyPressed: false,
            backKeyPressed: false,
            selectKeyPressed: false,
            moveUpKeyPressed: false,
            moveDownKeyPressed: true,
            selectedDestination: GuideNavigationDestination.Home);

        Assert.Equal(GuideNavigationAction.MoveSelection, decision.Action);
        Assert.Equal(GuideNavigationDestination.Search, decision.Destination);
    }

    [Fact]
    public void Up_Moves_Open_Guide_Selection_In_Menu_Order()
    {
        var decision = GuideNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            playbackPageActive: false,
            guideOpen: true,
            menuKeyPressed: false,
            backKeyPressed: false,
            selectKeyPressed: false,
            moveUpKeyPressed: true,
            moveDownKeyPressed: false,
            selectedDestination: GuideNavigationDestination.Movies);

        Assert.Equal(GuideNavigationAction.MoveSelection, decision.Action);
        Assert.Equal(GuideNavigationDestination.Search, decision.Destination);
    }

    [Fact]
    public void Handled_Menu_Does_Not_Open_Guide_When_Guide_Is_Closed()
    {
        var decision = GuideNavigationPolicy.GetDecision(
            eventAlreadyHandled: true,
            playbackPageActive: false,
            guideOpen: false,
            menuKeyPressed: true,
            backKeyPressed: false,
            selectKeyPressed: false,
            moveUpKeyPressed: false,
            moveDownKeyPressed: false,
            selectedDestination: GuideNavigationDestination.Home);

        Assert.Equal(GuideNavigationAction.None, decision.Action);
    }
}
