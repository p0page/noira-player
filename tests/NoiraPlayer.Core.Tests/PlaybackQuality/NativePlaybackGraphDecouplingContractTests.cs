using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class NativePlaybackGraphDecouplingContractTests
{
    [Fact]
    public void PlaybackGraph_Open_Request_Is_Not_Bound_To_WinRt_RuntimeClass()
    {
        var root = FindRepositoryRoot();
        var graphHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.h"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));
        var engineSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.cpp"));

        Assert.Contains("struct PlaybackGraphOpenRequest", graphHeader, StringComparison.Ordinal);
        Assert.Contains("void Open(PlaybackGraphOpenRequest const& request);", graphHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("NativePlaybackEngine.g.h", graphHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("void Open(NoiraPlayer::Native::NativePlaybackOpenRequest", graphHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("void PlaybackGraph::Open(NoiraPlayer::Native::NativePlaybackOpenRequest", graphSource, StringComparison.Ordinal);
        Assert.Contains("CreatePlaybackGraphOpenRequest(request)", engineSource, StringComparison.Ordinal);
        Assert.Contains("m_graph->Open(graphRequest)", engineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlaybackGraph_Does_Not_Count_Rendered_Frame_When_Surface_Render_Or_Present_Fails()
    {
        var root = FindRepositoryRoot();
        var rendererHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "VideoRenderer.h"));
        var rendererSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "VideoRenderer.cpp"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));

        Assert.Contains("bool Render(DecodedVideoFrame const& frame, bool hdrDisplayActive);", rendererHeader, StringComparison.Ordinal);
        Assert.Contains("return rendered;", rendererSource, StringComparison.Ordinal);
        Assert.Contains("auto rendered = m_videoRenderer.Render(frame, m_hdrOutputActive);", graphSource, StringComparison.Ordinal);
        Assert.Contains("auto presented = m_deviceResources.Present();", graphSource, StringComparison.Ordinal);
        Assert.Contains("if (rendered && presented)", graphSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Playback_Commands_Leave_The_Calling_Apartment_Before_Graph_Work()
    {
        var root = FindRepositoryRoot();
        var engineSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.cpp"));
        var methodNames = new[]
        {
            "OpenAsync",
            "PauseAsync",
            "ResumeAsync",
            "SeekAsync",
            "StopAsync",
            "SwitchAudioStreamAsync",
            "SwitchSubtitleStreamAsync",
            "DisableSubtitlesAsync"
        };

        foreach (var methodName in methodNames)
        {
            var body = ReadMethodBody(engineSource, "NativePlaybackEngine::" + methodName);
            var resumeIndex = body.IndexOf("co_await winrt::resume_background();", StringComparison.Ordinal);
            var graphCallIndex = body.IndexOf("m_graph->", StringComparison.Ordinal);

            Assert.True(resumeIndex >= 0, methodName + " must explicitly leave the UI apartment.");
            Assert.True(graphCallIndex > resumeIndex, methodName + " must leave the UI apartment before graph work.");
        }
    }

    [Fact]
    public void Playback_Position_Read_Uses_A_Lock_Free_Snapshot()
    {
        var root = FindRepositoryRoot();
        var graphHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.h"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));
        var body = ReadMethodBody(graphSource, "PlaybackGraph::CurrentPositionTicks");

        Assert.Contains("#include <atomic>", graphHeader, StringComparison.Ordinal);
        Assert.Contains("std::atomic<int64_t> m_positionSnapshotTicks", graphHeader, StringComparison.Ordinal);
        Assert.Contains("m_positionSnapshotTicks.load", body, StringComparison.Ordinal);
        Assert.DoesNotContain("m_graphMutex", body, StringComparison.Ordinal);
        Assert.DoesNotContain("m_audioRenderer", body, StringComparison.Ordinal);
    }

    [Fact]
    public void View_Bound_Display_Lookup_Is_Separated_From_Background_Mode_Changes()
    {
        var root = FindRepositoryRoot();
        var engineSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "NativePlaybackEngine.cpp"));
        var hdrHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "HdrDisplayController.h"));
        var hdrSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "HdrDisplayController.cpp"));
        var probeBody = ReadMethodBody(hdrSource, "HdrDisplayController::Probe");
        var enterBody = ReadMethodBody(hdrSource, "HdrDisplayController::EnterHdr10");
        var applyBody = ReadMethodBody(hdrSource, "HdrDisplayController::Apply");
        var restoreBody = ReadMethodBody(hdrSource, "HdrDisplayController::RestoreInitialState");
        var stopBody = ReadMethodBody(engineSource, "NativePlaybackEngine::StopAsync");

        Assert.Contains("HdmiDisplayInformation m_hdmi", hdrHeader, StringComparison.Ordinal);
        Assert.Contains("DisplayInformation::GetForCurrentView()", probeBody, StringComparison.Ordinal);
        Assert.Contains("HdmiDisplayInformation::GetForCurrentView()", probeBody, StringComparison.Ordinal);
        Assert.Contains("m_hdmi = hdmi;", probeBody, StringComparison.Ordinal);
        Assert.Contains("auto hdmi = m_hdmi;", applyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("GetForCurrentView", applyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Probe()", enterBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Probe()", restoreBody, StringComparison.Ordinal);

        var resumeBackgroundIndex = stopBody.IndexOf("co_await winrt::resume_background();", StringComparison.Ordinal);
        var restoreDisplayIndex = stopBody.IndexOf("m_hdr.RestoreInitialState()", StringComparison.Ordinal);
        Assert.True(restoreDisplayIndex > resumeBackgroundIndex, "Display restoration must use the cached agile HDMI object off the UI apartment.");
    }

    private static string ReadMethodBody(string source, string methodName)
    {
        var signatureIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, "Method was not found: " + methodName);

        var openBraceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(openBraceIndex >= 0, "Method body was not found: " + methodName);

        var depth = 0;
        for (var index = openBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(openBraceIndex, index - openBraceIndex + 1);
                }
            }
        }

        throw new InvalidDataException("Method body was not terminated: " + methodName);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.h")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.Core", "PlaybackQuality", "PlaybackQualityReport.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
