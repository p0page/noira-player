using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class LiveTvPageSourceTests
{
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
    public void LiveTv_Current_Program_Preview_Uses_Media_First_Artwork_When_Available()
    {
        var liveTvXaml = ReadAppSource("Views", "LiveTvPage.xaml");
        var liveTvPageSource = ReadAppSource("Views", "LiveTvPage.xaml.cs");
        var contentColumns = SliceFrom(liveTvXaml, "<Grid Grid.Row=\"1\" ColumnSpacing=\"24\">", "<ScrollViewer");

        Assert.Contains("ColumnDefinition Width=\"1120\"", contentColumns);
        Assert.Contains("ColumnDefinition Width=\"520\"", contentColumns);
        Assert.DoesNotContain("ColumnDefinition Width=\"*\"", contentColumns);
        Assert.DoesNotContain("ColumnDefinition Width=\"4*\"", contentColumns);
        Assert.Contains("PreviewArtworkFrame", liveTvXaml);
        Assert.Contains("PreviewArtworkImage", liveTvXaml);
        Assert.Contains("CornerRadius=\"{StaticResource TvHomeWideCardCornerRadius}\"", liveTvXaml);
        Assert.DoesNotContain("AppCardCornerRadius", liveTvXaml);
        Assert.Contains("UpdatePreviewArtwork(channel)", liveTvPageSource);
        Assert.Contains("CreateProgramArtworkImageSource(channel)", liveTvPageSource);
        Assert.Contains("_liveProgramArtworkUris[channel.Id] = client.GetImageUrl(session, program.Id,", liveTvPageSource);
        Assert.Contains("PreviewArtworkFrame.Visibility = Visibility.Collapsed", liveTvPageSource);
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
        parts[2] = "NoiraPlayer.App";
        Array.Copy(segments, 0, parts, 3, segments.Length);
        return File.ReadAllText(Path.Combine(parts));
    }

    private static string SliceFrom(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing source marker " + startMarker);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, "Missing source marker " + endMarker);
        return source.Substring(start, end - start);
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
