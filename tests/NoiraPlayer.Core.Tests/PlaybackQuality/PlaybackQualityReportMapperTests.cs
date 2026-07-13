using NoiraPlayer.Core.Playback;
using NoiraPlayer.Core.PlaybackQuality;
using NoiraPlayer.Core.Emby;
using System.Linq;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

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
    public void ApplyDisplayStatus_Maps_Sdr_Output_When_Hdr_Is_Unsupported()
    {
        var report = new PlaybackQualityReport();
        var status = new PlaybackDisplayStatus(
            HdrOutputStatus.Unsupported,
            isHdrDisplayAvailable: false,
            isHdrOutputActive: false);

        PlaybackQualityReportMapper.ApplyDisplayStatus(report, status);

        Assert.Equal("Unsupported", report.Display.HdrStatus);
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
            HardwareDecodedVideoFrames = 70,
            SoftwareDecodedVideoFrames = 25,
            RenderedVideoFrames = 90,
            SubmittedAudioFrames = 88,
            DroppedVideoFrames = 3,
            SeekPrerollDroppedFrames = 2,
            VideoAheadWaitCount = 7,
            AudioAheadWaitCount = 5,
            VideoClockWaitCount = 2,
            VideoStarvedPasses = 4,
            AudioStarvedPasses = 1,
            QueuedAudioBuffers = 12,
            PlaybackDemuxReadDurationMs = 4321.5,
            PlaybackDemuxPacketCount = 345,
            PlaybackDemuxBytes = 6_543_210,
            PlaybackTransportCalls = new PlaybackQualityTransportCallSnapshot
            {
                Provider = "instrumented-ffmpeg-avio",
                EvidenceAvailable = true,
                ReadCalls = 44,
                SeekCalls = 2,
                ReadWaitMs = 3210.5,
                SeekWaitMs = 18.25,
                SeekDistanceBytes = 8192
            },
            AudioClockTicks = 123456,
            VideoPositionTicks = 120000,
            RenderIntervalMsP05 = 25.0,
            RenderIntervalMsP50 = 41.7,
            RenderIntervalMsP95 = 45.2,
            RenderIntervalMsP99 = 80.0,
            MinFrameGapMs = 25.0,
            MaxFrameGapMs = 120.0,
            RenderIntervalUnderExpected2MsCount = 6,
            RenderIntervalUnderExpected4MsCount = 4,
            RenderIntervalAfterAudioAheadWaitSampleCount = 2,
            RenderIntervalAfterAudioAheadWaitMsP95 = 43.0,
            RenderIntervalAfterAudioAheadWaitMsP99 = 44.0,
            RenderIntervalAfterAudioAheadWaitMsMax = 45.0,
            AudioAheadWaitEndToPresentSampleCount = 2,
            AudioAheadWaitEndToPresentMsP50 = 2.0,
            AudioAheadWaitEndToPresentMsP95 = 3.0,
            AudioAheadWaitEndToPresentMsP99 = 4.0,
            AudioAheadWaitEndToPresentMsMax = 5.0,
            RenderIntervalAfterNonAudioWaitSampleCount = 3,
            RenderIntervalAfterNonAudioWaitMsP95 = 34.0,
            RenderIntervalAfterNonAudioWaitMsP99 = 35.0,
            RenderIntervalAfterNonAudioWaitMsMax = 36.0,
            PresentDurationMsP50 = 2.0,
            PresentDurationMsP95 = 16.7,
            PresentDurationMsP99 = 33.4,
            PresentDurationMsMax = 50.1,
            AudioAheadWaitDurationMsP50 = 5.1,
            AudioAheadWaitDurationMsP95 = 15.2,
            AudioAheadWaitDurationMsP99 = 20.3,
            AudioAheadWaitDurationMsMax = 25.4,
            AudioAheadWaitTargetMsP50 = 1.1,
            AudioAheadWaitTargetMsP95 = 4.2,
            AudioAheadWaitTargetMsP99 = 5.3,
            AudioAheadWaitTargetMsMax = 6.4,
            AudioAheadWaitOversleepSemantics = "sum-positive-pass-oversleep-v2",
            AudioAheadWaitOversleepMsP50 = 4.0,
            AudioAheadWaitOversleepMsP95 = 11.0,
            AudioAheadWaitOversleepMsP99 = 15.0,
            AudioAheadWaitOversleepMsMax = 19.0,
            AudioAheadWaitFinalDeltaAbsMsP50 = 100.0,
            AudioAheadWaitFinalDeltaAbsMsP95 = 105.0,
            AudioAheadWaitFinalDeltaAbsMsP99 = 110.0,
            AudioAheadWaitFinalDeltaAbsMsMax = 120.0,
            AudioAheadWaitEpisodeCount = 3,
            AudioAheadWaitPassesPerEpisodeP50 = 1.0,
            AudioAheadWaitPassesPerEpisodeP95 = 2.0,
            AudioAheadWaitPassesPerEpisodeP99 = 3.0,
            AudioAheadWaitPassesPerEpisodeMax = 4.0,
            AudioAheadWaitPassDurationMsP50 = 7.1,
            AudioAheadWaitPassDurationMsP95 = 12.2,
            AudioAheadWaitPassDurationMsP99 = 18.3,
            AudioAheadWaitPassDurationMsMax = 24.4,
            AudioAheadWaitPassTargetMsP50 = 5.1,
            AudioAheadWaitPassTargetMsP95 = 8.2,
            AudioAheadWaitPassTargetMsP99 = 9.3,
            AudioAheadWaitPassTargetMsMax = 10.4,
            AudioAheadWaitPassOversleepMsP50 = 2.0,
            AudioAheadWaitPassOversleepMsP95 = 4.0,
            AudioAheadWaitPassOversleepMsP99 = 9.0,
            AudioAheadWaitPassOversleepMsMax = 14.0,
            FramePacingSourceFrameRate = 60.0,
            LateFrameDropToleranceMs = 41.6667,
            AudioVideoDriftMsP50 = 12.0,
            AudioVideoDriftMsP95 = 38.0,
            AudioVideoDriftMsP99 = 50.0,
            AudioVideoDriftMsMax = 75.0
        };

        PlaybackQualityReportMapper.ApplyMetrics(report, metrics);

        Assert.Equal(100UL, report.Timing.RenderPasses);
        Assert.Equal(95UL, report.Timing.DecodedVideoFrames);
        Assert.Equal(70UL, report.Timing.HardwareDecodedVideoFrames);
        Assert.Equal(25UL, report.Timing.SoftwareDecodedVideoFrames);
        Assert.Equal(90UL, report.Timing.RenderedVideoFrames);
        Assert.Equal(3UL, report.Timing.DroppedVideoFrames);
        Assert.Equal(2UL, report.Timing.SeekPrerollDroppedFrames);
        Assert.Equal(7UL, report.Timing.VideoAheadWaitCount);
        Assert.Equal(5UL, report.Timing.AudioAheadWaitCount);
        Assert.Equal(2UL, report.Timing.VideoClockWaitCount);
        Assert.Equal(25.0, report.Timing.RenderIntervalMsP05);
        Assert.Equal(41.7, report.Timing.RenderIntervalMsP50);
        Assert.Equal(45.2, report.Timing.RenderIntervalMsP95);
        Assert.Equal(80.0, report.Timing.RenderIntervalMsP99);
        Assert.Equal(25.0, report.Timing.MinFrameGapMs);
        Assert.Equal(120.0, report.Timing.MaxFrameGapMs);
        Assert.Equal(6UL, report.Timing.RenderIntervalUnderExpected2MsCount);
        Assert.Equal(4UL, report.Timing.RenderIntervalUnderExpected4MsCount);
        Assert.Equal(2UL, report.Timing.RenderIntervalAfterAudioAheadWaitSampleCount);
        Assert.Equal(43.0, report.Timing.RenderIntervalAfterAudioAheadWaitMsP95);
        Assert.Equal(44.0, report.Timing.RenderIntervalAfterAudioAheadWaitMsP99);
        Assert.Equal(45.0, report.Timing.RenderIntervalAfterAudioAheadWaitMsMax);
        Assert.Equal(2UL, report.Timing.AudioAheadWaitEndToPresentSampleCount);
        Assert.Equal(2.0, report.Timing.AudioAheadWaitEndToPresentMsP50);
        Assert.Equal(3.0, report.Timing.AudioAheadWaitEndToPresentMsP95);
        Assert.Equal(4.0, report.Timing.AudioAheadWaitEndToPresentMsP99);
        Assert.Equal(5.0, report.Timing.AudioAheadWaitEndToPresentMsMax);
        Assert.Equal(3UL, report.Timing.RenderIntervalAfterNonAudioWaitSampleCount);
        Assert.Equal(34.0, report.Timing.RenderIntervalAfterNonAudioWaitMsP95);
        Assert.Equal(35.0, report.Timing.RenderIntervalAfterNonAudioWaitMsP99);
        Assert.Equal(36.0, report.Timing.RenderIntervalAfterNonAudioWaitMsMax);
        Assert.Equal(2.0, report.Timing.PresentDurationMsP50);
        Assert.Equal(16.7, report.Timing.PresentDurationMsP95);
        Assert.Equal(33.4, report.Timing.PresentDurationMsP99);
        Assert.Equal(50.1, report.Timing.PresentDurationMsMax);
        Assert.Equal(5.1, report.Timing.AudioAheadWaitDurationMsP50);
        Assert.Equal(15.2, report.Timing.AudioAheadWaitDurationMsP95);
        Assert.Equal(20.3, report.Timing.AudioAheadWaitDurationMsP99);
        Assert.Equal(25.4, report.Timing.AudioAheadWaitDurationMsMax);
        Assert.Equal(1.1, report.Timing.AudioAheadWaitTargetMsP50);
        Assert.Equal(4.2, report.Timing.AudioAheadWaitTargetMsP95);
        Assert.Equal(5.3, report.Timing.AudioAheadWaitTargetMsP99);
        Assert.Equal(6.4, report.Timing.AudioAheadWaitTargetMsMax);
        Assert.Equal("sum-positive-pass-oversleep-v2", report.Timing.AudioAheadWaitOversleepSemantics);
        Assert.Equal(4.0, report.Timing.AudioAheadWaitOversleepMsP50);
        Assert.Equal(11.0, report.Timing.AudioAheadWaitOversleepMsP95);
        Assert.Equal(15.0, report.Timing.AudioAheadWaitOversleepMsP99);
        Assert.Equal(19.0, report.Timing.AudioAheadWaitOversleepMsMax);
        Assert.Equal(100.0, report.Timing.AudioAheadWaitFinalDeltaAbsMsP50);
        Assert.Equal(105.0, report.Timing.AudioAheadWaitFinalDeltaAbsMsP95);
        Assert.Equal(110.0, report.Timing.AudioAheadWaitFinalDeltaAbsMsP99);
        Assert.Equal(120.0, report.Timing.AudioAheadWaitFinalDeltaAbsMsMax);
        Assert.Equal(3UL, report.Timing.AudioAheadWaitEpisodeCount);
        Assert.Equal(1.0, report.Timing.AudioAheadWaitPassesPerEpisodeP50);
        Assert.Equal(2.0, report.Timing.AudioAheadWaitPassesPerEpisodeP95);
        Assert.Equal(3.0, report.Timing.AudioAheadWaitPassesPerEpisodeP99);
        Assert.Equal(4.0, report.Timing.AudioAheadWaitPassesPerEpisodeMax);
        Assert.Equal(7.1, report.Timing.AudioAheadWaitPassDurationMsP50);
        Assert.Equal(12.2, report.Timing.AudioAheadWaitPassDurationMsP95);
        Assert.Equal(18.3, report.Timing.AudioAheadWaitPassDurationMsP99);
        Assert.Equal(24.4, report.Timing.AudioAheadWaitPassDurationMsMax);
        Assert.Equal(5.1, report.Timing.AudioAheadWaitPassTargetMsP50);
        Assert.Equal(8.2, report.Timing.AudioAheadWaitPassTargetMsP95);
        Assert.Equal(9.3, report.Timing.AudioAheadWaitPassTargetMsP99);
        Assert.Equal(10.4, report.Timing.AudioAheadWaitPassTargetMsMax);
        Assert.Equal(2.0, report.Timing.AudioAheadWaitPassOversleepMsP50);
        Assert.Equal(4.0, report.Timing.AudioAheadWaitPassOversleepMsP95);
        Assert.Equal(9.0, report.Timing.AudioAheadWaitPassOversleepMsP99);
        Assert.Equal(14.0, report.Timing.AudioAheadWaitPassOversleepMsMax);
        Assert.Equal(60.0, report.Timing.FramePacingSourceFrameRate);
        Assert.Equal(41.6667, report.Timing.LateFrameDropToleranceMs);
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
        Assert.Equal(4321.5, report.Buffers.PlaybackDemuxReadDurationMs);
        Assert.Equal(345UL, report.Buffers.PlaybackDemuxPacketCount);
        Assert.Equal(6_543_210UL, report.Buffers.PlaybackDemuxBytes);
        Assert.Equal("instrumented-ffmpeg-avio", report.Buffers.PlaybackTransportProvider);
        Assert.Equal("available", report.Buffers.PlaybackTransportCallEvidenceStatus);
        Assert.Equal(44UL, report.Buffers.PlaybackTransportReadCalls);
        Assert.Equal(2UL, report.Buffers.PlaybackTransportSeekCalls);
        Assert.Equal(3210.5, report.Buffers.PlaybackTransportReadWaitMs);
        Assert.Equal(18.25, report.Buffers.PlaybackTransportSeekWaitMs);
        Assert.Equal(8192UL, report.Buffers.PlaybackTransportSeekDistanceBytes);
    }

    [Fact]
    public void ApplyMetrics_Copies_Actual_Position_For_Timeline_Signals()
    {
        var report = new PlaybackQualityReport();
        var metrics = new PlaybackQualityMetricsSnapshot
        {
            VideoPositionTicks = 610_000_000
        };

        PlaybackQualityReportMapper.ApplyMetrics(report, metrics);

        Assert.Equal(610_000_000, report.Position.ActualPositionTicks);
    }

    [Fact]
    public void ApplyMetrics_Copies_Zero_Actual_Position_For_Timeline_Signals()
    {
        var report = new PlaybackQualityReport();
        var metrics = new PlaybackQualityMetricsSnapshot
        {
            VideoPositionTicks = 0
        };

        PlaybackQualityReportMapper.ApplyMetrics(report, metrics);

        Assert.Equal(0, report.Position.ActualPositionTicks);
    }

    [Fact]
    public void ApplyMetrics_Maps_Native_Subtitle_Render_Evidence()
    {
        var report = new PlaybackQualityReport();
        var metrics = new PlaybackQualityMetricsSnapshot
        {
            SubtitleDecodedCueCount = 5,
            SubtitleCueRenderCount = 4,
            SelectedSubtitleStreamIndex = 7
        };

        PlaybackQualityReportMapper.ApplyMetrics(report, metrics);

        Assert.Equal(5UL, report.Tracks.SubtitleDecodedCueCount);
        Assert.Equal(4UL, report.Tracks.SubtitleCueRenderCount);
        Assert.Equal(7, report.Tracks.SelectedSubtitleStreamIndex);
        Assert.False(report.Tracks.IsSubtitleDisabled);
    }

    [Fact]
    public void ApplySource_Copies_Playback_Source_Metadata()
    {
        var report = new PlaybackQualityReport();
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            DirectStreamUrl = "https://media.example.invalid/emby/videos/1/stream.mkv?api_key=secret-token",
            Container = "mkv",
            Bitrate = 76_000_000,
            RunTimeTicks = 70_200_000_000,
            Width = 3840,
            Height = 2160,
            VideoFrameRate = 23.976,
            HdrProfile = new HdrPlaybackProfile
            {
                Kind = HdrPlaybackKind.DolbyVisionWithHdr10Fallback,
                Codec = "hevc",
                IsDolbyVision = true,
                DolbyVisionProfile = 8,
                DolbyVisionCompatibilityId = 1,
                HasHdr10BaseLayer = true,
                HasHlgBaseLayer = false
            }
        };
        source.Chapters.Add(new EmbyChapter
        {
            Name = "Opening",
            StartPositionTicks = 0,
            ImageTag = "chapter-0"
        });
        source.Chapters.Add(new EmbyChapter
        {
            Name = "Act 1",
            StartPositionTicks = 900_000_000
        });
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Video,
            Codec = "hevc",
            Index = 0,
            Language = "und",
            VideoRange = "HDR10",
            ColorPrimaries = "bt2020",
            ColorTransfer = "smpte2084",
            ColorSpace = "bt2020nc",
            DisplayTitle = "4K HEVC Main10",
            RealFrameRate = 23.976,
            AverageFrameRate = 23.976,
            IsDefault = true,
            IsForced = false
        });
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Audio,
            Codec = "aac",
            Index = 1,
            Language = "jpn",
            ChannelLayout = "5.1",
            Channels = 6,
            DisplayTitle = "Japanese AAC 5.1"
        });
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Audio,
            Codec = "truehd",
            Index = 2,
            Language = "eng",
            ChannelLayout = "7.1",
            Channels = 8,
            DisplayTitle = "English TrueHD 7.1",
            IsDefault = true,
            IsForced = false
        });
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Subtitle,
            Codec = "srt",
            Index = 7,
            Language = "zho",
            DisplayTitle = "Chinese SRT",
            IsExternal = true,
            IsDefault = false,
            IsForced = true
        });
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Subtitle,
            Codec = "ass",
            Index = 8,
            Language = "eng",
            DisplayTitle = "English ASS"
        });
        var descriptor = new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 600_000_000,
            audioStreamIndex: 2,
            subtitleStreamIndex: 7);

        PlaybackQualityReportMapper.ApplySource(report, descriptor);

        Assert.Equal("item-1", report.Source.ItemId);
        Assert.Equal(600_000_000, report.Position.RequestedStartPositionTicks);
        Assert.Equal(600_000_000, report.Position.SeekTargetPositionTicks);
        Assert.Equal("source-1", report.Source.MediaSourceId);
        Assert.True(report.Source.HasDirectStreamUrl);
        Assert.Equal("https", report.Source.DirectStreamProtocol);
        Assert.DoesNotContain("secret-token", report.Source.DirectStreamProtocol);
        Assert.Equal("mkv", report.Source.Container);
        Assert.Equal(76_000_000, report.Source.Bitrate);
        Assert.Equal(70_200_000_000, report.Source.DurationTicks);
        Assert.Equal("hevc", report.Source.Codec);
        Assert.Equal("HDR10", report.Source.VideoRange);
        Assert.Equal("bt2020", report.Source.ColorPrimaries);
        Assert.Equal("smpte2084", report.Source.ColorTransfer);
        Assert.Equal("bt2020nc", report.Source.ColorSpace);
        Assert.Equal(3840, report.Source.Width);
        Assert.Equal(2160, report.Source.Height);
        Assert.Equal(23.976, report.Source.FrameRate);
        Assert.True(report.Source.HasChapterMetadata);
        Assert.Equal(2, report.Source.ChapterCount);
        Assert.Equal("Opening", report.Source.Chapters[0].Name);
        Assert.Equal(0, report.Source.Chapters[0].StartPositionTicks);
        Assert.Equal("chapter-0", report.Source.Chapters[0].ImageTag);
        Assert.Equal("Act 1", report.Source.Chapters[1].Name);
        Assert.Equal(900_000_000, report.Source.Chapters[1].StartPositionTicks);
        Assert.Equal("", report.Source.Chapters[1].ImageTag);
        Assert.Equal(1000.0 / 23.976, report.Timing.ExpectedFrameDurationMs, precision: 6);
        Assert.Equal("DolbyVisionWithHdr10Fallback", report.Source.HdrKind);
        Assert.Equal("HDR10 fallback from Dolby Vision", report.Source.HdrPlaybackStrategy);
        Assert.True(report.Source.IsHdr);
        Assert.True(report.Source.IsDirectPlayable);
        Assert.True(report.Source.IsDolbyVision);
        Assert.Equal(8, report.Source.DolbyVisionProfile);
        Assert.Equal(1, report.Source.DolbyVisionCompatibilityId);
        Assert.True(report.Source.HasHdr10BaseLayer);
        Assert.False(report.Source.HasHlgBaseLayer);
        Assert.Equal("truehd", report.Source.AudioCodec);
        Assert.Equal(1, report.Tracks.VideoTrackCount);
        Assert.Equal(2, report.Tracks.AudioTrackCount);
        Assert.Equal(2, report.Tracks.SubtitleTrackCount);
        Assert.Equal(0, report.Tracks.SelectedVideoStreamIndex);
        Assert.Equal(2, report.Tracks.SelectedAudioStreamIndex);
        Assert.Equal(7, report.Tracks.SelectedSubtitleStreamIndex);
        Assert.False(report.Tracks.IsSubtitleDisabled);
        var video = Assert.Single(report.Tracks.Video);
        Assert.Equal(0, video.Index);
        Assert.Equal("hevc", video.Codec);
        Assert.Equal("und", video.Language);
        Assert.Equal("4K HEVC Main10", video.DisplayTitle);
        Assert.Equal(23.976, video.RealFrameRate);
        Assert.True(video.IsDefault);
        Assert.False(video.IsForced);
        var audio = report.Tracks.Audio.Single(track => track.Index == 2);
        Assert.Equal("truehd", audio.Codec);
        Assert.Equal("eng", audio.Language);
        Assert.Equal("7.1", audio.ChannelLayout);
        Assert.Equal(8, audio.Channels);
        Assert.Equal("English TrueHD 7.1", audio.DisplayTitle);
        Assert.True(audio.IsDefault);
        Assert.False(audio.IsForced);
        var subtitle = report.Tracks.Subtitles.Single(track => track.Index == 7);
        Assert.Equal("srt", subtitle.Codec);
        Assert.Equal("zho", subtitle.Language);
        Assert.Equal("Chinese SRT", subtitle.DisplayTitle);
        Assert.True(subtitle.IsExternal);
        Assert.False(subtitle.IsDefault);
        Assert.True(subtitle.IsForced);
    }

    [Fact]
    public void ApplySource_Does_Not_Report_Chapter_Count_When_Metadata_Was_Not_Observed()
    {
        var report = new PlaybackQualityReport();
        var source = new EmbyMediaSource
        {
            Id = "source-1"
        };
        var descriptor = new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 0);

        PlaybackQualityReportMapper.ApplySource(report, descriptor);

        Assert.False(report.Source.HasChapterMetadata);
        Assert.Null(report.Source.ChapterCount);
        Assert.Empty(report.Source.Chapters);
    }

    [Fact]
    public void ApplySource_Reports_Subtitles_Disabled_When_Selected_Subtitle_Is_Null()
    {
        var report = new PlaybackQualityReport();
        var source = new EmbyMediaSource
        {
            Id = "source-1"
        };
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Subtitle,
            Codec = "srt",
            Index = 7
        });
        var descriptor = new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 0,
            subtitleStreamIndex: null);

        PlaybackQualityReportMapper.ApplySource(report, descriptor);

        Assert.True(report.Tracks.IsSubtitleDisabled);
        Assert.Null(report.Tracks.SelectedSubtitleStreamIndex);
        Assert.Equal(1, report.Tracks.SubtitleTrackCount);
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
