using System;
using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class PlaybackBackendDiagnosticsTests
{
    [Fact]
    public void Capabilities_Can_Report_Native_Hdr_Features()
    {
        var capabilities = new PlaybackBackendCapabilities(
            PlaybackBackendFeature.DirectPlayHttp |
            PlaybackBackendFeature.HevcMain10 |
            PlaybackBackendFeature.Hdr10 |
            PlaybackBackendFeature.AudioStreamSwitching |
            PlaybackBackendFeature.SubtitleStreamSwitching);

        Assert.True(capabilities.Supports(PlaybackBackendFeature.DirectPlayHttp));
        Assert.True(capabilities.Supports(PlaybackBackendFeature.HevcMain10));
        Assert.True(capabilities.Supports(PlaybackBackendFeature.Hdr10));
        Assert.False(capabilities.Supports(PlaybackBackendFeature.Transcoding));
    }

    [Fact]
    public void DisplayStatus_Requires_Message_For_Failed_Hdr_Status()
    {
        var status = new PlaybackDisplayStatus(
            HdrOutputStatus.Failed,
            isHdrDisplayAvailable: true,
            isHdrOutputActive: false,
            message: "DXGI SetColorSpace1 failed.");

        Assert.Equal(HdrOutputStatus.Failed, status.HdrStatus);
        Assert.True(status.IsHdrDisplayAvailable);
        Assert.False(status.IsHdrOutputActive);
        Assert.Equal("DXGI SetColorSpace1 failed.", status.Message);
        Assert.Equal("", status.SwapChainFormat);
        Assert.Equal("", status.SwapChainColorSpace);
        Assert.False(status.IsTenBitSwapChain);
        Assert.False(status.IsVideoProcessorColorSpaceValidated);
    }

    [Fact]
    public void DisplayStatus_Carries_Native_Color_Pipeline_Diagnostics()
    {
        var status = new PlaybackDisplayStatus(
            HdrOutputStatus.On,
            isHdrDisplayAvailable: true,
            isHdrOutputActive: true,
            swapChainFormat: "R10G10B10A2_UNORM",
            swapChainColorSpace: "RGB_FULL_G2084_NONE_P2020",
            isTenBitSwapChain: true,
            isVideoProcessorColorSpaceValidated: true);

        Assert.Equal("R10G10B10A2_UNORM", status.SwapChainFormat);
        Assert.Equal("RGB_FULL_G2084_NONE_P2020", status.SwapChainColorSpace);
        Assert.True(status.IsTenBitSwapChain);
        Assert.True(status.IsVideoProcessorColorSpaceValidated);
    }

    [Fact]
    public void DisplayStatus_Carries_Display_Refresh_Rate()
    {
        var status = new PlaybackDisplayStatus(
            HdrOutputStatus.On,
            isHdrDisplayAvailable: true,
            isHdrOutputActive: true,
            refreshRateHz: 59.94006);

        Assert.Equal(59.94006, status.RefreshRateHz);
    }

    [Fact]
    public void DisplayStatus_Carries_Video_Processor_Color_Conversion_Diagnostics()
    {
        var status = new PlaybackDisplayStatus(
            HdrOutputStatus.On,
            isHdrDisplayAvailable: true,
            isHdrOutputActive: true,
            videoProcessorInputColorSpace: "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
            videoProcessorOutputColorSpace: "RGB_FULL_G2084_NONE_P2020",
            videoProcessorConversionStatus: "validated");

        Assert.Equal("YCBCR_STUDIO_G2084_TOPLEFT_P2020", status.VideoProcessorInputColorSpace);
        Assert.Equal("RGB_FULL_G2084_NONE_P2020", status.VideoProcessorOutputColorSpace);
        Assert.Equal("validated", status.VideoProcessorConversionStatus);
    }

    [Fact]
    public void DisplayStatus_Flags_Hdr_To_Sdr_Tone_Mapping_Requirement()
    {
        var status = new PlaybackDisplayStatus(
            HdrOutputStatus.Off,
            isHdrDisplayAvailable: true,
            isHdrOutputActive: false,
            videoProcessorInputColorSpace: "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
            videoProcessorOutputColorSpace: "RGB_FULL_G22_NONE_P709",
            videoProcessorConversionStatus: "validated;requires-tone-mapping");

        Assert.True(status.RequiresExplicitToneMapping);
        Assert.False(status.IsVideoProcessorColorPipelineComplete);
    }

    [Fact]
    public void DisplayStatus_Flags_Missing_Tone_Mapping_Implementation()
    {
        var status = new PlaybackDisplayStatus(
            HdrOutputStatus.Off,
            isHdrDisplayAvailable: true,
            isHdrOutputActive: false,
            videoProcessorInputColorSpace: "YCBCR_STUDIO_G22_TOPLEFT_P2020",
            videoProcessorOutputColorSpace: "RGB_FULL_G22_NONE_P709",
            videoProcessorConversionStatus: "validated;tone-mapping-missing:hlg-to-sdr");

        Assert.True(status.HasMissingToneMappingImplementation);
        Assert.False(status.IsVideoProcessorColorPipelineComplete);
    }

    [Fact]
    public void DisplayStatus_Rejects_Failed_Status_Without_Message()
    {
        Assert.Throws<ArgumentException>(() =>
            new PlaybackDisplayStatus(
                HdrOutputStatus.Failed,
                isHdrDisplayAvailable: true,
                isHdrOutputActive: false));
    }
}
