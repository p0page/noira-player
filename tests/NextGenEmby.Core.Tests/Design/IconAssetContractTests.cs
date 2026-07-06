using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class IconAssetContractTests
{
    private static readonly (string FileName, int Width, int Height)[] RequiredAssets =
    {
        ("StoreLogo.png", 50, 50),
        ("Square44x44Logo.png", 44, 44),
        ("Square150x150Logo.png", 150, 150),
        ("Wide310x150Logo.png", 310, 150),
        ("SplashScreen.png", 620, 300),
    };

    [Fact]
    public void Icon_Assets_Match_Uwp_Manifest_And_Required_Dimensions()
    {
        var root = FindRepositoryRoot();
        var assetRoot = Path.Combine(root, "src", "NextGenEmby.App", "Assets");
        var manifest = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "Package.appxmanifest"));
        var project = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "NextGenEmby.App.csproj"));

        foreach (var asset in RequiredAssets)
        {
            var relativePath = "Assets\\" + asset.FileName;
            Assert.Contains(relativePath, manifest, StringComparison.Ordinal);
            Assert.Contains(relativePath, project, StringComparison.Ordinal);

            var size = ReadPngSize(Path.Combine(assetRoot, asset.FileName));
            Assert.Equal((asset.Width, asset.Height), size);
        }
    }

    [Fact]
    public void Icon_Generator_Is_Symbol_Only_And_Avoids_Text_Rendering_Apis()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "Generate-AppIconAssets.ps1"));

        Assert.DoesNotContain("DrawString", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Drawing.Font", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TextRenderingHint", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Library Portal", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cinema Shelf", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Private media library", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Icon_Generator_Tokens_Map_To_Design_Tokens()
    {
        var root = FindRepositoryRoot();
        var designColors = ReadDesignColors(Path.Combine(root, "docs", "DESIGN.md"));
        var iconColors = ReadIconTokenColors(Path.Combine(root, "tools", "Generate-AppIconAssets.ps1"));

        Assert.Equal(designColors["canvas"], iconColors["Canvas"]);
        Assert.Equal(designColors["surface"], iconColors["Surface"]);
        Assert.Equal(designColors["surface_raised"], iconColors["Raised"]);
        Assert.Equal(designColors["hairline"], iconColors["Hairline"]);
        Assert.Equal(designColors["focus"], iconColors["Focus"]);
        Assert.Equal(designColors["primary"], iconColors["Play"]);
        Assert.Equal(designColors["secondary"], iconColors["Progress"]);
        Assert.Equal(designColors["text"], iconColors["Text"]);
        Assert.Equal(designColors["text_muted"], iconColors["MutedText"]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "DESIGN.md")) &&
                File.Exists(Path.Combine(directory.FullName, "tools", "Generate-AppIconAssets.ps1")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static (int Width, int Height) ReadPngSize(string path)
    {
        var header = File.ReadAllBytes(path);
        Assert.True(header.Length >= 24, "PNG asset is too small to contain an IHDR header.");

        var pngSignature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        Assert.Equal(pngSignature, header[..8]);

        return (
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4)));
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

    private static IReadOnlyDictionary<string, string> ReadIconTokenColors(string path)
    {
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            var match = Regex.Match(line, @"^\s+([A-Za-z]+) = New-IconColor (\d+) (\d+) (\d+)(?: \d+)?$");
            if (match.Success)
            {
                colors[match.Groups[1].Value] = "#" +
                    int.Parse(match.Groups[2].Value).ToString("X2") +
                    int.Parse(match.Groups[3].Value).ToString("X2") +
                    int.Parse(match.Groups[4].Value).ToString("X2");
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
