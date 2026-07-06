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
            selectedDestination: GuideNavigationDestination.Movies);

        Assert.Equal(GuideNavigationAction.Navigate, decision.Action);
        Assert.Equal(GuideNavigationDestination.Movies, decision.Destination);
        Assert.False(decision.ShouldRestorePreviousFocus);
    }
}
