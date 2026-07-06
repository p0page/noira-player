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
