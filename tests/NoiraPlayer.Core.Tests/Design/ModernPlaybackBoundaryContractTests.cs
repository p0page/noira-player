using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class ModernPlaybackBoundaryContractTests
{
    [Fact]
    public void Modernization_Docs_Define_Playback_Strategy_Boundary()
    {
        var spec = ReadRepositoryFile(
            "docs",
            "superpowers",
            "specs",
            "2026-07-09-dotnet-modernization-design.md");
        var plan = ReadRepositoryFile(
            "docs",
            "superpowers",
            "plans",
            "2026-07-09-dotnet-modernization.md");

        Assert.Contains(
            "Problem Boundary Between .NET Modernization And Playback Strategy",
            spec,
            StringComparison.Ordinal);
        Assert.Contains("Modernization-owned:", spec, StringComparison.Ordinal);
        Assert.Contains("Playback-worktree-owned:", spec, StringComparison.Ordinal);
        Assert.Contains(
            "Do not change source selection, transcode/direct-stream policy, decoder retry, starvation, or frame pacing in this branch.",
            spec,
            StringComparison.Ordinal);
        Assert.Contains(
            "Every playback failure must be classified as host/toolchain, validation harness, native diagnostic, or playback strategy before the branch advances.",
            spec,
            StringComparison.Ordinal);
        Assert.Contains(
            "Regression Attribution Matrix",
            spec,
            StringComparison.Ordinal);
        Assert.Contains(
            "same media, same playback code, different toolchain",
            spec,
            StringComparison.Ordinal);
        Assert.Contains(
            "same toolchain, same media, different playback strategy",
            spec,
            StringComparison.Ordinal);
        Assert.Contains(
            "Modernization branch must not carry the behavioral fix.",
            spec,
            StringComparison.Ordinal);
        Assert.Contains(
            "Step 20: Lock the playback-strategy boundary for modernization",
            plan,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Double_Eagain_Path_Remains_Diagnostics_Only()
    {
        var source = ReadRepositoryFile(
            "src",
            "NoiraPlayer.Native",
            "Media",
            "VideoDecoder.cpp");
        var block = SliceFrom(
            source,
            "if (sendResult == AVERROR(EAGAIN))",
            "AppendNativePlaybackDiagnostic(CreatePacketDiagnostic(\n                    L\"VideoDecoder.SendPacket failed\"");

        var diagnosticIndex = block.IndexOf("VideoDecoder.SendPacket eagain no-frame", StringComparison.Ordinal);
        var throwIndex = block.IndexOf("throw winrt::hresult_error(", StringComparison.Ordinal);
        var failureMessageIndex = block.IndexOf(
            "FFmpeg decoder could not accept a packet and produced no frame while draining.",
            StringComparison.Ordinal);

        Assert.True(diagnosticIndex >= 0, "double-EAGAIN path must emit searchable native diagnostics.");
        Assert.True(throwIndex > diagnosticIndex, "double-EAGAIN path must still fail after diagnostics.");
        Assert.True(failureMessageIndex > throwIndex, "double-EAGAIN failure message must remain on the thrown error.");
    }

    private static string SliceFrom(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, "Expected start marker was not found.");

        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, "Expected end marker was not found after start marker.");

        return source.Substring(startIndex, endIndex - startIndex);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var parts = new string[segments.Length + 1];
        parts[0] = FindRepositoryRoot();
        Array.Copy(segments, 0, parts, 1, segments.Length);
        return File.ReadAllText(Path.Combine(parts));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NoiraPlayer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
