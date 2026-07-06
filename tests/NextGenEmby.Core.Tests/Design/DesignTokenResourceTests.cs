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
                colors[match.Groups[1].Value] = OpaqueRgb(match.Groups[2].Value);
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
        var value = color.Trim().ToUpperInvariant();
        return value.Length == 9 ? "#" + value.Substring(3) : value;
    }
}
