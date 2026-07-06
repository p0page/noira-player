using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

    [Fact]
    public void Icon_Generator_Uses_Player_Status_Aperture_Primitives()
    {
        var root = FindRepositoryRoot();
        var design = File.ReadAllText(Path.Combine(root, "docs", "DESIGN.md"));
        var script = File.ReadAllText(Path.Combine(root, "tools", "Generate-AppIconAssets.ps1"));

        Assert.Contains("Player Status Aperture", design, StringComparison.Ordinal);
        Assert.Contains("Draw-PlayerStatusAperture", script, StringComparison.Ordinal);
        Assert.Contains("Draw-FocusPath", script, StringComparison.Ordinal);
        Assert.Contains("Draw-PlaybackCore", script, StringComparison.Ordinal);
        Assert.Contains("Draw-ProgressBase", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Draw-WidePlayerSignals", script, StringComparison.Ordinal);
        Assert.DoesNotContain("sideMeter", script, StringComparison.Ordinal);
        Assert.DoesNotContain("subtitleLine", script, StringComparison.Ordinal);
        Assert.DoesNotContain("audioLine", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Icon_Assets_Preserve_Focus_Play_And_Progress_Signals_At_All_Sizes()
    {
        var assetRoot = Path.Combine(FindRepositoryRoot(), "src", "NextGenEmby.App", "Assets");

        AssertIconSignals(Path.Combine(assetRoot, "Square44x44Logo.png"), 12, 28, 22);
        AssertIconSignals(Path.Combine(assetRoot, "StoreLogo.png"), 14, 34, 26);
        AssertIconSignals(Path.Combine(assetRoot, "Square150x150Logo.png"), 160, 360, 300);
        AssertIconSignals(Path.Combine(assetRoot, "Wide310x150Logo.png"), 140, 320, 240);
        AssertIconSignals(Path.Combine(assetRoot, "SplashScreen.png"), 420, 1000, 760);
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

    private static void AssertIconSignals(
        string path,
        int minimumFocusPixels,
        int minimumPlayPixels,
        int minimumProgressPixels)
    {
        var bitmap = ReadPngRgbaPixels(path);

        var focusPixels = bitmap.Pixels.Count(IsFocusPixel);
        var playPixels = bitmap.Pixels.Count(IsPlayPixel);
        var progressPixels = bitmap.Pixels.Count(IsProgressPixel);

        Assert.True(
            focusPixels >= minimumFocusPixels,
            $"{Path.GetFileName(path)} should preserve the cyan controller-focus signal; found {focusPixels} pixels.");
        Assert.True(
            playPixels >= minimumPlayPixels,
            $"{Path.GetFileName(path)} should preserve the green play/confirm signal; found {playPixels} pixels.");
        Assert.True(
            progressPixels >= minimumProgressPixels,
            $"{Path.GetFileName(path)} should preserve the amber progress signal; found {progressPixels} pixels.");
    }

    private static bool IsFocusPixel(RgbaPixel color)
    {
        return color.A > 160 &&
            color.B > 130 &&
            color.G > 100 &&
            color.R < 90 &&
            color.B - color.R > 70 &&
            color.G - color.R > 45;
    }

    private static bool IsPlayPixel(RgbaPixel color)
    {
        return color.A > 180 &&
            color.G > 140 &&
            color.R > 45 &&
            color.R < 135 &&
            color.B > 55 &&
            color.B < 150 &&
            color.G - color.R > 55 &&
            color.G - color.B > 35;
    }

    private static bool IsProgressPixel(RgbaPixel color)
    {
        return color.A > 180 &&
            color.R > 160 &&
            color.G > 120 &&
            color.B > 45 &&
            color.B < 135 &&
            color.R - color.B > 80 &&
            color.G - color.B > 45;
    }

    private static DecodedPng ReadPngRgbaPixels(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        Assert.Equal(signature, bytes[..8]);

        var width = 0;
        var height = 0;
        var bitDepth = 0;
        var colorType = 0;
        using var compressed = new MemoryStream();
        var offset = 8;
        while (offset < bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
            var dataOffset = offset + 8;

            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(dataOffset, 4));
                height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(dataOffset + 4, 4));
                bitDepth = bytes[dataOffset + 8];
                colorType = bytes[dataOffset + 9];
            }
            else if (type == "IDAT")
            {
                compressed.Write(bytes, dataOffset, length);
            }
            else if (type == "IEND")
            {
                break;
            }

            offset = dataOffset + length + 4;
        }

        Assert.Equal(8, bitDepth);
        Assert.Equal(6, colorType);

        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        zlib.CopyTo(decompressed);
        var scanlines = decompressed.ToArray();

        const int bytesPerPixel = 4;
        var stride = width * bytesPerPixel;
        var pixels = new byte[height * stride];
        var sourceOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = scanlines[sourceOffset++];
            var rowOffset = y * stride;
            for (var x = 0; x < stride; x++)
            {
                var raw = scanlines[sourceOffset++];
                var left = x >= bytesPerPixel ? pixels[rowOffset + x - bytesPerPixel] : 0;
                var up = y > 0 ? pixels[rowOffset + x - stride] : 0;
                var upLeft = y > 0 && x >= bytesPerPixel ? pixels[rowOffset + x - stride - bytesPerPixel] : 0;
                pixels[rowOffset + x] = (byte)((raw + Unfilter(filter, left, up, upLeft)) & 0xFF);
            }
        }

        var decoded = new List<RgbaPixel>(width * height);
        for (var index = 0; index < pixels.Length; index += bytesPerPixel)
        {
            decoded.Add(new RgbaPixel(
                pixels[index],
                pixels[index + 1],
                pixels[index + 2],
                pixels[index + 3]));
        }

        return new DecodedPng(width, height, decoded);
    }

    private static int Unfilter(int filter, int left, int up, int upLeft)
    {
        return filter switch
        {
            0 => 0,
            1 => left,
            2 => up,
            3 => (left + up) / 2,
            4 => Paeth(left, up, upLeft),
            _ => throw new InvalidDataException("Unsupported PNG filter: " + filter),
        };
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upDistance = Math.Abs(estimate - up);
        var upLeftDistance = Math.Abs(estimate - upLeft);

        if (leftDistance <= upDistance && leftDistance <= upLeftDistance)
        {
            return left;
        }

        return upDistance <= upLeftDistance ? up : upLeft;
    }

    private sealed record DecodedPng(int Width, int Height, IReadOnlyList<RgbaPixel> Pixels);

    private sealed record RgbaPixel(byte R, byte G, byte B, byte A);
}
