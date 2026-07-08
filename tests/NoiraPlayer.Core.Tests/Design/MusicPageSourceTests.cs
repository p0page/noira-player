using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class MusicPageSourceTests
{
    [Fact]
    public void Music_Fixture_Development_Route_Renders_Positive_Browse_State()
    {
        var root = FindRepositoryRoot();
        var mainPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "MainPage.xaml.cs"));
        var requestSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Navigation",
            "MusicNavigationRequest.cs"));
        var musicPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MusicPage.xaml.cs"));

        Assert.Contains("case \"music-fixture\"", mainPageSource);
        Assert.Contains("UseDevelopmentFixture", requestSource);
        Assert.Contains("RenderDevelopmentMusicFixture()", musicPageSource);
        Assert.Contains("DevelopmentMusicFixture.Create()", musicPageSource);
        Assert.Contains("CreateDevelopmentArtworkFrame(", musicPageSource);
        Assert.Contains("FocusDevelopmentDefaultContentAsync()", musicPageSource);
        Assert.Contains("\"Fixture music library\"", musicPageSource);
    }

    [Fact]
    public void Music_Page_Renders_Artist_Hierarchy_For_Tv_Browsing()
    {
        var root = FindRepositoryRoot();
        var musicPageXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MusicPage.xaml"));
        var musicPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MusicPage.xaml.cs"));

        Assert.Contains("ArtistsPanel", musicPageXaml);
        Assert.Contains("ArtistsCountBlock", musicPageXaml);
        Assert.Contains("_artistButtons", musicPageSource);
        Assert.Contains("RenderArtists(", musicPageSource);
        Assert.Contains("CreateArtistItems(", musicPageSource);
        Assert.Contains("ArtistButton_OnClick", musicPageSource);
        Assert.Contains("ItemMatchesArtist(", musicPageSource);
        Assert.Contains("_activeArtistButton", musicPageSource);
        Assert.Contains("FocusActiveArtistOrFirst()", musicPageSource);
    }

    [Fact]
    public void Music_Page_Uses_Shared_Tv_List_Sizing_And_Browse_Only_Unsupported_Layer()
    {
        var musicPageSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
            "Views",
            "MusicPage.xaml.cs"));

        Assert.Contains("TvListButtonStyle", musicPageSource);
        Assert.Contains("TvListArtworkSize", musicPageSource);
        Assert.Contains("TvCompactArtworkSize", musicPageSource);
        Assert.Contains("TransientLayerInputPolicy.ShouldDismiss", musicPageSource);
        Assert.Contains("ShowUnsupportedPanel(songName)", musicPageSource);
    }

    [Fact]
    public void Music_Unsupported_Layer_Dismissal_Restores_Invoking_Song_Focus()
    {
        var musicPageSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
            "Views",
            "MusicPage.xaml.cs"));

        Assert.Contains("_unsupportedReturnFocusTarget", musicPageSource);
        Assert.Contains("_unsupportedReturnFocusTarget = sender as Button;", musicPageSource);
        Assert.Contains("FocusUnsupportedReturnTarget()", musicPageSource);
        Assert.Contains("_unsupportedReturnFocusTarget.Focus(FocusState.Keyboard)", musicPageSource);
    }

    [Fact]
    public void Music_Page_Handles_Dpad_List_Movement_Explicitly()
    {
        var musicPageSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
            "Views",
            "MusicPage.xaml.cs"));

        Assert.Contains("_albumButtons", musicPageSource);
        Assert.Contains("_songButtons", musicPageSource);
        Assert.Contains("_artistButtons", musicPageSource);
        Assert.Contains("TryMoveWithinMusicLists(e.Key)", musicPageSource);
        Assert.Contains("MusicListFocusPolicy.GetVerticalTargetIndex", musicPageSource);
        Assert.Contains("IsRightKey(e.Key)", musicPageSource);
        Assert.Contains("IsLeftKey(e.Key)", musicPageSource);
        Assert.Contains("_artistButtons.Contains(focusedButton)", musicPageSource);
        Assert.Contains("_albumButtons.Contains(focusedButton)", musicPageSource);
    }

    [Fact]
    public void Music_Page_Uses_Matte_List_Focus_And_Transient_Unsupported_Layer()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "App.xaml"));
        var musicPageXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MusicPage.xaml"));
        var musicPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MusicPage.xaml.cs"));

        Assert.Contains("TvTransientMessagePanelStyle", appXaml);
        Assert.Contains("Style=\"{StaticResource TvTransientMessagePanelStyle}\"", musicPageXaml);
        Assert.Contains("Style=\"{StaticResource TvLibraryOptionSheetOptionButtonStyle}\"", musicPageXaml);
        Assert.Contains("MatteButtonFocusVisuals.PrepareListButton(button)", musicPageSource);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(AllSongsButton)", musicPageSource);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(UnsupportedCloseButton)", musicPageSource);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(FallbackRetryButton)", musicPageSource);
    }

    [Fact]
    public void Music_Preview_Uses_Artwork_Context_When_Item_Artwork_Is_Available()
    {
        var root = FindRepositoryRoot();
        var musicPageXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MusicPage.xaml"));
        var musicPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "MusicPage.xaml.cs"));

        Assert.Contains("PreviewArtworkFrame", musicPageXaml);
        Assert.Contains("PreviewArtworkImage", musicPageXaml);
        Assert.Contains("ColumnDefinition Width=\"380\"", musicPageXaml);
        Assert.Contains("ColumnDefinition Width=\"400\"", musicPageXaml);
        Assert.Contains("ColumnDefinition Width=\"440\"", musicPageXaml);
        Assert.Contains("ColumnDefinition Width=\"260\"", musicPageXaml);
        Assert.DoesNotContain("ColumnDefinition Width=\"5*\"", musicPageXaml);
        Assert.Contains("Width=\"220\"", musicPageXaml);
        Assert.Contains("Height=\"220\"", musicPageXaml);
        Assert.Contains("UpdatePreviewArtwork(item)", musicPageSource);
        Assert.Contains("ClearPreviewArtwork()", musicPageSource);
        Assert.Contains("CreatePreviewArtworkImageSource(item)", musicPageSource);
        Assert.Contains("DevelopmentMusicFixture.ArtworkKey(item.Id, \"Primary\")", musicPageSource);
        Assert.Contains("_musicArtworkUris[item.Id] = imageUri.AbsoluteUri", musicPageSource);
        Assert.Contains("PreviewArtworkFrame.Visibility = Visibility.Collapsed", musicPageSource);
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
