using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class LibraryPageSourceTests
{
    [Fact]
    public void Library_Navigation_Request_Can_Carry_Development_Fixture_Items()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Navigation",
            "LibraryNavigationRequest.cs"));

        Assert.Contains("WithDevelopmentFixture", source);
        Assert.Contains("DevelopmentItems", source);
        Assert.Contains("DevelopmentArtworkUris", source);
    }

    [Fact]
    public void Library_Page_Renders_Development_Fixture_Items_Without_Session()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Views",
            "LibraryPage.xaml.cs"));

        Assert.Contains("request.DevelopmentItems.Count > 0", source);
        Assert.Contains("CreateDevelopmentGridItems", source);
        Assert.Contains("DevelopmentHomeFixture.ArtworkKey", source);
    }

    [Fact]
    public void Library_Page_Filters_Development_Items_By_Parent_For_Nested_Folders()
    {
        var source = ReadAppSource("Views", "LibraryPage.xaml.cs");

        Assert.Contains("SelectDevelopmentItemsForRequest", source);
        Assert.Contains("item.ParentId", source);
        Assert.Contains("request.ParentId", source);
    }

    [Fact]
    public void Library_Page_Opens_Folders_As_Nested_Libraries()
    {
        var source = ReadAppSource("Views", "LibraryPage.xaml.cs");

        Assert.Contains("LibraryItemActivationRoute.BrowseFolder", source);
        Assert.Contains("CreateFolderNavigationRequest", source);
        Assert.Contains("Frame.Navigate(typeof(LibraryPage)", source);
    }

    [Fact]
    public void Library_Page_Opens_Collections_And_Playlists_With_Media_Child_Query()
    {
        var source = ReadAppSource("Views", "LibraryPage.xaml.cs");

        Assert.Contains("IsOrganizationContainer", source);
        Assert.Contains("OrganizationChildItemTypes", source);
        Assert.Contains("LibraryNavigationQuery.Empty", source);
    }

    [Fact]
    public void Library_Page_Loads_Playlist_Children_From_Playlist_Items_Endpoint()
    {
        var source = ReadAppSource("Views", "LibraryPage.xaml.cs");
        var requestSource = ReadAppSource("Navigation", "LibraryNavigationRequest.cs");

        Assert.Contains("ContainerItemType", requestSource);
        Assert.Contains("item.Type", source);
        Assert.Contains("IsPlaylistRequest(request)", source);
        Assert.Contains("client.GetPlaylistItemsAsync(session, request.ParentId, 100)", source);
    }

    [Fact]
    public void Library_Page_Hides_Sort_Filter_For_Read_Only_Sequence_Requests()
    {
        var source = ReadAppSource("Views", "LibraryPage.xaml.cs");

        Assert.Contains("IsReadOnlySequenceRequest(_request)", source);
        Assert.Contains("IsReadOnlySequenceRequest(request)", source);
        Assert.Contains("return IsSectionRequest(request) || IsPlaylistRequest(request);", source);
        Assert.Contains("LibraryToolbarFocusPolicy.Move(current, direction, IsReadOnlySequenceRequest(_request))", source);
    }

    [Fact]
    public void Library_Option_Sheet_Uses_Matte_Command_Structure()
    {
        var appXaml = ReadAppSource("App.xaml");
        var pageXaml = ReadAppSource("Views", "LibraryPage.xaml");
        var source = ReadAppSource("Views", "LibraryPage.xaml.cs");

        Assert.Contains("x:Key=\"TvCommandButtonStyle\"", appXaml);
        Assert.Contains("x:Key=\"TvLibraryOptionSheetOptionButtonStyle\"", appXaml);
        Assert.Contains("x:Key=\"TvLibraryOptionSheetWidth\"", appXaml);
        Assert.Contains("x:Key=\"TvLibraryOptionSheetMargin\"", appXaml);
        Assert.Contains("Style=\"{StaticResource TvCommandButtonStyle}\"", pageXaml);
        Assert.Contains("Width=\"{StaticResource TvLibraryOptionSheetWidth}\"", pageXaml);
        Assert.Contains("Margin=\"{StaticResource TvLibraryOptionSheetMargin}\"", pageXaml);
        Assert.Contains("VerticalAlignment=\"Top\"", pageXaml);
        Assert.DoesNotContain("VerticalAlignment=\"Bottom\"", pageXaml);
        Assert.Contains("Style = (Style)Application.Current.Resources[\"TvLibraryOptionSheetOptionButtonStyle\"]", source);
        Assert.Contains("button.UseSystemFocusVisuals = false;", source);
        Assert.Contains("isPreview ? BrushResource(\"AppFocusedCardFillBrush\") : BrushResource(\"AppChromeBrush\")", source);
        Assert.DoesNotContain("isPreview ? BrushResource(\"AppAccentBrush\") : BrushResource(\"AppHairlineBrush\")", source);
    }

    [Fact]
    public void Library_Page_Passes_Development_Photo_Uri_To_Photo_Viewer()
    {
        var source = ReadAppSource("Views", "LibraryPage.xaml.cs");
        var requestSource = ReadAppSource("Navigation", "PhotoViewerNavigationRequest.cs");

        Assert.Contains("ResolveDevelopmentPhotoUri", source);
        Assert.Contains("new PhotoViewerNavigationRequest(item.Id, itemName, developmentImageUri)", source);
        Assert.Contains("DevelopmentImageUri", requestSource);
    }

    [Fact]
    public void Library_Page_Restores_Focus_To_Activated_Item_When_Returning_From_Child_Page()
    {
        var source = ReadAppSource("Views", "LibraryPage.xaml.cs");
        var requestSource = ReadAppSource("Navigation", "LibraryNavigationRequest.cs");

        Assert.Contains("RestoreFocusItemId", requestSource);
        Assert.Contains("request.RestoreFocusItemId = item.Id;", source);
        Assert.Contains("preferredFocusItemId = request.RestoreFocusItemId", source);
        Assert.Contains("FocusPreferredItemAsync(loadGeneration, preferredFocusItemId)", source);
        Assert.Contains("FocusItemByIdNow(preferredFocusItemId, FocusState.Keyboard)", source);
        Assert.Contains("FindGridItemIndexById", source);
    }

    [Fact]
    public void MainPage_Provides_Positive_Collections_And_Playlists_Fixture_Routes()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "MainPage.xaml.cs"));

        Assert.Contains("case \"collections-fixture\":", source);
        Assert.Contains("case \"playlists-fixture\":", source);
        Assert.Contains("DevelopmentLibraryOrganizationFixture.Create()", source);
        Assert.Contains("fixture.Items", source);
        Assert.Contains("fixture.ArtworkUris", source);
    }

    private static string ReadAppSource(params string[] segments)
    {
        var parts = new string[segments.Length + 3];
        parts[0] = FindRepositoryRoot();
        parts[1] = "src";
        parts[2] = "NextGenEmby.App";
        Array.Copy(segments, 0, parts, 3, segments.Length);
        return File.ReadAllText(Path.Combine(parts));
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
