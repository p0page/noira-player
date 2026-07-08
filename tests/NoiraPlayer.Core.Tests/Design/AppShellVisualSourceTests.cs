using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class AppShellVisualSourceTests
{
    [Fact]
    public void A3_Desktop_Title_Bar_Uses_Dark_Room_Chrome()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
            "App.xaml.cs"));

        Assert.Contains("ApplyA3TitleBarTreatment()", source);
        Assert.Contains("ApplicationView.GetForCurrentView().TitleBar", source);
        Assert.Contains("Color.FromArgb(255, 5, 7, 10)", source);
        Assert.Contains("Color.FromArgb(255, 238, 243, 246)", source);
        Assert.DoesNotContain("Colors.White", source);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "DESIGN.md")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.App", "App.xaml.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
