using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class NativePlaybackGraphDecouplingContractTests
{
    [Fact]
    public void PlaybackGraph_Open_Request_Is_Not_Bound_To_WinRt_RuntimeClass()
    {
        var root = FindRepositoryRoot();
        var graphHeader = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.Native", "Media", "PlaybackGraph.h"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.Native", "Media", "PlaybackGraph.cpp"));
        var engineSource = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.Native", "NativePlaybackEngine.cpp"));

        Assert.Contains("struct PlaybackGraphOpenRequest", graphHeader, StringComparison.Ordinal);
        Assert.Contains("void Open(PlaybackGraphOpenRequest const& request);", graphHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("NativePlaybackEngine.g.h", graphHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("void Open(NextGenEmby::Native::NativePlaybackOpenRequest", graphHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("void PlaybackGraph::Open(NextGenEmby::Native::NativePlaybackOpenRequest", graphSource, StringComparison.Ordinal);
        Assert.Contains("CreatePlaybackGraphOpenRequest(request)", engineSource, StringComparison.Ordinal);
        Assert.Contains("m_graph->Open(graphRequest)", engineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlaybackGraph_Does_Not_Count_Rendered_Frame_When_Surface_Render_Or_Present_Fails()
    {
        var root = FindRepositoryRoot();
        var rendererHeader = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.Native", "Media", "VideoRenderer.h"));
        var rendererSource = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.Native", "Media", "VideoRenderer.cpp"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.Native", "Media", "PlaybackGraph.cpp"));

        Assert.Contains("bool Render(DecodedVideoFrame const& frame, bool hdrDisplayActive);", rendererHeader, StringComparison.Ordinal);
        Assert.Contains("return rendered;", rendererSource, StringComparison.Ordinal);
        Assert.Contains("auto rendered = m_videoRenderer.Render(frame, m_hdrOutputActive);", graphSource, StringComparison.Ordinal);
        Assert.Contains("auto presented = m_deviceResources.Present();", graphSource, StringComparison.Ordinal);
        Assert.Contains("if (rendered && presented)", graphSource, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "NextGenEmby.Native", "Media", "PlaybackGraph.h")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NextGenEmby.Core", "PlaybackQuality", "PlaybackQualityReport.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
