using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class LiveTvPageSourceTests
{
    [Fact]
    public void LiveTv_Fixture_Development_Route_Renders_Positive_Channel_Browse_State()
    {
        var mainPageSource = ReadAppSource("MainPage.xaml.cs");
        var requestSource = ReadAppSource("Navigation", "LiveTvNavigationRequest.cs");
        var liveTvPageSource = ReadAppSource("Views", "LiveTvPage.xaml.cs");

        Assert.Contains("case \"livetv-fixture\"", mainPageSource);
        Assert.Contains("UseDevelopmentFixture", requestSource);
        Assert.Contains("RenderDevelopmentLiveTvFixture()", liveTvPageSource);
        Assert.Contains("DevelopmentLiveTvFixture.Create()", liveTvPageSource);
        Assert.Contains("CreateDevelopmentChannelLogoFrame(", liveTvPageSource);
        Assert.Contains("\"Fixture Live TV guide\"", liveTvPageSource);
    }

    [Fact]
    public void LiveTv_Unsupported_Layer_Dismissal_Restores_Invoking_Channel_Focus()
    {
        var liveTvPageSource = ReadAppSource("Views", "LiveTvPage.xaml.cs");

        Assert.Contains("_unsupportedReturnFocusTarget", liveTvPageSource);
        Assert.Contains("_unsupportedReturnFocusTarget = sender as Button;", liveTvPageSource);
        Assert.Contains("FocusUnsupportedReturnTarget()", liveTvPageSource);
        Assert.Contains("_unsupportedReturnFocusTarget.Focus(FocusState.Keyboard)", liveTvPageSource);
    }

    [Fact]
    public void LiveTv_Fixture_Channel_Artwork_Uses_Packaged_Qa_Uris()
    {
        var liveTvPageSource = ReadAppSource("Views", "LiveTvPage.xaml.cs");

        Assert.Contains("DevelopmentLiveTvFixture.ArtworkKey(channel.Id, \"Primary\")", liveTvPageSource);
        Assert.Contains("new BitmapImage(new Uri(imageUri))", liveTvPageSource);
    }

    [Fact]
    public void LiveTv_Page_Handles_Dpad_Channel_List_Movement_Explicitly()
    {
        var liveTvPageSource = ReadAppSource("Views", "LiveTvPage.xaml.cs");

        Assert.Contains("_channelButtons", liveTvPageSource);
        Assert.Contains("TryMoveWithinChannelList(e.Key)", liveTvPageSource);
        Assert.Contains("MusicListFocusPolicy.GetVerticalTargetIndex", liveTvPageSource);
        Assert.Contains("IsDownKey(e.Key)", liveTvPageSource);
        Assert.Contains("IsUpKey(e.Key)", liveTvPageSource);
    }

    [Fact]
    public void LiveTv_Page_Uses_Matte_List_Focus_And_Transient_Unsupported_Layer()
    {
        var appXaml = ReadAppSource("App.xaml");
        var liveTvXaml = ReadAppSource("Views", "LiveTvPage.xaml");
        var liveTvPageSource = ReadAppSource("Views", "LiveTvPage.xaml.cs");

        Assert.Contains("TvTransientMessagePanelStyle", appXaml);
        Assert.Contains("Style=\"{StaticResource TvTransientMessagePanelStyle}\"", liveTvXaml);
        Assert.Contains("Style=\"{StaticResource TvLibraryOptionSheetOptionButtonStyle}\"", liveTvXaml);
        Assert.Contains("MatteButtonFocusVisuals.PrepareListButton(button)", liveTvPageSource);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(UnsupportedCloseButton)", liveTvPageSource);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(FallbackRetryButton)", liveTvPageSource);
        Assert.Contains("UseSystemFocusVisuals\" Value=\"False\"", appXaml);
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
