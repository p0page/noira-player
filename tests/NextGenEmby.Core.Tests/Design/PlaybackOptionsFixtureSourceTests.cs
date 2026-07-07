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

    [Fact]
    public void Playback_Osd_Uses_Compact_Status_Capsule_And_Transport_Strip()
    {
        var root = FindRepositoryRoot();
        var playbackPageXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "PlaybackPage.xaml"));

        Assert.Contains("x:Name=\"PlaybackStatusCapsule\"", playbackPageXaml);
        Assert.Contains("x:Name=\"PlaybackTransportStrip\"", playbackPageXaml);
        Assert.Contains("x:Name=\"PlaybackTimelineRow\"", playbackPageXaml);
        Assert.Contains("x:Name=\"TransportControlsPanel\"", playbackPageXaml);
        Assert.Contains("x:Name=\"CurrentTimeBlock\"", playbackPageXaml);
        Assert.Contains("x:Name=\"DurationBlock\"", playbackPageXaml);
        Assert.DoesNotContain("Padding=\"40,22\"", playbackPageXaml);
    }

    [Fact]
    public void Playback_Transport_And_Menu_Use_Matte_Focus_Without_Bright_Frames()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "App.xaml"));
        var playbackPageXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "PlaybackPage.xaml"));
        var playbackPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "PlaybackPage.xaml.cs"));

        Assert.Contains("TvPlaybackTransportButtonStyle", appXaml);
        Assert.Contains("TvPlaybackPrimaryTransportButtonStyle", appXaml);
        Assert.Contains("TvPlaybackMoreMenuButtonStyle", appXaml);
        Assert.Contains("TvPlaybackOptionComboBoxStyle", appXaml);
        Assert.Contains("FocusVisualPrimaryBrush\" Value=\"{StaticResource AppFocusedCardFillBrush}", appXaml);
        Assert.Contains("FocusVisualSecondaryBrush\" Value=\"{StaticResource AppTransparentBrush}", appXaml);
        Assert.Contains("Style=\"{StaticResource TvPlaybackTransportButtonStyle}\"", playbackPageXaml);
        Assert.Contains("Style=\"{StaticResource TvPlaybackPrimaryTransportButtonStyle}\"", playbackPageXaml);
        Assert.Contains("Style=\"{StaticResource TvPlaybackMoreMenuButtonStyle}\"", playbackPageXaml);
        Assert.Contains("Style=\"{StaticResource TvPlaybackOptionComboBoxStyle}\"", playbackPageXaml);
        Assert.DoesNotContain("resources[isFocused ? \"AppAccentBrush\" : \"AppHairlineBrush\"]", playbackPageSource);
        Assert.Contains("resources[\"AppTransparentBrush\"]", playbackPageSource);
        Assert.Contains("control is ComboBox ? new Thickness(0) : new Thickness(1)", playbackPageSource);
        Assert.Contains("resources[isFocused ? \"AppFocusedCardFillBrush\" : \"AppChromeBrush\"]", playbackPageSource);
    }

    [Fact]
    public void Playback_More_Uses_Compact_Menu_Instead_Of_Full_Height_Drawer()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "App.xaml"));
        var playbackPageXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "PlaybackPage.xaml"));

        Assert.Contains("TvPlaybackMoreMenuWidth", appXaml);
        Assert.Contains("TvPlaybackMoreMenuMaxHeight", appXaml);
        Assert.Contains("x:Name=\"MoreMenuPanel\"", playbackPageXaml);
        Assert.Contains("MaxHeight=\"{StaticResource TvPlaybackMoreMenuMaxHeight}\"", playbackPageXaml);
        Assert.Contains("VerticalAlignment=\"Bottom\"", playbackPageXaml);
        Assert.Contains("Margin=\"{StaticResource TvPlaybackMoreMenuMargin}\"", playbackPageXaml);
        Assert.Contains("x:Name=\"SubtitleSafeSampleBlock\"", playbackPageXaml);
        Assert.DoesNotContain("Width=\"370\"", playbackPageXaml);
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
