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
