using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class ShellGuideAccessibilitySourceTests
{
    [Fact]
    public void Guide_Active_Route_State_Uses_Matte_Fill_Instead_Of_Bright_Border()
    {
        var root = FindRepositoryRoot();
        var design = File.ReadAllText(Path.Combine(root, "docs", "DESIGN.md"));
        var mainPageXaml = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "MainPage.xaml"));
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "App.xaml"));
        var mainPageSource = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "MainPage.xaml.cs"));

        Assert.Contains("guide_focus_fill:", design);
        Assert.Contains("AppGuideFocusFillColor", appXaml);
        Assert.Contains("AppGuideFocusFillBrush", appXaml);
        Assert.Contains("<Setter Property=\"UseSystemFocusVisuals\" Value=\"False\" />", mainPageXaml);
        Assert.Contains("button.Background = (Brush)resources[isActive ? \"AppGuideFocusFillBrush\" : \"AppTransparentBrush\"];", mainPageSource);
        Assert.Contains("button.BorderBrush = (Brush)resources[\"AppTransparentBrush\"];", mainPageSource);
        Assert.DoesNotContain("AppGuideActiveBorderBrush", mainPageSource);
        Assert.DoesNotContain("button.BorderBrush = (Brush)resources[isActive ? \"AppAccentBrush\" : \"AppTransparentBrush\"];", mainPageSource);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "DESIGN.md")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NextGenEmby.App", "MainPage.xaml.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
