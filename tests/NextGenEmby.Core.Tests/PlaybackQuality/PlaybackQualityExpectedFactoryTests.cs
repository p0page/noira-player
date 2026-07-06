using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityExpectedFactoryTests
{
    [Fact]
    public void CreateDefault_Derives_Cadence_Thresholds_From_Source_Frame_Rate()
    {
        var descriptor = CreateDescriptor(
            frameRate: 23.976,
            hdrKind: HdrPlaybackKind.Hdr10);

        var expected = PlaybackQualityExpectedFactory.CreateDefault(descriptor);

        var frameDurationMs = 1000.0 / 23.976;
        Assert.Equal("hevc", expected.Codec);
        Assert.Equal(3840, expected.Width);
        Assert.Equal(2160, expected.Height);
        Assert.Equal(23.976, expected.FrameRate);
        Assert.Equal("Hdr10", expected.HdrKind);
        Assert.Equal("Hdr10", expected.HdrOutput);
        Assert.Equal("YCBCR_STUDIO_G2084_TOPLEFT_P2020", expected.DxgiInput);
        Assert.Equal("RGB_FULL_G2084_NONE_P2020", expected.DxgiOutput);
        Assert.Equal(120, expected.MinRenderedVideoFrames);
        Assert.Equal(0, expected.MaxDroppedFrames);
        Assert.Equal(frameDurationMs * 2.5, expected.MaxFrameGapMs!.Value, precision: 6);
        Assert.Equal(frameDurationMs * 1.25, expected.MaxRenderIntervalMsP95!.Value, precision: 6);
        Assert.Equal(frameDurationMs * 2.0, expected.MaxRenderIntervalMsP99!.Value, precision: 6);
        Assert.Equal(40, expected.MaxAudioVideoDriftMsP95);
        Assert.Equal(0, expected.MaxVideoStarvedPasses);
        Assert.Equal(0, expected.MaxAudioStarvedPasses);
        Assert.True(expected.RequireValidatedConversion);
        Assert.True(expected.RequireMatchedDisplayRefreshRate);
    }

    [Fact]
    public void CreateDefault_Maps_Sdr_Source_To_Sdr_Output()
    {
        var descriptor = CreateDescriptor(
            frameRate: 24.0,
            hdrKind: HdrPlaybackKind.Sdr);

        var expected = PlaybackQualityExpectedFactory.CreateDefault(descriptor);

        Assert.Equal("Sdr", expected.HdrOutput);
        Assert.Equal("YCBCR_STUDIO_G22_LEFT_P709", expected.DxgiInput);
        Assert.Equal("RGB_FULL_G22_NONE_P709", expected.DxgiOutput);
    }

    [Fact]
    public void CreateDefault_Maps_DolbyVision_Hdr10_Fallback_To_Hdr10_Dxgi_Path()
    {
        var descriptor = CreateDescriptor(
            frameRate: 24.0,
            hdrKind: HdrPlaybackKind.DolbyVisionWithHdr10Fallback);

        var expected = PlaybackQualityExpectedFactory.CreateDefault(descriptor);

        Assert.Equal("Hdr10", expected.HdrOutput);
        Assert.Equal("YCBCR_STUDIO_G2084_TOPLEFT_P2020", expected.DxgiInput);
        Assert.Equal("RGB_FULL_G2084_NONE_P2020", expected.DxgiOutput);
    }

    [Fact]
    public void CreateDefault_Leaves_Dxgi_Expectations_Unset_For_Hlg()
    {
        var descriptor = CreateDescriptor(
            frameRate: 24.0,
            hdrKind: HdrPlaybackKind.Hlg);

        var expected = PlaybackQualityExpectedFactory.CreateDefault(descriptor);

        Assert.Equal("", expected.HdrOutput);
        Assert.Equal("", expected.DxgiInput);
        Assert.Equal("", expected.DxgiOutput);
    }

    [Fact]
    public void CreateDefault_Leaves_Cadence_Thresholds_Unset_When_Frame_Rate_Is_Unusable()
    {
        var descriptor = CreateDescriptor(
            frameRate: 0,
            hdrKind: HdrPlaybackKind.Hdr10);

        var expected = PlaybackQualityExpectedFactory.CreateDefault(descriptor);

        Assert.Equal(0, expected.FrameRate);
        Assert.Null(expected.MinRenderedVideoFrames);
        Assert.Null(expected.MaxFrameGapMs);
        Assert.Null(expected.MaxRenderIntervalMsP95);
        Assert.Null(expected.MaxRenderIntervalMsP99);
        Assert.False(expected.RequireMatchedDisplayRefreshRate);
    }

    private static PlaybackDescriptor CreateDescriptor(
        double frameRate,
        HdrPlaybackKind hdrKind)
    {
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            Width = 3840,
            Height = 2160,
            VideoFrameRate = frameRate,
            HdrProfile = new HdrPlaybackProfile
            {
                Kind = hdrKind,
                Codec = "hevc"
            }
        };
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Video,
            Codec = "hevc",
            Index = 0
        });

        return new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 0);
    }
}
