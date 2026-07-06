using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class ShellGuideAccessibilitySourceTests
{
    [Fact]
    public void Guide_Active_Route_State_Does_Not_ReUse_Controller_Focus_Border_Color()
    {
        var root = FindRepositoryRoot();
        var design = File.ReadAllText(Path.Combine(root, "docs", "DESIGN.md"));
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "App.xaml"));
        var mainPageSource = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "MainPage.xaml.cs"));

        Assert.Contains("guide_active_border:", design);
        Assert.Contains("AppGuideActiveBorderColor", appXaml);
        Assert.Contains("AppGuideActiveBorderBrush", appXaml);
        Assert.Contains("button.BorderBrush = (Brush)resources[isActive ? \"AppGuideActiveBorderBrush\" : \"AppTransparentBrush\"];", mainPageSource);
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
