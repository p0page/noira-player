using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class MusicPageSourceTests
{
    [Fact]
    public void Music_Fixture_Development_Route_Renders_Positive_Browse_State()
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
            "MusicNavigationRequest.cs"));
        var musicPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
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
            "NextGenEmby.App",
            "Views",
            "MusicPage.xaml"));
        var musicPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
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
            "NextGenEmby.App",
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
            "NextGenEmby.App",
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
            "NextGenEmby.App",
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
