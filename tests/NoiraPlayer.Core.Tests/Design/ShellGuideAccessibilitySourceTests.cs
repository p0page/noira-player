using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class ShellGuideAccessibilitySourceTests
{
    [Fact]
    public void Guide_Active_Route_State_Uses_Matte_Fill_Instead_Of_Bright_Border()
    {
        var root = FindRepositoryRoot();
        var design = File.ReadAllText(Path.Combine(root, "docs", "DESIGN.md"));
        var mainPageXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "MainPage.xaml"));
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "App.xaml"));
        var mainPageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "MainPage.xaml.cs"));

        Assert.Contains("guide_focus_fill:", design);
        Assert.Contains("AppGuideFocusFillColor", appXaml);
        Assert.Contains("AppGuideFocusFillBrush", appXaml);
        Assert.Contains("<muxc:WebView2", mainPageXaml);
        Assert.DoesNotContain("GuideRail", mainPageXaml);
        Assert.DoesNotContain("GuideRail", mainPageSource);
        Assert.DoesNotContain("GuideButtonStyle", mainPageXaml);
        Assert.DoesNotContain("RegisterGuideButtonFocusHandlers", mainPageSource);
        Assert.DoesNotContain("AppGuideActiveBorderBrush", mainPageSource);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "DESIGN.md")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.App", "MainPage.xaml.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
