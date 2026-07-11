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
    public void Native_Seek_Evidence_Uses_First_Presented_Frame_From_Current_Generation()
    {
        var root = FindRepositoryRoot();
        var graphHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.h"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));
        var helperSource = File.ReadAllText(Path.Combine(root, "tests", "NoiraPlayer.Native.Tests", "NativePlaybackGraphHeadlessSmokeTests.cpp"));

        Assert.Contains("SeekPresentationSnapshot SeekPresentationSnapshot() const noexcept;", graphHeader, StringComparison.Ordinal);
        Assert.Contains("m_seekPresentationTracker.BeginSeek(m_renderedVideoFrameCount)", graphSource, StringComparison.Ordinal);
        Assert.Contains("m_seekPresentationTracker.RecordPresentedFrame(", graphSource, StringComparison.Ordinal);
        Assert.Contains("graph.SeekPresentationSnapshot()", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("seek.ActualPositionTicks = graph.QualityMetricsSnapshot().VideoPositionTicks;", helperSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Audio_Switch_Evidence_Uses_Graph_Selected_Stream_State()
    {
        var root = FindRepositoryRoot();
        var audioRendererHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "AudioRenderer.h"));
        var graphHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.h"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));
        var helperSource = File.ReadAllText(Path.Combine(root, "tests", "NoiraPlayer.Native.Tests", "NativePlaybackGraphHeadlessSmokeTests.cpp"));

        Assert.Contains("std::optional<int32_t> SelectedStreamIndex() const noexcept;", audioRendererHeader, StringComparison.Ordinal);
        Assert.Contains("std::optional<int32_t> SelectedAudioStreamIndex() const noexcept;", graphHeader, StringComparison.Ordinal);
        Assert.Contains("return m_audioRenderer.SelectedStreamIndex();", graphSource, StringComparison.Ordinal);
        Assert.Contains("selectedAudioStreamIndex = graph.SelectedAudioStreamIndex();", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedAudioStreamIndex = audioSwitch.StreamIndex;", helperSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Subtitle_Switch_Is_Transactional_And_Paused_Smoke_Requires_Resume_Progress()
    {
        var root = FindRepositoryRoot();
        var subtitleDecoderHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "SubtitleDecoder.h"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));
        var helperSource = File.ReadAllText(Path.Combine(root, "tests", "NoiraPlayer.Native.Tests", "NativePlaybackGraphHeadlessSmokeTests.cpp"));
        var methodStart = graphSource.IndexOf("void PlaybackGraph::SwitchSubtitleStream", StringComparison.Ordinal);
        var methodEnd = graphSource.IndexOf("int64_t PlaybackGraph::CurrentPositionTicks", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart, "SwitchSubtitleStream source was not found.");
        var switchSource = graphSource[methodStart..methodEnd];

        Assert.Contains("std::optional<int32_t> SelectedStreamIndex() const noexcept;", subtitleDecoderHeader, StringComparison.Ordinal);
        Assert.Contains("RunSubtitleSwitchTransaction(", switchSource, StringComparison.Ordinal);
        Assert.Contains("SubtitleSwitchDisposition::Disabled", switchSource, StringComparison.Ordinal);
        Assert.Contains("m_subtitleDecoder.SelectedStreamIndex()", switchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("m_paused =", switchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("m_audioRenderer.Start()", switchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("m_audioRenderer.Stop()", switchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("m_audioRenderer.Pause()", switchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("m_audioRenderer.Resume()", switchSource, StringComparison.Ordinal);

        Assert.Contains("runSubtitleSwitch(subtitleStreamIndexes[0], true)", helperSource, StringComparison.Ordinal);
        Assert.Contains("PausedPositionBeforeTicks", helperSource, StringComparison.Ordinal);
        Assert.Contains("PausedPositionAfterTicks", helperSource, StringComparison.Ordinal);
        Assert.Contains("PositionBeforeResumeTicks", helperSource, StringComparison.Ordinal);
        Assert.Contains("PositionAfterResumeTicks", helperSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlaybackGraph_Audio_Ahead_Wait_Pass_Metrics_Do_Not_Take_A_Second_Graph_Lock_After_Wait()
    {
        var root = FindRepositoryRoot();
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));
        var normalizedSource = graphSource.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("auto completedRenderLoopWaitReason = RenderLoopWaitReason::Default;", graphSource, StringComparison.Ordinal);
        Assert.Contains(
            "m_qualityMetrics.RecordAudioAheadWaitPassMs(completedRenderLoopWaitDurationMs, completedRenderLoopWaitTargetMs);",
            graphSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "std::lock_guard lock(m_graphMutex);\n                m_qualityMetrics.RecordAudioAheadWaitPassMs(durationMs, targetMs);",
            normalizedSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PlaybackGraph_Groups_Render_Intervals_By_Preceding_Audio_Ahead_Wait()
    {
        var root = FindRepositoryRoot();
        var graphHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.h"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));

        Assert.Contains("RenderLoopWaitReason m_lastCompletedRenderLoopWaitReason", graphHeader, StringComparison.Ordinal);
        Assert.Contains("m_lastCompletedRenderLoopWaitReason = completedRenderLoopWaitReason;", graphSource, StringComparison.Ordinal);
        Assert.Contains("m_qualityMetrics.RecordRenderIntervalAfterAudioAheadWaitMs(elapsed);", graphSource, StringComparison.Ordinal);
        Assert.Contains("m_qualityMetrics.RecordRenderIntervalAfterNonAudioWaitMs(elapsed);", graphSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlaybackGraph_Records_Audio_Ahead_Wait_End_To_Successful_Present()
    {
        var root = FindRepositoryRoot();
        var graphHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.h"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));

        Assert.Contains("m_lastAudioAheadWaitEndedAt", graphHeader, StringComparison.Ordinal);
        Assert.Contains("completedAudioAheadWaitEndedAt = waitEndedAt;", graphSource, StringComparison.Ordinal);
        Assert.Contains("m_qualityMetrics.RecordAudioAheadWaitEndToPresentMs(endToPresentMs);", graphSource, StringComparison.Ordinal);
        Assert.Contains("m_lastAudioAheadWaitEndedAt.reset();", graphSource, StringComparison.Ordinal);
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
