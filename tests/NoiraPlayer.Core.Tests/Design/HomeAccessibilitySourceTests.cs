using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class HomeAccessibilitySourceTests
{
    [Fact]
    public void Home_Card_Focus_Uses_Matte_Treatment_Instead_Of_System_Ring()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "App.xaml"));
        var source = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "HomePage.xaml.cs"));

        Assert.Contains("AppFocusedCardFillColor", appXaml);
        Assert.Contains("AppFocusedCardFillBrush", appXaml);
        Assert.Contains("button.UseSystemFocusVisuals = false;", source);
        Assert.Contains("ApplyHomeCardMatteFocus(button, isFocused: true)", source);
        Assert.Contains("ApplyHomeCardMatteFocus(button, isFocused: false)", source);
        Assert.DoesNotContain("UseSystemFocusVisuals = true", source);
        Assert.DoesNotContain("SystemControlFocusVisualPrimaryBrush", source);
    }

    [Fact]
    public void Resume_Rows_Render_Wide_Cards_With_Progress_And_Bottom_Scrim()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "App.xaml"));
        var source = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "HomePage.xaml.cs"));

        Assert.Contains("TvHomeResumeCardWidth", appXaml);
        Assert.Contains("TvHomeResumeCardHeight", appXaml);
        Assert.Contains("HomeRowVisualKind.Resume", source);
        Assert.Contains("AddUniqueRow(renderedRowTitles, \"Continue watching\", continueItems, null, HomeRowVisualKind.Resume)", source);
        Assert.Contains("AddUniqueRow(renderedRowTitles, \"Next up\", nextUpItems, null, HomeRowVisualKind.Resume)", source);
        Assert.Contains("CreateResumeItemButton(item)", source);
        Assert.Contains("EmbyArtworkPolicy.SelectItemWideArtwork(item, 760)", source);
        Assert.Contains("CreateResumeProgressBar(item)", source);
    }

    [Fact]
    public void Passive_Home_Section_Chrome_Does_Not_Use_Green_Decorative_Accents()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
            "Views",
            "HomePage.xaml.cs"));

        var sectionStart = source.IndexOf("private Button CreateHomeSectionButton", StringComparison.Ordinal);
        var scrimStart = source.IndexOf("private static Border CreateHomeWideCardTextScrim", StringComparison.Ordinal);

        Assert.True(sectionStart >= 0);
        Assert.True(scrimStart > sectionStart);
        var sectionSource = source.Substring(sectionStart, scrimStart - sectionStart);

        Assert.DoesNotContain("AppWarmBrush", sectionSource);
        Assert.DoesNotContain("TvHomeWideCardSideAccentWidth", sectionSource);
        Assert.DoesNotContain("TvHomeWideCardAccentHeight", sectionSource);
    }

    [Fact]
    public void Dynamic_Home_Buttons_Set_Automation_Names()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
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
            "NoiraPlayer.App",
            "Views",
            "HomePage.xaml"));

        Assert.Contains("AutomationProperties.Name=\"Play\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"Details\"", xaml);
    }

    [Fact]
    public void Home_Top_Decision_Surface_Is_Compact_And_Unframed()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "App.xaml"));
        var homeXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "HomePage.xaml"));

        Assert.Contains("<x:Double x:Key=\"TvHomeHeroHeight\">196</x:Double>", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeHeroContentSpacing\">8</x:Double>", appXaml);
        Assert.Contains("x:Name=\"HomeFeatureStrip\"", homeXaml);
        Assert.Contains("Background=\"{StaticResource AppTransparentBrush}\"", homeXaml);
        Assert.DoesNotContain("TvHomeHeroAccentWidth", appXaml);
        Assert.DoesNotContain("TvHomeHeroAccentMargin", appXaml);
        Assert.DoesNotContain("TvHomeHeroAccentCornerRadius", appXaml);
        Assert.DoesNotContain("TvHomeHeroAccentWidth", homeXaml);
        Assert.DoesNotContain("TvHomeHeroAccentMargin", homeXaml);
        Assert.DoesNotContain("Background=\"{StaticResource AppAccentBrush}\"", homeXaml);
    }

    [Fact]
    public void Home_Xaml_Renders_Server_Sections_As_Dedicated_Rail()
    {
        var xaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
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
            "NoiraPlayer.App",
            "Views",
            "HomePage.xaml.cs"));

        Assert.Contains("private readonly List<Button> _sectionButtons", source);
        Assert.Contains("sectionCount: _sectionButtons.Count", source);
        Assert.Contains("HomeFocusZone.Section", source);
        Assert.Contains("HomeSectionsPanel.Children.Clear()", source);
    }

    [Fact]
    public void Home_Server_Section_Artwork_Does_Not_Use_Obsolete_Development_State()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
            "Views",
            "HomePage.xaml.cs"));

        Assert.DoesNotContain("if (_client == null || _session == null || view == null", source);
        Assert.DoesNotContain("if (_client == null || _session == null || row == null", source);
        Assert.Contains("CreateArtworkBrush(EmbyArtworkPolicy.SelectLibraryWideArtwork(view, maxWidth))", source);
        Assert.Contains("CreateArtworkBrush(EmbyArtworkPolicy.SelectHomeSectionWideArtwork(row.Section, maxWidth))", source);
    }

    [Fact]
    public void A3_Passive_Home_Media_Cards_Do_Not_Use_Hairline_Structure()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
            "Views",
            "HomePage.xaml.cs"));

        var libraryCardSource = ExtractSourceBlock(source, "private Button CreateLibraryButton", "private Button CreateHomeSectionButton");
        var sectionCardSource = ExtractSourceBlock(source, "private Button CreateHomeSectionButton", "private static Border CreateHomeWideCardTextScrim");
        var resumeCardSource = ExtractSourceBlock(source, "private Button CreateResumeItemButton", "private static Border CreateResumeCardTextScrim");
        var posterCardSource = ExtractSourceBlock(source, "private Button CreateItemButton", "private async Task<IReadOnlyList<T>> TryLoadListAsync");

        Assert.DoesNotContain("AppHairlineBrush", libraryCardSource);
        Assert.DoesNotContain("AppHairlineBrush", sectionCardSource);
        Assert.DoesNotContain("AppHairlineBrush", resumeCardSource);
        Assert.DoesNotContain("AppHairlineBrush", posterCardSource);
        Assert.Contains("CreateHomeCardFocusChrome", libraryCardSource);
        Assert.Contains("CreateHomeCardFocusChrome", sectionCardSource);
        Assert.Contains("CreateHomeCardFocusChrome", resumeCardSource);
        Assert.Contains("CreateHomeCardFocusChrome", posterCardSource);
    }

    [Fact]
    public void A3_Home_Header_And_Refresh_Are_Subordinate_To_Media()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "App.xaml"));
        var homeXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "HomePage.xaml"));

        var titleStyle = ExtractSourceBlock(appXaml, "<Style x:Key=\"TvPageTitleTextStyle\"", "<Style x:Key=\"TvPageSubtitleTextStyle\"");
        var headerBlock = ExtractSourceBlock(homeXaml, "x:Name=\"HomeChromeHeader\"", "x:Name=\"HeroBackdropImage\"");

        Assert.Contains("<Setter Property=\"FontSize\" Value=\"28\" />", titleStyle);
        Assert.DoesNotContain("<Setter Property=\"FontSize\" Value=\"34\" />", titleStyle);
        Assert.Contains("<x:Double x:Key=\"TvHomeHeaderChromeOpacity\">0.56</x:Double>", appXaml);
        Assert.Contains("Opacity=\"{StaticResource TvHomeHeaderChromeOpacity}\"", headerBlock);
        Assert.Contains("Visibility=\"Collapsed\"", headerBlock);
        Assert.Contains("Opacity=\"0.48\"", headerBlock);
        Assert.DoesNotContain("Opacity=\"0.72\"", headerBlock);
        Assert.DoesNotContain("UseSystemFocusVisuals=\"True\"", homeXaml);
    }

    private static string ExtractSourceBlock(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, StringComparison.Ordinal);

        Assert.True(start >= 0, "Start marker not found: " + startMarker);
        Assert.True(end > start, "End marker not found after start marker: " + endMarker);

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
