using NextGenEmby.Core.Playback;
using NextGenEmby.Core.PlaybackQuality;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityReportMapperTests
{
    [Fact]
    public void ApplyDisplayStatus_Copies_Display_And_Color_Pipeline_Signals()
    {
        var report = new PlaybackQualityReport();
        var status = new PlaybackDisplayStatus(
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

        PlaybackQualityReportMapper.ApplyDisplayStatus(report, status);

        Assert.Equal("On", report.Display.HdrStatus);
        Assert.True(report.Display.IsHdrDisplayAvailable);
        Assert.True(report.Display.IsHdrOutputActive);
        Assert.Equal(59.94006, report.Display.RefreshRateHz);
        Assert.Equal("HDR active", report.Display.Message);
        Assert.Equal("Hdr10", report.ColorPipeline.ActualHdrOutput);
        Assert.Equal("R10G10B10A2_UNORM", report.ColorPipeline.SwapChainFormat);
        Assert.Equal("RGB_FULL_G2084_NONE_P2020", report.ColorPipeline.SwapChainColorSpace);
        Assert.True(report.ColorPipeline.IsTenBitSwapChain);
        Assert.True(report.ColorPipeline.IsVideoProcessorColorSpaceValidated);
        Assert.Equal("YCBCR_STUDIO_G2084_TOPLEFT_P2020", report.ColorPipeline.DxgiInput);
        Assert.Equal("RGB_FULL_G2084_NONE_P2020", report.ColorPipeline.DxgiOutput);
        Assert.Equal("validated", report.ColorPipeline.ConversionStatus);
    }

    [Fact]
    public void ApplyDisplayStatus_Maps_Sdr_Output_For_Hdr_Off()
    {
        var report = new PlaybackQualityReport();
        var status = new PlaybackDisplayStatus(
            HdrOutputStatus.Off,
            isHdrDisplayAvailable: true,
            isHdrOutputActive: false);

        PlaybackQualityReportMapper.ApplyDisplayStatus(report, status);

        Assert.Equal("Off", report.Display.HdrStatus);
        Assert.Equal("Sdr", report.ColorPipeline.ActualHdrOutput);
    }

    [Fact]
    public void ApplyMetrics_Copies_Timing_Sync_And_Buffer_Signals()
    {
        var report = new PlaybackQualityReport();
        var metrics = new PlaybackQualityMetricsSnapshot
        {
            RenderPasses = 100,
            DecodedVideoFrames = 95,
            RenderedVideoFrames = 90,
            SubmittedAudioFrames = 88,
            DroppedVideoFrames = 3,
            SeekPrerollDroppedFrames = 2,
            VideoAheadWaitCount = 7,
            VideoStarvedPasses = 4,
            AudioStarvedPasses = 1,
            QueuedAudioBuffers = 12,
            AudioClockTicks = 123456,
            VideoPositionTicks = 120000,
            RenderIntervalMsP50 = 41.7,
            RenderIntervalMsP95 = 45.2,
            RenderIntervalMsP99 = 80.0,
            MaxFrameGapMs = 120.0,
            AudioVideoDriftMsP50 = 12.0,
            AudioVideoDriftMsP95 = 38.0,
            AudioVideoDriftMsP99 = 50.0,
            AudioVideoDriftMsMax = 75.0
        };

        PlaybackQualityReportMapper.ApplyMetrics(report, metrics);

        Assert.Equal(100UL, report.Timing.RenderPasses);
        Assert.Equal(95UL, report.Timing.DecodedVideoFrames);
        Assert.Equal(90UL, report.Timing.RenderedVideoFrames);
        Assert.Equal(3UL, report.Timing.DroppedVideoFrames);
        Assert.Equal(2UL, report.Timing.SeekPrerollDroppedFrames);
        Assert.Equal(7UL, report.Timing.VideoAheadWaitCount);
        Assert.Equal(41.7, report.Timing.RenderIntervalMsP50);
        Assert.Equal(45.2, report.Timing.RenderIntervalMsP95);
        Assert.Equal(80.0, report.Timing.RenderIntervalMsP99);
        Assert.Equal(120.0, report.Timing.MaxFrameGapMs);
        Assert.Equal(123456, report.Sync.AudioClockTicks);
        Assert.Equal(120000, report.Sync.VideoPositionTicks);
        Assert.Equal(12.0, report.Sync.AudioVideoDriftMsP50);
        Assert.Equal(38.0, report.Sync.AudioVideoDriftMsP95);
        Assert.Equal(50.0, report.Sync.AudioVideoDriftMsP99);
        Assert.Equal(75.0, report.Sync.AudioVideoDriftMsMax);
        Assert.Equal(88UL, report.Buffers.SubmittedAudioFrames);
        Assert.Equal(12UL, report.Buffers.QueuedAudioBuffers);
        Assert.Equal(4UL, report.Buffers.VideoStarvedPasses);
        Assert.Equal(1UL, report.Buffers.AudioStarvedPasses);
    }

    [Fact]
    public void ApplySource_Copies_Playback_Source_Metadata()
    {
        var report = new PlaybackQualityReport();
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            Width = 3840,
            Height = 2160,
            VideoFrameRate = 23.976,
            HdrProfile = new HdrPlaybackProfile
            {
                Kind = HdrPlaybackKind.DolbyVisionWithHdr10Fallback,
                Codec = "hevc"
            }
        };
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Video,
            Codec = "hevc",
            Index = 0
        });
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Audio,
            Codec = "aac",
            Index = 1
        });
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Audio,
            Codec = "truehd",
            Index = 2
        });
        var descriptor = new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 0,
            audioStreamIndex: 2);

        PlaybackQualityReportMapper.ApplySource(report, descriptor);

        Assert.Equal("item-1", report.Source.ItemId);
        Assert.Equal("source-1", report.Source.MediaSourceId);
        Assert.Equal("hevc", report.Source.Codec);
        Assert.Equal(3840, report.Source.Width);
        Assert.Equal(2160, report.Source.Height);
        Assert.Equal(23.976, report.Source.FrameRate);
        Assert.Equal(1000.0 / 23.976, report.Timing.ExpectedFrameDurationMs, precision: 6);
        Assert.Equal("DolbyVisionWithHdr10Fallback", report.Source.HdrKind);
        Assert.Equal("truehd", report.Source.AudioCodec);
    }

    [Fact]
    public void ApplySource_Leaves_Expected_Frame_Duration_Empty_When_Frame_Rate_Is_Unusable()
    {
        var report = new PlaybackQualityReport
        {
            Timing = new PlaybackQualityTiming
            {
                ExpectedFrameDurationMs = 12.3
            }
        };
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            VideoFrameRate = 0
        };
        var descriptor = new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 0);

        PlaybackQualityReportMapper.ApplySource(report, descriptor);

        Assert.Equal(0, report.Timing.ExpectedFrameDurationMs);
    }
}
