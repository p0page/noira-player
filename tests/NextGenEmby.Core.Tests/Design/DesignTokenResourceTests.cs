using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class DesignTokenResourceTests
{
    private static readonly (string AppKey, string DesignKey)[] RuntimeColorMappings =
    {
        ("AppBackgroundColor", "canvas"),
        ("AppCanvasAltColor", "canvas_alt"),
        ("AppSurfaceColor", "surface"),
        ("AppRaisedSurfaceColor", "surface_raised"),
        ("AppChromeColor", "surface_overlay"),
        ("AppShellRailColor", "shell_rail"),
        ("AppGuideActiveBorderColor", "guide_active_border"),
        ("AppImmersiveScrimColor", "immersive_scrim"),
        ("AppImmersiveControlColor", "surface_overlay"),
        ("AppChromeHoverColor", "chrome_hover"),
        ("AppChromePressedColor", "chrome_pressed"),
        ("AppAccentColor", "focus"),
        ("AppFocusSecondaryColor", "focus_secondary"),
        ("AppActionColor", "primary"),
        ("AppMutedTextColor", "text_muted"),
        ("AppTextColor", "text"),
        ("AppTextSubtleColor", "text_subtle"),
        ("AppHairlineColor", "hairline"),
        ("AppWarmColor", "secondary"),
        ("AppTertiaryColor", "tertiary"),
        ("AppDangerColor", "danger"),
        ("AppCardScrimColor", "scrim"),
        ("AppOnActionColor", "on_primary"),
        ("AppOnSecondaryColor", "on_secondary"),
        ("AppHeroBackdropWashColor", "scrim"),
        ("AppHeroGradientStartColor", "hero_gradient_start"),
        ("AppHeroGradientMidColor", "scrim"),
        ("AppHeroGradientEndColor", "hero_gradient_end"),
        ("AppLibraryArtworkWashColor", "library_artwork_wash"),
        ("AppSectionArtworkWashColor", "section_artwork_wash"),
        ("AppArtworkDimColor", "artwork_dim"),
        ("AppHeroPosterDimColor", "hero_poster_dim"),
        ("AppDetailsBackdropWashColor", "surface_overlay"),
        ("AppModalScrimColor", "modal_scrim"),
        ("AppPlaybackCanvasColor", "canvas"),
        ("AppPlaybackOverlayColor", "surface_overlay"),
        ("AppPlaybackDrawerColor", "playback_drawer"),
        ("AppTransparentColor", "transparent"),
        ("ButtonBackgroundDisabledColor", "button_disabled_background"),
        ("ButtonForegroundDisabledColor", "button_disabled_foreground"),
        ("ButtonBorderPointerOverColor", "button_hover_border"),
        ("ButtonBorderDisabledColor", "button_disabled_border"),
    };

    [Fact]
    public void Playback_Resources_ReUse_Design_Canvas_And_Surface_Family()
    {
        var root = FindRepositoryRoot();
        var designColors = ReadDesignColors(Path.Combine(root, "docs", "DESIGN.md"));
        var appColors = ReadAppColors(Path.Combine(root, "src", "NextGenEmby.App", "App.xaml"));

        Assert.Equal(designColors["canvas"], OpaqueRgb(appColors["AppBackgroundColor"]));
        Assert.Equal(designColors["surface"], OpaqueRgb(appColors["AppSurfaceColor"]));
        Assert.Equal(designColors["canvas"], OpaqueRgb(appColors["AppPlaybackCanvasColor"]));
        Assert.Equal(designColors["surface"], OpaqueRgb(appColors["AppPlaybackOverlayColor"]));
        Assert.Equal(designColors["surface"], OpaqueRgb(appColors["AppPlaybackDrawerColor"]));
    }

    [Fact]
    public void App_Runtime_Colors_Are_Backed_By_Design_Tokens()
    {
        var root = FindRepositoryRoot();
        var designColors = ReadDesignColors(Path.Combine(root, "docs", "DESIGN.md"));
        var appColors = ReadAppColors(Path.Combine(root, "src", "NextGenEmby.App", "App.xaml"));

        foreach (var mapping in RuntimeColorMappings)
        {
            Assert.True(
                designColors.ContainsKey(mapping.DesignKey),
                $"DESIGN.md is missing colors.{mapping.DesignKey} for {mapping.AppKey}.");
            Assert.True(
                appColors.ContainsKey(mapping.AppKey),
                $"App.xaml is missing {mapping.AppKey} for colors.{mapping.DesignKey}.");
            Assert.Equal(designColors[mapping.DesignKey], NormalizeColor(appColors[mapping.AppKey]));
        }
    }

    [Fact]
    public void View_CodeBehind_Does_Not_Create_Page_Local_Raw_Color_Brushes()
    {
        var root = FindRepositoryRoot();
        var viewsPath = Path.Combine(root, "src", "NextGenEmby.App", "Views");
        var offenders = Directory
            .EnumerateFiles(viewsPath, "*.xaml.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    LineNumber = index + 1,
                    Line = line
                }))
            .Where(entry => entry.Line.Contains("new SolidColorBrush(Color.FromArgb", StringComparison.Ordinal))
            .Select(entry => $"{Path.GetRelativePath(root, entry.Path)}:{entry.LineNumber}: {entry.Line.Trim()}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "View code-behind should consume App.xaml brushes instead of page-local raw colors:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Home_Wide_Card_Artwork_Treatment_Uses_Theme_Resources()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "App.xaml"));
        var homeSource = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "Views", "HomePage.xaml.cs"));

        Assert.Contains("<x:Double x:Key=\"TvHomeLibraryArtworkOpacity\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeSectionArtworkOpacity\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeWideCardTextScrimHeight\">", appXaml);
        Assert.Contains("GetDoubleResource(\"TvHomeLibraryArtworkOpacity\"", homeSource);
        Assert.Contains("GetDoubleResource(\"TvHomeSectionArtworkOpacity\"", homeSource);
        Assert.Contains("CreateHomeWideCardTextScrim()", homeSource);
        Assert.Contains("LinearGradientBrush", homeSource);
        Assert.Contains("AppTransparentColor", homeSource);
        Assert.Contains("AppCardScrimColor", homeSource);
        Assert.DoesNotContain("Opacity = 0.62", homeSource);
        Assert.DoesNotContain("Opacity = 0.68", homeSource);
    }

    [Fact]
    public void Home_Rail_Card_Metrics_And_Focus_Treatment_Use_Theme_Resources()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "App.xaml"));
        var homeXaml = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "Views", "HomePage.xaml"));
        var homeSource = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "Views", "HomePage.xaml.cs"));

        Assert.Contains("<x:Double x:Key=\"TvHomeWideCardWidth\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeWideCardHeight\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeRowPosterCardWidth\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeRowPosterCardHeight\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeWideCardTitleFontSize\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeFocusedCardScale\">", appXaml);
        Assert.Contains("Spacing=\"{StaticResource TvHomeSectionSpacing}\"", homeXaml);
        Assert.Contains("Spacing=\"{StaticResource TvHomeRailHeaderSpacing}\"", homeXaml);
        Assert.Contains("Width = GetDoubleResource(\"TvHomeWideCardWidth\"", homeSource);
        Assert.Contains("Height = GetDoubleResource(\"TvHomeWideCardHeight\"", homeSource);
        Assert.Contains("Width = GetDoubleResource(\"TvHomeRowPosterCardWidth\"", homeSource);
        Assert.Contains("Height = GetDoubleResource(\"TvHomeRowPosterCardHeight\"", homeSource);
        Assert.Contains("ApplyHomeCardFocusTreatment(button)", homeSource);
        Assert.Contains("HomeCard_OnLostFocus", homeSource);
        Assert.Contains("ScaleTransform", homeSource);
        Assert.DoesNotContain("Width = 250", homeSource);
        Assert.DoesNotContain("Width = 284", homeSource);
        Assert.DoesNotContain("Height = 132", homeSource);
        Assert.DoesNotContain("Width = 172", homeSource);
        Assert.DoesNotContain("Height = 252", homeSource);
        Assert.DoesNotContain("FontSize = 23", homeSource);
    }

    [Fact]
    public void Home_Hero_Layout_Metrics_Use_Theme_Resources()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "App.xaml"));
        var homeXaml = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "Views", "HomePage.xaml"));

        Assert.Contains("<x:Double x:Key=\"TvHomeHeroHeight\">", appXaml);
        Assert.Contains("<Thickness x:Key=\"TvHomeHeroPadding\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeHeroColumnSpacing\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeHeroPosterWidth\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeHeroPosterHeight\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeHeroTitleFontSize\">", appXaml);
        Assert.Contains("<x:Double x:Key=\"TvHomeHeroLogoMaxWidth\">", appXaml);
        Assert.Contains("<CornerRadius x:Key=\"TvHomeHeroCornerRadius\">", appXaml);
        Assert.Contains("Height=\"{StaticResource TvHomeHeroHeight}\"", homeXaml);
        Assert.Contains("Padding=\"{StaticResource TvHomeHeroPadding}\"", homeXaml);
        Assert.Contains("ColumnSpacing=\"{StaticResource TvHomeHeroColumnSpacing}\"", homeXaml);
        Assert.Contains("Width=\"{StaticResource TvHomeHeroPosterWidth}\"", homeXaml);
        Assert.Contains("Height=\"{StaticResource TvHomeHeroPosterHeight}\"", homeXaml);
        Assert.Contains("FontSize=\"{StaticResource TvHomeHeroTitleFontSize}\"", homeXaml);
        Assert.Contains("MaxWidth=\"{StaticResource TvHomeHeroLogoMaxWidth}\"", homeXaml);
        Assert.DoesNotContain("Height=\"246\"", homeXaml);
        Assert.DoesNotContain("Padding=\"26,24,26,24\"", homeXaml);
        Assert.DoesNotContain("FontSize=\"38\"", homeXaml);
        Assert.DoesNotContain("Width=\"182\"", homeXaml);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "DESIGN.md")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NextGenEmby.App", "App.xaml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static IReadOnlyDictionary<string, string> ReadDesignColors(string path)
    {
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        var inColors = false;
        foreach (var line in File.ReadLines(path))
        {
            if (line == "colors:")
            {
                inColors = true;
                continue;
            }

            if (inColors && !line.StartsWith("  ", StringComparison.Ordinal))
            {
                break;
            }

            if (!inColors)
            {
                continue;
            }

            var match = Regex.Match(line, "^  ([a-z_]+): \"(#[0-9A-Fa-f]{6,8})\"$");
            if (match.Success)
            {
                colors[match.Groups[1].Value] = NormalizeColor(match.Groups[2].Value);
            }
        }

        return colors;
    }

    private static IReadOnlyDictionary<string, string> ReadAppColors(string path)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        var document = XDocument.Load(path);
        foreach (var color in document.Descendants().Where(e => e.Name.LocalName == "Color"))
        {
            var key = color.Attribute(xaml + "Key")?.Value;
            if (!string.IsNullOrWhiteSpace(key))
            {
                colors[key] = (color.Value ?? "").Trim();
            }
        }

        return colors;
    }

    private static string OpaqueRgb(string color)
    {
        var value = NormalizeColor(color);
        return value.Length == 9 ? "#" + value.Substring(3) : value;
    }

    private static string NormalizeColor(string color)
    {
        return color.Trim().ToUpperInvariant();
    }
}
