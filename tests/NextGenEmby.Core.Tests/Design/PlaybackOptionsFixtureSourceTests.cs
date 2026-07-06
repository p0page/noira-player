using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class PlaybackOptionsFixtureSourceTests
{
    [Fact]
    public void Playback_Options_Fixture_Route_Renders_Stream_Choices_Without_Backend_Switches()
    {
        var root = FindRepositoryRoot();
        var mainPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "MainPage.xaml.cs"));
        var playbackPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "PlaybackPage.xaml.cs"));
        var playbackPageXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "PlaybackPage.xaml"));

        Assert.Contains("case \"playback-options-fixture\"", mainPageSource);
        Assert.Contains("PlaybackOptionsFixtureNavigationRequest", mainPageSource);
        Assert.Contains("RenderDevelopmentPlaybackOptionsFixture(", playbackPageSource);
        Assert.Contains("DevelopmentPlaybackOptionsFixture.Create()", playbackPageSource);
        Assert.Contains("SelectDevelopmentMediaSource(option)", playbackPageSource);
        Assert.Contains("SelectDevelopmentAudioStream(option)", playbackPageSource);
        Assert.Contains("SelectDevelopmentSubtitleStream(option)", playbackPageSource);
        Assert.Contains("_usesDevelopmentPlaybackOptionsFixture", playbackPageSource);
        Assert.Contains("FocusDevelopmentPlaybackOptionsDefaultAsync()", playbackPageSource);
        Assert.Equal(3, CountOccurrences(playbackPageXaml, "ProcessKeyboardAccelerators=\"MoreDrawerComboBox_OnProcessKeyboardAccelerators\""));
        Assert.DoesNotContain("KeyDown=\"MoreDrawerComboBox_OnKeyDown\"", playbackPageXaml);
        Assert.Contains("PlaybackOverlayInputPolicy.ShouldRouteMoreDrawerComboBoxDirectionalInput", playbackPageSource);
        Assert.Contains("MoreDrawerComboBox_OnProcessKeyboardAccelerators", playbackPageSource);
        Assert.Contains("_handledMoreDrawerComboBoxDirectionalKey", playbackPageSource);
        Assert.Contains("ShouldIgnoreMoreDrawerComboBoxDirectionalReplay(args.VirtualKey)", playbackPageSource);
        Assert.Contains("FocusMoreDrawerTarget(PlaybackMoreDrawerFocusTarget.Info);", playbackPageSource);
        Assert.Contains("ClearTransportFocusForMoreDrawer();", playbackPageSource);
    }

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
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
