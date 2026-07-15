using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class NativePlaybackGraphDecouplingContractTests
{
    [Fact]
    public void Native_Subtitle_Path_Preserves_FFmpeg_Bitmap_Regions_Through_D2D_Overlay()
    {
        var root = FindRepositoryRoot();
        var decoderSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "SubtitleDecoder.cpp"));
        var rendererSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "SubtitleRenderer.cpp"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));

        Assert.Contains("SUBTITLE_BITMAP", decoderSource, StringComparison.Ordinal);
        Assert.Contains("TryConvertIndexedSubtitleBitmap", decoderSource, StringComparison.Ordinal);
        Assert.Contains("SetCue(*cue)", graphSource, StringComparison.Ordinal);
        Assert.Contains("DrawSubtitleBitmapOverlay", rendererSource, StringComparison.Ordinal);
    }

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

        Assert.Contains("VideoRenderPhaseSample Render(DecodedVideoFrame const& frame, bool hdrDisplayActive);", rendererHeader, StringComparison.Ordinal);
        Assert.Contains("return sample;", rendererSource, StringComparison.Ordinal);
        Assert.Contains("auto const renderSample = m_videoRenderer.Render(frame, m_hdrOutputActive);", graphSource, StringComparison.Ordinal);
        Assert.Contains("auto const rendered = renderSample.Path != VideoRenderPath::None;", graphSource, StringComparison.Ordinal);
        Assert.Contains("m_qualityMetrics.RecordVideoRenderPhaseSample(renderSample);", graphSource, StringComparison.Ordinal);
        Assert.Contains("auto presented = m_deviceResources.Present();", graphSource, StringComparison.Ordinal);
        Assert.Contains("if (rendered && presented)", graphSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlaybackGraph_Breaks_Render_Interval_Continuity_Across_Pause_And_Resume()
    {
        var root = FindRepositoryRoot();
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));
        var pauseSource = ReadMethodBody(graphSource, "void PlaybackGraph::Pause");
        var resumeSource = ReadMethodBody(graphSource, "void PlaybackGraph::Resume");

        Assert.Contains("m_renderIntervalTracker.BreakContinuity()", pauseSource, StringComparison.Ordinal);
        Assert.Contains("m_renderIntervalTracker.BreakContinuity()", resumeSource, StringComparison.Ordinal);
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
        var methodStart = graphSource.IndexOf("PlaybackGraphSwitchTiming PlaybackGraph::SwitchSubtitleStream", StringComparison.Ordinal);
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

        Assert.Contains("options.Scenario == L\"subtitle-switch\"", helperSource, StringComparison.Ordinal);
        Assert.Contains("runSubtitleSwitch(subtitleStreamIndexes[0], true)", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("runSubtitleSwitch(subtitleStreamIndexes[1]", helperSource, StringComparison.Ordinal);
        Assert.Contains("InteractionEvidenceTimeout", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("options.Scenario == L\"interactions\"", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("subtitleOff.Attempted = true;", helperSource, StringComparison.Ordinal);
        Assert.Contains("ReportStage(\"interaction-sample-captured\")", helperSource, StringComparison.Ordinal);
        Assert.Contains(
            "excludedSampleDuration += std::chrono::steady_clock::now() - interactionStartedAt;",
            helperSource,
            StringComparison.Ordinal);
        Assert.Contains("PausedPositionBeforeTicks", helperSource, StringComparison.Ordinal);
        Assert.Contains("PausedPositionAfterTicks", helperSource, StringComparison.Ordinal);
        Assert.Contains("PositionBeforeResumeTicks", helperSource, StringComparison.Ordinal);
        Assert.Contains("PositionAfterResumeTicks", helperSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Headless_Helper_Can_Hold_A_Network_Pause_For_Resume_Recovery_Testing()
    {
        var root = FindRepositoryRoot();
        var helperSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "NoiraPlayer.Native.Tests",
            "NativePlaybackGraphHeadlessSmokeTests.cpp"));

        Assert.Contains("--pause-seconds", helperSource, StringComparison.Ordinal);
        Assert.Contains("options.PauseSeconds", helperSource, StringComparison.Ordinal);
        Assert.Contains("pauseDurationSeconds=", helperSource, StringComparison.Ordinal);
        Assert.Contains("pauseResumeStatus=", helperSource, StringComparison.Ordinal);
        Assert.Contains("positionBeforePauseTicks=", helperSource, StringComparison.Ordinal);
        Assert.Contains("positionAfterResumeTicks=", helperSource, StringComparison.Ordinal);
        Assert.Contains("decodedVideoFramesBeforePause=", helperSource, StringComparison.Ordinal);
        Assert.Contains("renderedVideoFramesBeforePause=", helperSource, StringComparison.Ordinal);
        Assert.Contains("postResumeDecodedVideoFrames=", helperSource, StringComparison.Ordinal);
        Assert.Contains("postResumeRenderedVideoFrames=", helperSource, StringComparison.Ordinal);
        Assert.Contains("actualPauseDurationMs=", helperSource, StringComparison.Ordinal);
        Assert.Contains("resumeRecoveryDurationMs=", helperSource, StringComparison.Ordinal);
        Assert.Contains("NOIRAPLAYER_NATIVE_PAUSE_MARKER_PATH", helperSource, StringComparison.Ordinal);
        Assert.Contains("native playback graph failed:", helperSource, StringComparison.Ordinal);
        Assert.Contains("renderedVideoFrames=", helperSource, StringComparison.Ordinal);
        Assert.Contains("observedSampleWallClockDurationMs=", helperSource, StringComparison.Ordinal);
        Assert.Contains("sourceCodec=", helperSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Headless_Reference_Case_Must_Match_Execution_Arguments()
    {
        var root = FindRepositoryRoot();
        var harnessSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "NoiraPlayer.PlaybackQuality.Headless",
            "Program.cs"));

        Assert.Contains("options.ReferenceCase.StartPositionTicks != options.StartPositionTicks", harnessSource, StringComparison.Ordinal);
        Assert.Contains("options.ReferenceCase.SeekTargetPositionTicks != options.SeekTargetPositionTicks", harnessSource, StringComparison.Ordinal);
        Assert.Contains("options.ReferenceCase.ForceSdrOutput != options.ForceSdrOutput", harnessSource, StringComparison.Ordinal);
        Assert.Contains("options.ReferenceCase.PauseSeconds != options.PauseSeconds", harnessSource, StringComparison.Ordinal);
        Assert.Contains("options.ReferenceCase.ExecutionRequirement.Scenario", harnessSource, StringComparison.Ordinal);
        Assert.Contains("must match the corresponding execution arguments", harnessSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Headless_Timeline_Uses_The_Explicit_Manifest_Seek_Target()
    {
        var root = FindRepositoryRoot();
        var harnessSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "NoiraPlayer.PlaybackQuality.Headless",
            "Program.cs"));
        var helperSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "NoiraPlayer.Native.Tests",
            "NativePlaybackGraphHeadlessSmokeTests.cpp"));

        Assert.Contains("--seek-target-position-ticks", harnessSource, StringComparison.Ordinal);
        Assert.Contains("options.SeekTargetPositionTicks", harnessSource, StringComparison.Ordinal);
        Assert.Contains("--seek-target-position-ticks", helperSource, StringComparison.Ordinal);
        Assert.Contains("seek.TargetPositionTicks = options.SeekTargetPositionTicks", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("constexpr int64_t SeekTargetPositionTicks", helperSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Headless_Timeline_Waits_For_The_First_Post_Seek_Presentation()
    {
        var root = FindRepositoryRoot();
        var helperSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "NoiraPlayer.Native.Tests",
            "NativePlaybackGraphHeadlessSmokeTests.cpp"));
        var timelineStart = helperSource.IndexOf(
            "if (options.Scenario == L\"timeline\")",
            StringComparison.Ordinal);
        var timelineEnd = helperSource.IndexOf(
            "auto displayRefreshRateHz",
            timelineStart,
            StringComparison.Ordinal);

        Assert.True(timelineStart >= 0 && timelineEnd > timelineStart);
        var timelineSource = helperSource[timelineStart..timelineEnd];
        var waitIndex = timelineSource.IndexOf("waitForEvidence([&]()", StringComparison.Ordinal);
        var recoveryIndex = timelineSource.IndexOf(
            "seek.RecoveryDurationMs =",
            StringComparison.Ordinal);

        Assert.True(waitIndex >= 0, "Timeline seek must wait for the first presented frame when graph.Seek returns before presentation.");
        Assert.True(recoveryIndex > waitIndex, "Seek recovery duration must include the wait until first presentation.");
    }

    [Fact]
    public void Native_Headless_End_Of_Stream_Uses_Natural_Graph_State_Evidence()
    {
        var root = FindRepositoryRoot();
        var helperSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "NoiraPlayer.Native.Tests",
            "NativePlaybackGraphHeadlessSmokeTests.cpp"));
        var harnessModule = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "quality-run",
            "NativeHeadlessHarness.psm1"));

        Assert.Contains("scenario == L\"end-of-stream\"", helperSource, StringComparison.Ordinal);
        Assert.Contains("PlaybackGraphState::Stopped", helperSource, StringComparison.Ordinal);
        Assert.Contains("message == L\"Playback ended.\"", helperSource, StringComparison.Ordinal);
        Assert.Contains("endOfStreamAttempted=", helperSource, StringComparison.Ordinal);
        Assert.Contains("endOfStreamObserved=", helperSource, StringComparison.Ordinal);
        Assert.Contains("endOfStreamStatus=", helperSource, StringComparison.Ordinal);
        Assert.Contains("endOfStreamPositionTicks=", helperSource, StringComparison.Ordinal);
        Assert.Contains("'end-of-stream'", harnessModule, StringComparison.Ordinal);
        Assert.DoesNotContain("endOfStreamObserved = timeline", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("endOfStreamObserved = graph.CurrentPositionTicks", helperSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AppHosted_Metrics_Expose_Native_Subtitle_Selection_And_Cue_Render_Count()
    {
        var root = FindRepositoryRoot();
        var idl = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "NativePlaybackEngine.idl"));
        var nativeEngine = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "NativePlaybackEngine.cpp"));
        var appEngine = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Playback",
            "WinRtNativePlaybackEngine.cs"));

        Assert.Contains("UInt64 SubtitleCueRenderCount;", idl, StringComparison.Ordinal);
        Assert.Contains("UInt64 SubtitleDecodedCueCount;", idl, StringComparison.Ordinal);
        Assert.Contains("Int32 SelectedSubtitleStreamIndex;", idl, StringComparison.Ordinal);
        Assert.Contains("m_graph->SubtitleCueRenderCount()", nativeEngine, StringComparison.Ordinal);
        Assert.Contains("m_graph->SubtitleDecodedCueCount()", nativeEngine, StringComparison.Ordinal);
        Assert.Contains("m_graph->SelectedSubtitleStreamIndex()", nativeEngine, StringComparison.Ordinal);
        Assert.Contains(
            "SubtitleCueRenderCount = nativeMetrics.SubtitleCueRenderCount",
            appEngine,
            StringComparison.Ordinal);
        Assert.Contains(
            "SubtitleDecodedCueCount = nativeMetrics.SubtitleDecodedCueCount",
            appEngine,
            StringComparison.Ordinal);
        Assert.Contains(
            "SelectedSubtitleStreamIndex = nativeMetrics.SelectedSubtitleStreamIndex",
            appEngine,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Headless_EndOfStream_Does_Not_Apply_Hidden_Frame_Count_Thresholds()
    {
        var root = FindRepositoryRoot();
        var helper = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "NoiraPlayer.Native.Tests",
            "NativePlaybackGraphHeadlessSmokeTests.cpp"));

        Assert.Contains(
            "assert(endOfStreamAttempted || playbackSnapshot.DecodedVideoFrames > 1);",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "assert(endOfStreamAttempted || playbackSnapshot.RenderedVideoFrames > 1);",
            helper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TenBit_Subtitle_Overlay_Uses_D3d_Texture_Composition()
    {
        var root = FindRepositoryRoot();
        var resources = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "DxDeviceResources.cpp"));

        Assert.Contains("if (m_isTenBitSwapChain)", resources, StringComparison.Ordinal);
        Assert.Contains("DrawSubtitleBitmapOverlayD3d11(region)", resources, StringComparison.Ordinal);
        Assert.Contains("D3D11_BLEND_INV_SRC_ALPHA", resources, StringComparison.Ordinal);
        Assert.Contains("SubtitleOverlayShader", resources, StringComparison.Ordinal);
        Assert.Contains("TransferPQ", resources, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Headless_Report_Does_Not_Invent_Pause_Resume_Lifecycle()
    {
        var root = FindRepositoryRoot();
        var harnessSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "NoiraPlayer.PlaybackQuality.Headless",
            "Program.cs"));
        var helperAttemptGuard = "if (helper.PauseResume.Attempted)";
        var guardIndex = harnessSource.IndexOf(helperAttemptGuard, StringComparison.Ordinal);
        var pauseEventIndex = harnessSource.IndexOf(
            "AddLifecycleEvent(\n                lifecycle,\n                \"pause\"",
            StringComparison.Ordinal);
        var resumeEventIndex = harnessSource.IndexOf(
            "AddLifecycleEvent(\n                lifecycle,\n                \"resume\"",
            StringComparison.Ordinal);

        Assert.True(guardIndex >= 0, "Headless lifecycle must be guarded by actual pause/resume evidence.");
        Assert.True(pauseEventIndex > guardIndex, "Pause lifecycle must be emitted only after the attempt guard.");
        Assert.True(resumeEventIndex > pauseEventIndex, "Resume lifecycle must be emitted only after the attempt guard.");
    }

    [Fact]
    public void Native_Dolby_Vision_Evidence_Survives_Unsupported_Open_And_Reaches_Headless_Report()
    {
        var root = FindRepositoryRoot();
        var graphHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.h"));
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));
        var decoderHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "VideoDecoder.h"));
        var helperSource = File.ReadAllText(Path.Combine(root, "tests", "NoiraPlayer.Native.Tests", "NativePlaybackGraphHeadlessSmokeTests.cpp"));
        var harnessSource = File.ReadAllText(Path.Combine(root, "tools", "NoiraPlayer.PlaybackQuality.Headless", "Program.cs"));

        Assert.Contains("DolbyVisionConfigurationSnapshot() const noexcept", decoderHeader, StringComparison.Ordinal);
        Assert.Contains("m_lastVideoSourceSnapshot", graphHeader, StringComparison.Ordinal);
        Assert.Contains("EnrichVideoSourceSnapshot", graphSource, StringComparison.Ordinal);
        Assert.Contains("unsupportedCode=dolby-vision-profile5-no-fallback", helperSource, StringComparison.Ordinal);
        Assert.Contains("sourceDolbyVisionProfile=", helperSource, StringComparison.Ordinal);
        Assert.Contains("TryParseUnsupportedSource", harnessSource, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityExecutionStatus.Unsupported", harnessSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_D3d11va_Decoder_Uses_The_Independent_Device_And_Exports_Shared_Frames()
    {
        var root = FindRepositoryRoot();
        var decoderHeader = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "Media",
            "VideoDecoder.h"));
        var decoderSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "Media",
            "VideoDecoder.cpp"));

        Assert.Contains("D3D11SharedDecodeBridge m_sharedDecodeBridge", decoderHeader, StringComparison.Ordinal);
        Assert.Contains("m_sharedDecodeBridge.Initialize(d3dDevice, d3dContext)", decoderSource, StringComparison.Ordinal);
        Assert.Contains("m_sharedDecodeBridge.DecoderDevice()", decoderSource, StringComparison.Ordinal);
        Assert.Contains("m_sharedDecodeBridge.DecoderTextureMiscFlags()", decoderSource, StringComparison.Ordinal);
        Assert.Contains("m_sharedDecodeBridge.ExportFrame(", decoderSource, StringComparison.Ordinal);
        Assert.Contains("m_sharedDecodeBridge.WaitForFrame(", decoderSource, StringComparison.Ordinal);
        Assert.Contains("av_frame_clone(frame)", decoderSource, StringComparison.Ordinal);
        Assert.Contains("DecoderFrameLifetime", decoderHeader, StringComparison.Ordinal);

        var graphSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "Media",
            "PlaybackGraph.cpp"));
        var helperSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "NoiraPlayer.Native.Tests",
            "NativePlaybackGraphHeadlessSmokeTests.cpp"));
        Assert.Contains("snapshot.VideoDecodeDeviceMode", graphSource, StringComparison.Ordinal);
        Assert.Contains("snapshot.VideoDecodeSynchronizationMode", graphSource, StringComparison.Ordinal);
        Assert.Contains("snapshot.VideoDecodeWorkerActive", graphSource, StringComparison.Ordinal);
        Assert.Contains("snapshot.VideoDecodeQueueCapacity", graphSource, StringComparison.Ordinal);
        Assert.Contains("snapshot.VideoDecodeQueueMaxDepth", graphSource, StringComparison.Ordinal);
        Assert.Contains("snapshot.VideoDecodeQueueProducerWaitCount", graphSource, StringComparison.Ordinal);
        Assert.Contains("videoDecodeDeviceMode=", helperSource, StringComparison.Ordinal);
        Assert.Contains("videoDecodeSynchronizationMode=", helperSource, StringComparison.Ordinal);
        Assert.Contains("videoDecodeWorkerActive=", helperSource, StringComparison.Ordinal);
        Assert.Contains("videoDecodeQueueCapacity=", helperSource, StringComparison.Ordinal);
        Assert.Contains("videoDecodeQueueMaxDepth=", helperSource, StringComparison.Ordinal);
        Assert.Contains("videoDecodeQueueProducerWaitCount=", helperSource, StringComparison.Ordinal);

        var graphHeader = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "Media",
            "PlaybackGraph.h"));
        Assert.Contains("std::unique_ptr<VideoDecodeWorker> m_videoDecodeWorker", graphHeader, StringComparison.Ordinal);
        Assert.Contains("m_videoDecoder.UsesIndependentDecodeDevice()", graphSource, StringComparison.Ordinal);
        Assert.Contains("m_videoDecodeWorker->Start()", graphSource, StringComparison.Ordinal);
        Assert.Contains("void StartVideoDecodeWorkerOrFallback() noexcept", graphHeader, StringComparison.Ordinal);
        Assert.Contains("StartVideoDecodeWorkerOrFallback();", graphSource, StringComparison.Ordinal);
        Assert.Contains(
            "could not start the independent video decode worker; falling back to synchronous decode",
            graphSource,
            StringComparison.Ordinal);
        Assert.Contains("StopVideoDecodeWorkerForMutation", graphSource, StringComparison.Ordinal);
        Assert.Contains("m_videoDecodeWorker.reset();", graphSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Worker_Queue_Empty_Does_Not_Count_As_Video_Starvation_Without_Audio()
    {
        var root = FindRepositoryRoot();
        var graphSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "Media",
            "PlaybackGraph.cpp"));
        var renderNextFrame = ReadMethodBody(graphSource, "bool PlaybackGraph::RenderNextFrame");

        Assert.Contains("if (hasQueuedAudio)", renderNextFrame, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "if (workerWaitingForFrame || hasQueuedAudio)",
            renderNextFrame,
            StringComparison.Ordinal);
        Assert.Contains(
            "return workerWaitingForFrame || hasQueuedAudio;",
            renderNextFrame,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Mutation_Restart_Does_Not_Block_Until_First_Decoded_Frame()
    {
        var root = FindRepositoryRoot();
        var graphSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "Media",
            "PlaybackGraph.cpp"));

        var initialStart = ReadMethodBody(graphSource, "void PlaybackGraph::StartVideoDecodeWorkerOrFallback");
        var mutationRestart = ReadMethodBody(graphSource, "void PlaybackGraph::RestartVideoDecodeWorkerAfterMutation");

        Assert.Contains("StartVideoDecodeWorker(true);", initialStart, StringComparison.Ordinal);
        Assert.Contains("StartVideoDecodeWorker(false);", mutationRestart, StringComparison.Ordinal);
        Assert.DoesNotContain("readyDeadline", mutationRestart, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Headless_Opened_Source_Hash_Uses_Observed_Media_Signature()
    {
        var root = FindRepositoryRoot();
        var harnessSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "NoiraPlayer.PlaybackQuality.Headless",
            "Program.cs"));

        Assert.Contains(
            "ComputeOpenedMediaSignature(runResult.Report)",
            harnessSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PlaybackQualitySourceFingerprint.ComputeOpenedSource(options.StreamUrl)",
            harnessSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Headless_Maps_Parsed_Source_Into_Observed_Metrics_Before_Composition()
    {
        var root = FindRepositoryRoot();
        var harnessSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "NoiraPlayer.PlaybackQuality.Headless",
            "Program.cs"));

        Assert.Contains(
            "PopulateObservedVideoSourceMetrics(helper.Metrics, helper.Source);",
            harnessSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "metrics.ObservedVideoSourceAvailable = true;",
            harnessSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "metrics.ObservedHdrKind = source.HdrKind;",
            harnessSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Headless_Gate_Runs_A_Deterministic_Network_Reconnect_Case()
    {
        var root = FindRepositoryRoot();
        var gateSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "quality-run",
            "run-native-headless-harness-smoke-test.ps1"));

        Assert.True(File.Exists(Path.Combine(
            root,
            "tools",
            "quality-run",
            "Start-FaultingRangeMediaServer.ps1")));
        Assert.Contains("Assert-NativeNetworkReconnectRecovery", gateSource, StringComparison.Ordinal);
        Assert.Contains("Start-FaultingRangeMediaServer.ps1", gateSource, StringComparison.Ordinal);
        Assert.Contains("Invoke-PlaybackQualityManifest.ps1", gateSource, StringComparison.Ordinal);
        Assert.Contains("$pauseSeconds = if ($LongPause) { 30 } else { 1 }", gateSource, StringComparison.Ordinal);
        Assert.Contains("executionValid", gateSource, StringComparison.Ordinal);
        Assert.Contains("strict validation", gateSource, StringComparison.Ordinal);
        Assert.Contains("request=2", gateSource, StringComparison.Ordinal);
        Assert.Contains("local-fault://network-reconnect-pause-resume", gateSource, StringComparison.Ordinal);
        Assert.Contains("-RuntimeSourceMapPath", gateSource, StringComparison.Ordinal);
        Assert.Contains("networkReconnectManifestCase", gateSource, StringComparison.Ordinal);
        Assert.Contains("networkReconnectCapturedReportPath", gateSource, StringComparison.Ordinal);
        Assert.Contains("nativeMaterializedDir", gateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Headless_Gate_Runs_A_Long_Pause_Disconnect_Challenge_After_The_Pause_Marker()
    {
        var root = FindRepositoryRoot();
        var gateSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "quality-run",
            "run-native-headless-harness-smoke-test.ps1"));
        var serverSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "quality-run",
            "Start-FaultingRangeMediaServer.ps1"));
        var harnessSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "NoiraPlayer.PlaybackQuality.Headless",
            "Program.cs"));

        Assert.Contains("Assert-NativeLongPauseNetworkRecovery", gateSource, StringComparison.Ordinal);
        Assert.Contains("$pauseSeconds = if ($LongPause) { 30 } else { 1 }", gateSource, StringComparison.Ordinal);
        Assert.Contains("category = if ($LongPause -or $DemuxReadRecovery) { 'challenge' } else { 'stable' }", gateSource, StringComparison.Ordinal);
        Assert.Contains("NOIRAPLAYER_NATIVE_PAUSE_MARKER_PATH", gateSource, StringComparison.Ordinal);
        Assert.Contains("pauseMarkerObserved", serverSource, StringComparison.Ordinal);
        Assert.Contains("WaitForPauseMarkerPath", serverSource, StringComparison.Ordinal);
        Assert.Contains("decodedVideoFramesBeforePause", harnessSource, StringComparison.Ordinal);
        Assert.Contains("renderedVideoFramesBeforePause", harnessSource, StringComparison.Ordinal);
        Assert.Contains("actualPauseDurationMs", harnessSource, StringComparison.Ordinal);
        Assert.Contains("resumeRecoveryDurationMs", harnessSource, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityInteractionCapture.CreatePauseResume", harnessSource, StringComparison.Ordinal);
        Assert.Contains("options.Scenario == PlaybackQualityExecutionScenario.PauseResume", harnessSource, StringComparison.Ordinal);
        Assert.Contains("? \"resume\"", harnessSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Headless_Demux_Read_Recovery_Challenge_Is_Strict_And_Deterministic()
    {
        var root = FindRepositoryRoot();
        var gateSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "quality-run",
            "run-native-headless-harness-smoke-test.ps1"));
        var serverSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "quality-run",
            "Start-FaultingRangeMediaServer.ps1"));
        var helperSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "NoiraPlayer.Native.Tests",
            "NativePlaybackGraphHeadlessSmokeTests.cpp"));
        var parserSource = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "NoiraPlayer.PlaybackQuality.Headless",
            "Program.cs"));
        var mediaSourceHeader = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "Media",
            "FfmpegMediaSource.h"));
        var mediaSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "Media",
            "FfmpegMediaSource.cpp"));
        var httpInputHeader = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "Media",
            "HttpMediaInput.h"));
        var httpInput = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.Native",
            "Media",
            "HttpMediaInput.cpp"));

        foreach (var field in new[]
        {
            "readErrorCount",
            "readRetryCount",
            "readRecoveryCount",
            "maxConsecutiveReadErrors",
            "lastReadErrorCode",
            "fatalReadErrorCode",
            "lastReadRecoveryDurationMs"
        })
        {
            Assert.Contains(field, helperSource, StringComparison.Ordinal);
            Assert.Contains(field, parserSource, StringComparison.Ordinal);
            Assert.Contains(field, gateSource, StringComparison.Ordinal);
        }

        Assert.Contains("TrySetRequiredNonPositiveInt32", parserSource, StringComparison.Ordinal);
        Assert.Contains("ResetRequestCount", serverSource, StringComparison.Ordinal);
        Assert.Contains("ImmediateResetFromRequest", serverSource, StringComparison.Ordinal);
        Assert.Contains("local/demux-read-error-recovery-after-pause", gateSource, StringComparison.Ordinal);
        Assert.Contains("$networkExpected = [ordered]@{", gateSource, StringComparison.Ordinal);
        Assert.Contains("expected = $networkExpected", gateSource, StringComparison.Ordinal);
        Assert.Contains("$networkExpected['readRecovery'] = [ordered]@{", gateSource, StringComparison.Ordinal);
        Assert.Contains("DemuxReadRecoveryOnly", gateSource, StringComparison.Ordinal);
        Assert.Contains("ExpectDemuxReadRecoveryFailure", gateSource, StringComparison.Ordinal);
        Assert.Contains("demux-read-recovery-v0.9", gateSource, StringComparison.Ordinal);
        Assert.Contains("run-metadata.json", gateSource, StringComparison.Ordinal);
        Assert.Contains("demux-read-error-recovery-server.out.log", gateSource, StringComparison.Ordinal);
        Assert.Contains("Expected disabled demux read recovery baseline report materialization", gateSource, StringComparison.Ordinal);
        Assert.Contains("TryReopenHttpTransport", mediaSourceHeader, StringComparison.Ordinal);
        Assert.Contains("avio_tell", mediaSource, StringComparison.Ordinal);
        Assert.Contains("avio_closep", mediaSource, StringComparison.Ordinal);
        Assert.Contains("avio_open2", mediaSource, StringComparison.Ordinal);
        Assert.Contains("SetFfmpegOption(&options, \"offset\"", mediaSource, StringComparison.Ordinal);
        Assert.Contains("avformat_flush(m_formatContext)", mediaSource, StringComparison.Ordinal);
        Assert.Contains("ReopenAt", httpInputHeader, StringComparison.Ordinal);
        Assert.Contains("PendingReadError", httpInputHeader, StringComparison.Ordinal);
        Assert.Contains("m_outerContext->error = 0", httpInput, StringComparison.Ordinal);
        Assert.Contains("m_outerContext->eof_reached = 0", httpInput, StringComparison.Ordinal);
        Assert.Contains("position < self->m_expectedSize", httpInput, StringComparison.Ordinal);
        Assert.Contains("readResult == AVERROR_EOF && m_httpMediaInput != nullptr", mediaSource, StringComparison.Ordinal);
        Assert.Contains("NOIRAPLAYER_NATIVE_INSTRUMENTED_AVIO", mediaSource, StringComparison.Ordinal);
        Assert.Contains("auto enabled = value == nullptr || std::string_view(value) != \"0\"", mediaSource, StringComparison.Ordinal);
        Assert.DoesNotContain("avformat_close_input(&m_formatContext)", mediaSource[
            mediaSource.IndexOf("TryReopenHttpTransport", StringComparison.Ordinal)..
            mediaSource.IndexOf("TryReadPacket", StringComparison.Ordinal)], StringComparison.Ordinal);
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
        Assert.DoesNotContain("PlaybackFramePacing::RenderLoopWait(true)", graphSource, StringComparison.Ordinal);
        Assert.Contains("m_qualityMetrics.RecordRenderIntervalAfterAudioAheadWaitMs(*elapsed);", graphSource, StringComparison.Ordinal);
        Assert.Contains("m_qualityMetrics.RecordRenderIntervalAfterNonAudioWaitMs(*elapsed);", graphSource, StringComparison.Ordinal);
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
    public void MediaSource_Serializes_Shared_Demux_Queue_And_Seek_Entrypoints()
    {
        var root = FindRepositoryRoot();
        var header = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "FfmpegMediaSource.h"));
        var source = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "FfmpegMediaSource.cpp"));

        Assert.Contains("mutable std::mutex m_demuxMutex", header, StringComparison.Ordinal);
        foreach (var method in new[]
        {
            "FfmpegMediaSource::TryReadPacket",
            "FfmpegMediaSource::TryReadQueuedPacket",
            "FfmpegMediaSource::ReadTimingSnapshot",
            "FfmpegMediaSource::TransportBytesRead",
            "FfmpegMediaSource::Seek"
        })
        {
            Assert.Contains(
                "std::lock_guard lock(m_demuxMutex);",
                ReadMethodBody(source, method),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void D3d_Immediate_Context_Is_Protected_For_Decode_And_Render_Threads()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "DxDeviceResources.cpp"));
        var createDevice = ReadMethodBody(source, "DxDeviceResources::CreateDevice");

        Assert.Contains("ID3D11Multithread", createDevice, StringComparison.Ordinal);
        Assert.Contains("SetMultithreadProtected(TRUE)", createDevice, StringComparison.Ordinal);
    }

    [Fact]
    public void Seek_Replay_Hit_Does_Not_Claim_A_Demux_Seek()
    {
        var root = FindRepositoryRoot();
        var graphSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.cpp"));
        var helperSource = File.ReadAllText(Path.Combine(root, "tests", "NoiraPlayer.Native.Tests", "NativePlaybackGraphHeadlessSmokeTests.cpp"));
        var seekBody = ReadMethodBody(graphSource, "PlaybackGraph::Seek");

        Assert.Contains(
            "m_lastSeekReplaySnapshot.Hit ? -1 : timeline.LastSeekDemuxTargetTicks",
            seekBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "seek.PacketCacheHit ? -1 : timeline.LastSeekDemuxTargetTicks",
            helperSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void App_Playback_Enables_Bounded_Seek_Replay_By_Default()
    {
        var root = FindRepositoryRoot();
        var graphHeader = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.Native", "Media", "PlaybackGraph.h"));

        Assert.Contains("bool EnableSeekPacketCache{true};", graphHeader, StringComparison.Ordinal);
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
