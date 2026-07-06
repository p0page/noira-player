using NextGenEmby.Core.Playback;
using NextGenEmby.Core.PlaybackQuality;
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
}
