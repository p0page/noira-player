using System;
using System.IO;
using System.Linq;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackTimelineDurationBridgeContractTests
{
    [Fact]
    public void Native_Observed_Duration_Reaches_Core_And_App_Timeline()
    {
        var root = FindRepositoryRoot();
        var idl = Read(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.idl");
        var engineHeader = Read(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.h");
        var engineSource = Read(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.cpp");
        var graphHeader = Read(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.h");
        var graphSource = Read(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp");
        var nativeBridge = Read(root, "src", "NoiraPlayer.App", "Playback", "WinRtNativePlaybackEngine.cs");
        var coreBackend = Read(root, "src", "NoiraPlayer.Core", "Playback", "NativeDirectXPlaybackBackend.cs");
        var playbackPage = Read(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs");

        Assert.Contains("Int64 DurationTicks();", idl, StringComparison.Ordinal);
        Assert.Contains("int64_t DurationTicks() const;", engineHeader, StringComparison.Ordinal);
        Assert.Contains("NativePlaybackEngine::DurationTicks() const", engineSource, StringComparison.Ordinal);
        Assert.Contains("m_graph->DurationTicks()", engineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("m_graph->TimelineSnapshot().LogicalDurationTicks", engineSource, StringComparison.Ordinal);
        Assert.Contains("int64_t DurationTicks() const noexcept;", graphHeader, StringComparison.Ordinal);
        Assert.Contains("PlaybackGraph::DurationTicks() const noexcept", graphSource, StringComparison.Ordinal);
        Assert.Contains("if (!m_open)", graphSource, StringComparison.Ordinal);
        Assert.Contains("public long DurationTicks => _engine.DurationTicks();", nativeBridge, StringComparison.Ordinal);
        Assert.Contains("public long DurationTicks => _engine.DurationTicks;", coreBackend, StringComparison.Ordinal);
        Assert.Contains("RefreshTimelineDuration", playbackPage, StringComparison.Ordinal);
        Assert.Contains("var observedDurationTicks = _orchestrator.CurrentDurationTicks;", playbackPage, StringComparison.Ordinal);
        Assert.DoesNotContain("var nativeDurationTicks = Math.Max(0, _backend.DurationTicks);", playbackPage, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] path)
    {
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(path).ToArray()));
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

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
