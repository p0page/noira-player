using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class HomeAccessibilitySourceTests
{
    [Fact]
    public void Dynamic_Home_Buttons_Set_Automation_Names()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Views",
            "HomePage.xaml.cs"));

        Assert.Contains("AutomationProperties.SetName(button, string.IsNullOrWhiteSpace(view.Name)", source);
        Assert.Contains("AutomationProperties.SetName(button, row.Title)", source);
        Assert.Contains("AutomationProperties.SetName(moreButton, \"More \" + title)", source);
        Assert.Contains("AutomationProperties.SetName(button, string.IsNullOrWhiteSpace(item.Name)", source);
    }

    [Fact]
    public void Hero_Action_Buttons_Set_Automation_Names()
    {
        var xaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Views",
            "HomePage.xaml"));

        Assert.Contains("AutomationProperties.Name=\"Play\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"Details\"", xaml);
    }

    [Fact]
    public void Home_Fixture_Artwork_Does_Not_Require_Live_Client_Session()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Views",
            "HomePage.xaml.cs"));

        Assert.DoesNotContain("if (_client == null || _session == null || view == null", source);
        Assert.DoesNotContain("if (_client == null || _session == null || row == null", source);
        Assert.Contains("CreateArtworkBrush(EmbyArtworkPolicy.SelectLibraryWideArtwork(view, maxWidth))", source);
        Assert.Contains("CreateArtworkBrush(EmbyArtworkPolicy.SelectHomeSectionWideArtwork(new EmbyHomeSection", source);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "Generate-AppIconAssets.ps1")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
