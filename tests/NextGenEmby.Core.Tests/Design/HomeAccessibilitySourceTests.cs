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
    public void Home_Xaml_Renders_Server_Sections_As_Dedicated_Rail()
    {
        var xaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Views",
            "HomePage.xaml"));

        Assert.Contains("x:Name=\"HomeSectionsRail\"", xaml);
        Assert.Contains("x:Name=\"HomeSectionsPanel\"", xaml);
        Assert.Contains("Text=\"Server sections\"", xaml);
    }

    [Fact]
    public void Home_Source_Tracks_Server_Section_Focus_Separately()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Views",
            "HomePage.xaml.cs"));

        Assert.Contains("private readonly List<Button> _sectionButtons", source);
        Assert.Contains("sectionCount: _sectionButtons.Count", source);
        Assert.Contains("HomeFocusZone.Section", source);
        Assert.Contains("HomeSectionsPanel.Children.Clear()", source);
    }

    [Fact]
    public void Home_Fixture_Section_Requests_Carry_Development_Items_To_Library()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Views",
            "HomePage.xaml.cs"));

        Assert.Contains(".WithDevelopmentFixture(row.Items, _developmentArtworkUris)", source);
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
        Assert.Contains("CreateArtworkBrush(EmbyArtworkPolicy.SelectHomeSectionWideArtwork(row.Section, maxWidth))", source);
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
