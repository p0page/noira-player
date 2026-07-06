using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class MediaDetailsAccessibilitySourceTests
{
    [Fact]
    public void Details_Fixture_Development_Route_Renders_Below_Fold_Content_And_Add_To_Sheets()
    {
        var root = FindRepositoryRoot();
        var mainPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "MainPage.xaml.cs"));
        var requestSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Navigation",
            "MediaDetailsNavigationRequest.cs"));
        var detailsPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("case \"details-fixture\"", mainPageSource);
        Assert.Contains("UseDevelopmentFixture", requestSource);
        Assert.Contains("RenderDevelopmentDetailsFixture(", detailsPageSource);
        Assert.Contains("DevelopmentDetailsFixture.Create()", detailsPageSource);
        Assert.Contains("CreateDevelopmentArtworkBrush", detailsPageSource);
        Assert.Contains("OpenDevelopmentAddToSheet(", detailsPageSource);
        Assert.Contains("ConfirmDevelopmentAddToSheet()", detailsPageSource);
        Assert.Contains("FocusDevelopmentDefaultContentAsync()", detailsPageSource);
        Assert.Contains("pageType == typeof(MediaDetailsPage)", mainPageSource);
        Assert.Contains("ShellContentMode.MediaDetails", mainPageSource);
    }

    [Fact]
    public void Details_Selected_Version_State_Does_Not_ReUse_Focus_Border_Color()
    {
        var root = FindRepositoryRoot();
        var detailsPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("SourceSelectionMarker", detailsPageSource);
        Assert.DoesNotContain("isSelected ? \"AppAccentBrush\" : \"AppHairlineBrush\"", detailsPageSource);
        Assert.Contains("button.BorderBrush = (Brush)Application.Current.Resources[\"AppHairlineBrush\"];", detailsPageSource);
    }

    [Fact]
    public void Details_Fixture_User_Data_Toggles_Use_Local_State_Instead_Of_Live_Api()
    {
        var root = FindRepositoryRoot();
        var detailsPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("ApplyDevelopmentFixtureUserDataToggle(", detailsPageSource);
        Assert.Contains("MediaDetailsUserDataTogglePolicy.ToggleFavorite", detailsPageSource);
        Assert.Contains("MediaDetailsUserDataTogglePolicy.TogglePlayed", detailsPageSource);
        Assert.Contains("if (_usesDevelopmentDetailsFixture)", detailsPageSource);
        Assert.Contains("Fixture favorite added.", detailsPageSource);
        Assert.Contains("Fixture marked watched.", detailsPageSource);
    }

    [Fact]
    public void Details_Fixture_Default_Focus_Uses_Low_Priority_Retry()
    {
        var root = FindRepositoryRoot();
        var detailsPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("DevelopmentDetailsFocusRetryCount", detailsPageSource);
        Assert.Contains("CoreDispatcherPriority.Low", detailsPageSource);
        Assert.Contains("Task.Delay(120)", detailsPageSource);
        Assert.Contains("FocusDefaultContent();", detailsPageSource);
        Assert.DoesNotContain("CoreDispatcherPriority.Normal", detailsPageSource);
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
