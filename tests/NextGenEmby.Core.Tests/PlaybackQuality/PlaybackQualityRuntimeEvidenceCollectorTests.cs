using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityRuntimeEvidenceCollectorTests
{
    [Fact]
    public void ComposeRunResult_Uses_Backend_Display_And_Metrics_Evidence()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/runtime-evidence-hdr10",
            Category = "stable",
            Severity = "high",
            Stability = "stable",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 23.976,
                HdrKind = "Hdr10",
                HdrOutput = "Hdr10",
                DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                DxgiOutput = "RGB_FULL_G2084_NONE_P2020",
                MaxStartupDurationMs = 1500,
                MinRenderedVideoFrames = 120,
                MaxDroppedFrames = 1,
                MaxFrameGapMs = 105,
                MaxRenderIntervalMsP95 = 55,
                MaxRenderIntervalMsP99 = 80,
                MaxAudioVideoDriftMsP95 = 40,
                MaxVideoStarvedPasses = 0,
                MaxAudioStarvedPasses = 0,
                RequireValidatedConversion = true,
                RequireMatchedDisplayRefreshRate = true
            }
        };
        referenceCase.Purpose.Add("hdr-output");
        referenceCase.Purpose.Add("frame-pacing");
        referenceCase.Purpose.Add("av-sync");
        referenceCase.Purpose.Add("buffering");

        var descriptor = CreateDescriptor();
        var backend = new RuntimeEvidenceBackend();

        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult(
            referenceCase,
            descriptor,
            backend,
            backend,
            new PlaybackQualityStartup
            {
                CommandReceivedAt = "2026-07-07T00:00:00.000Z",
                PlaybackStartedAt = "2026-07-07T00:00:00.300Z",
                StartupDurationMs = 300
            },
            new PlaybackQualityEnvironment
            {
                CollectorVersion = "runtime-evidence-test",
                PlayerCoreVersion = "NextGenEmby.Core.Tests",
                SourceRevision = "test-revision",
                BuildConfiguration = "Debug"
            });

        Assert.Equal("pass", result.Report.Result);
        Assert.Equal("Hdr10", result.Report.ColorPipeline.ActualHdrOutput);
        Assert.Equal("RGB_FULL_G2084_NONE_P2020", result.Report.ColorPipeline.SwapChainColorSpace);
        Assert.Equal("validated", result.Report.ColorPipeline.ConversionStatus);
        Assert.Equal(59.94006, result.Report.Display.RefreshRateHz);
        Assert.Equal(240UL, result.Report.Timing.RenderedVideoFrames);
        Assert.Equal(40.0, result.Report.Timing.MaxFrameGapMs);
        Assert.Equal(1_200_000_000, result.Report.Position.ActualPositionTicks);
        Assert.Equal(1_199_700_000, result.Report.Sync.AudioClockTicks);
        Assert.Equal(12.0, result.Report.Sync.AudioVideoDriftMsP95);
        Assert.Equal(0UL, result.Report.Buffers.VideoStarvedPasses);
        Assert.Equal(0UL, result.Report.Buffers.AudioStarvedPasses);
        Assert.Equal("pass", result.ModelAnalysis.Result);
        Assert.Contains("timing.renderedVideoFrames", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("sync.audioVideoDriftMsP95", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("buffers.videoStarvedPasses", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("colorPipeline.actualHdrOutput", result.ModelAnalysis.EvidenceSignals);
    }

    private static PlaybackDescriptor CreateDescriptor()
    {
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            DirectStreamUrl = "https://example.invalid/hdr10.mp4",
            Width = 3840,
            Height = 2160,
            VideoFrameRate = 23.976,
            HdrProfile = new HdrPlaybackProfile
            {
                Kind = HdrPlaybackKind.Hdr10,
                Codec = "hevc"
            }
        };
        source.Streams.Add(new EmbyMediaStream
        {
            Index = 0,
            Kind = EmbyStreamKind.Video,
            Codec = "hevc",
            RealFrameRate = 23.976,
            AverageFrameRate = 23.976
        });
        source.Streams.Add(new EmbyMediaStream
        {
            Index = 1,
            Kind = EmbyStreamKind.Audio,
            Codec = "eac3",
            Language = "eng",
            ChannelLayout = "5.1"
        });

        return new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 0,
            audioStreamIndex: 1);
    }

    private sealed class RuntimeEvidenceBackend :
        IPlaybackBackendDiagnostics,
        IPlaybackQualityMetricsProvider
    {
        public PlaybackBackendCapabilities Capabilities { get; } =
            new PlaybackBackendCapabilities(
                PlaybackBackendFeature.DirectPlayHttp |
                PlaybackBackendFeature.Hevc |
                PlaybackBackendFeature.HevcMain10 |
                PlaybackBackendFeature.Hdr10);

        public PlaybackDisplayStatus DisplayStatus { get; } =
            new PlaybackDisplayStatus(
                HdrOutputStatus.On,
                isHdrDisplayAvailable: true,
                isHdrOutputActive: true,
                message: "HDR active",
                swapChainFormat: "R10G10B10A2_UNORM",
                swapChainColorSpace: "RGB_FULL_G2084_NONE_P2020",
                isTenBitSwapChain: true,
                isVideoProcessorColorSpaceValidated: true,
                videoProcessorInputColorSpace: "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
                videoProcessorOutputColorSpace: "RGB_FULL_G2084_NONE_P2020",
                videoProcessorConversionStatus: "validated",
                refreshRateHz: 59.94006);

        public bool TryGetQualityMetrics(out PlaybackQualityMetricsSnapshot metrics)
        {
            metrics = new PlaybackQualityMetricsSnapshot
            {
                RenderPasses = 240,
                DecodedVideoFrames = 240,
                RenderedVideoFrames = 240,
                SubmittedAudioFrames = 240,
                DroppedVideoFrames = 1,
                SeekPrerollDroppedFrames = 0,
                VideoAheadWaitCount = 0,
                VideoStarvedPasses = 0,
                AudioStarvedPasses = 0,
                QueuedAudioBuffers = 4,
                AudioClockTicks = 1_199_700_000,
                VideoPositionTicks = 1_200_000_000,
                RenderIntervalMsP50 = 41.708,
                RenderIntervalMsP95 = 42.2,
                RenderIntervalMsP99 = 48.0,
                MaxFrameGapMs = 40.0,
                FramePacingSourceFrameRate = 23.976,
                LateFrameDropToleranceMs = 104.27,
                AudioVideoDriftMsP50 = 4.0,
                AudioVideoDriftMsP95 = 12.0,
                AudioVideoDriftMsP99 = 18.0,
                AudioVideoDriftMsMax = 24.0
            };
            return true;
        }
    }
}
