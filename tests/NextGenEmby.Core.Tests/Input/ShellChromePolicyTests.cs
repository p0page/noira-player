using NextGenEmby.Core.Input;
using Xunit;

namespace NextGenEmby.Core.Tests.Input;

public sealed class ShellChromePolicyTests
{
    [Fact]
    public void Standard_Content_Keeps_Guide_Visible()
    {
        var decision = ShellChromePolicy.GetDecision(ShellContentMode.Standard);

        Assert.True(decision.IsGuideVisible);
        Assert.False(decision.IsContentImmersive);
        Assert.False(decision.BlocksGlobalBack);
        Assert.False(decision.SuppressGuideNavigation);
    }

    [Fact]
    public void Media_Details_Content_Keeps_Guide_Visible()
    {
        var decision = ShellChromePolicy.GetDecision(ShellContentMode.MediaDetails);

        Assert.True(decision.IsGuideVisible);
        Assert.False(decision.IsContentImmersive);
        Assert.False(decision.BlocksGlobalBack);
        Assert.False(decision.SuppressGuideNavigation);
    }

    [Fact]
    public void Playback_Content_Is_Immersive_And_Controls_Back_Itself()
    {
        var decision = ShellChromePolicy.GetDecision(ShellContentMode.Playback);

        Assert.False(decision.IsGuideVisible);
        Assert.True(decision.IsContentImmersive);
        Assert.True(decision.BlocksGlobalBack);
        Assert.True(decision.SuppressGuideNavigation);
    }

    [Fact]
    public void Photo_Viewer_Is_Immersive_But_Allows_Global_Back()
    {
        var decision = ShellChromePolicy.GetDecision(ShellContentMode.PhotoViewer);

        Assert.False(decision.IsGuideVisible);
        Assert.True(decision.IsContentImmersive);
        Assert.False(decision.BlocksGlobalBack);
        Assert.True(decision.SuppressGuideNavigation);
    }
}
