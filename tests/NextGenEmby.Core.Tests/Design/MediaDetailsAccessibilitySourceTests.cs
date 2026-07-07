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
    public void Details_Renders_Metadata_Facet_Chips_That_Open_Library_Browse()
    {
        var root = FindRepositoryRoot();
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));
        var requestSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Navigation",
            "LibraryNavigationRequest.cs"));

        Assert.Contains("MetadataSection", detailsXaml);
        Assert.Contains("MetadataPanel", detailsXaml);
        Assert.Contains("RenderMetadataFacetRail", detailsPageSource);
        Assert.Contains("CreateMetadataFacetButton", detailsPageSource);
        Assert.Contains("MetadataFacet_OnClick", detailsPageSource);
        Assert.Contains("FocusFirstButton(MetadataPanel, FocusState.Keyboard)", detailsPageSource);
        Assert.Contains("new LibraryNavigationQuery(genres:", detailsPageSource);
        Assert.Contains("new LibraryNavigationQuery(studioIds:", detailsPageSource);
        Assert.Contains("new LibraryNavigationQuery(tags:", detailsPageSource);
        Assert.Contains("_restoreMetadataFacetKey", detailsPageSource);
        Assert.Contains("e.NavigationMode == NavigationMode.Back", detailsPageSource);
        Assert.Contains("FocusMetadataFacetWhenReadyAsync()", detailsPageSource);
        Assert.Contains("FocusMetadataFacetByKey", detailsPageSource);
        Assert.Contains("Genres", requestSource);
        Assert.Contains("StudioIds", requestSource);
        Assert.Contains("Tags", requestSource);
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

    [Fact]
    public void Details_Fixture_Default_Focus_Does_Not_Override_Metadata_Restore()
    {
        var root = FindRepositoryRoot();
        var detailsPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("ShouldApplyDevelopmentDefaultFocus(focusGeneration)", detailsPageSource);
        Assert.Contains("string.IsNullOrWhiteSpace(_restoreMetadataFacetKey)", detailsPageSource);
    }

    [Fact]
    public void Details_Metadata_Facet_Restore_Survives_Backstack_Page_Recreation()
    {
        var root = FindRepositoryRoot();
        var detailsPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("s_pendingMetadataFacetRestoreKeys", detailsPageSource);
        Assert.Contains("ResolveMetadataRestoreItemId(e.Parameter)", detailsPageSource);
        Assert.Contains("ConsumeMetadataFacetRestoreKey(restoreItemId)", detailsPageSource);
        Assert.Contains("QueueMetadataFacetRestore(facet)", detailsPageSource);
        Assert.Contains("FocusDefaultContent();", detailsPageSource);
        Assert.Contains("string.IsNullOrWhiteSpace(_restoreMetadataFacetKey)", detailsPageSource);
    }

    [Fact]
    public void Details_Visual_System_Uses_Atmosphere_Zone_Instead_Of_Visible_Poster_Viewer()
    {
        var root = FindRepositoryRoot();
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("DetailsAtmosphereZone", detailsXaml);
        Assert.Contains("AtmosphereImage", detailsXaml);
        Assert.Contains("DetailsAtmosphereFallback", detailsXaml);
        Assert.Contains("ApplyDetailsAtmosphereArtwork", detailsSource);
        Assert.DoesNotContain("x:Name=\"PosterImage\"", detailsXaml);
        Assert.DoesNotContain("x:Name=\"PosterFallbackBlock\"", detailsXaml);
        Assert.DoesNotContain("PosterImage.Source", detailsSource);
    }

    [Fact]
    public void Details_Interactive_Controls_Use_Matte_Focus_Without_System_Focus_Rings()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "App.xaml"));
        var detailsXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml"));
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("TvDetailsPrimaryActionButtonStyle", appXaml);
        Assert.Contains("TvDetailsActionButtonStyle", appXaml);
        Assert.Contains("TvDetailsSourceOptionButtonStyle", appXaml);
        Assert.Contains("TvDetailsAddToSheetOptionButtonStyle", appXaml);
        Assert.Contains("Style=\"{StaticResource TvDetailsPrimaryActionButtonStyle}\"", detailsXaml);
        Assert.Contains("Style=\"{StaticResource TvDetailsActionButtonStyle}\"", detailsXaml);
        Assert.Contains("Style=\"{StaticResource TvDetailsIconActionButtonStyle}\"", detailsXaml);
        Assert.DoesNotContain("UseSystemFocusVisuals=\"True\"", detailsXaml);
        Assert.Contains("UseSystemFocusVisuals = false", detailsSource);
        Assert.Contains("Style = (Style)Application.Current.Resources[\"TvDetailsSourceOptionButtonStyle\"]", detailsSource);
        Assert.Contains("Style = (Style)Application.Current.Resources[\"TvDetailsAddToSheetOptionButtonStyle\"]", detailsSource);
        Assert.Contains("ApplyDetailsCommandFocusTreatment", detailsSource);
    }

    [Fact]
    public void Details_Source_Selection_And_Secondary_Rails_Do_Not_Use_Green_Or_Bright_Frame_Focus()
    {
        var root = FindRepositoryRoot();
        var detailsSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "MediaDetailsPage.xaml.cs"));

        Assert.Contains("AppSourceSelectedBrush", detailsSource);
        Assert.Contains("CreateSecondaryPosterRailButton", detailsSource);
        Assert.Contains("TvPosterGridCardButtonStyle", detailsSource);
        Assert.DoesNotContain("marker.Background = isSelected ? BrushResource(\"AppSecondaryBrush\")", detailsSource);
        Assert.DoesNotContain("BorderBrush = isPreview ? BrushResource(\"AppAccentBrush\") : BrushResource(\"AppHairlineBrush\")", detailsSource);
        Assert.DoesNotContain("UseSystemFocusVisuals = true", detailsSource);
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
