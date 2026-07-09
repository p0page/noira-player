using NoiraPlayer.Core.Playback;
using Xunit;

namespace NoiraPlayer.Core.Tests.Playback;

public sealed class HdrPlaybackProfileClassifierTests
{
    [Fact]
    public void Classify_Returns_Sdr_For_Empty_Metadata()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(
            videoRange: "",
            colorPrimaries: "",
            colorTransfer: "",
            colorSpace: "",
            codec: "h264",
            displayTitle: "1080p H.264");

        Assert.Equal(HdrPlaybackKind.Sdr, profile.Kind);
        Assert.False(profile.IsDolbyVision);
        Assert.False(profile.IsHdr);
        Assert.True(profile.IsDirectPlayable);
        Assert.Equal(0, profile.SelectionRank);
        Assert.Equal("SDR", profile.PlaybackStrategy);
    }

    [Fact]
    public void Classify_Returns_Hdr10_For_Pq_Bt2020_Metadata()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(
            videoRange: "SDR",
            colorPrimaries: "bt2020",
            colorTransfer: "smpte2084",
            colorSpace: "bt2020nc",
            codec: "hevc",
            displayTitle: "4K HEVC Main10");

        Assert.Equal(HdrPlaybackKind.Hdr10, profile.Kind);
        Assert.True(profile.IsHdr);
        Assert.True(profile.IsDirectPlayable);
        Assert.Equal(1, profile.SelectionRank);
        Assert.Equal("HDR10", profile.PlaybackStrategy);
    }

    [Fact]
    public void Classify_Returns_Hlg_For_Arib_Transfer()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(
            videoRange: "",
            colorPrimaries: "bt2020",
            colorTransfer: "arib-std-b67",
            colorSpace: "bt2020nc",
            codec: "hevc",
            displayTitle: "HLG broadcast");

        Assert.Equal(HdrPlaybackKind.Hlg, profile.Kind);
        Assert.True(profile.IsHdr);
        Assert.True(profile.IsDirectPlayable);
        Assert.Equal(2, profile.SelectionRank);
        Assert.Equal("HLG", profile.PlaybackStrategy);
    }

    [Fact]
    public void Classify_Returns_DolbyVision_Hdr10_Fallback_For_Profile_8_1()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(
            videoRange: "HDR10 Dolby Vision",
            colorPrimaries: "bt2020",
            colorTransfer: "smpte2084",
            colorSpace: "bt2020nc",
            codec: "hevc",
            displayTitle: "4K HEVC DoVi Profile 8.1 HDR10");

        Assert.Equal(HdrPlaybackKind.DolbyVisionWithHdr10Fallback, profile.Kind);
        Assert.True(profile.IsHdr);
        Assert.True(profile.IsDolbyVision);
        Assert.True(profile.HasHdr10BaseLayer);
        Assert.False(profile.HasHlgBaseLayer);
        Assert.Null(profile.DolbyVisionProfile);
        Assert.Null(profile.DolbyVisionCompatibilityId);
        Assert.True(profile.IsDirectPlayable);
        Assert.Equal(3, profile.SelectionRank);
        Assert.Equal("HDR10 fallback from Dolby Vision", profile.PlaybackStrategy);
    }

    [Fact]
    public void Classify_Returns_DolbyVision_Hlg_Fallback_For_Profile_8_4()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(
            videoRange: "Dolby Vision",
            colorPrimaries: "bt2020",
            colorTransfer: "arib-std-b67",
            colorSpace: "bt2020nc",
            codec: "hevc",
            displayTitle: "DoVi Profile 8.4 HLG");

        Assert.Equal(HdrPlaybackKind.DolbyVisionWithHlgFallback, profile.Kind);
        Assert.True(profile.IsDolbyVision);
        Assert.False(profile.HasHdr10BaseLayer);
        Assert.True(profile.HasHlgBaseLayer);
        Assert.Null(profile.DolbyVisionProfile);
        Assert.Null(profile.DolbyVisionCompatibilityId);
        Assert.True(profile.IsDirectPlayable);
        Assert.Equal(4, profile.SelectionRank);
        Assert.Equal("HLG fallback from Dolby Vision", profile.PlaybackStrategy);
    }

    [Fact]
    public void Classify_Returns_Unsupported_For_Profile_5()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(
            videoRange: "Dolby Vision",
            colorPrimaries: "",
            colorTransfer: "",
            colorSpace: "",
            codec: "dvhe.05",
            displayTitle: "DoVi Profile 5");

        Assert.Equal(HdrPlaybackKind.DolbyVisionUnsupported, profile.Kind);
        Assert.True(profile.IsHdr);
        Assert.True(profile.IsDolbyVision);
        Assert.Equal(5, profile.DolbyVisionProfile);
        Assert.False(profile.IsDirectPlayable);
        Assert.Equal(100, profile.SelectionRank);
        Assert.Equal("Dolby Vision unsupported", profile.PlaybackStrategy);
    }

    [Fact]
    public void Classify_Does_Not_Trust_MediaSource_Name_For_DolbyVision()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(
            videoRange: "PC",
            colorPrimaries: "",
            colorTransfer: "",
            colorSpace: "",
            codec: "hevc",
            displayTitle: "4K HEVC",
            mediaSourceName: "4K / 12 Mbps, HEVC - DV • DDP5.1");

        Assert.Equal(HdrPlaybackKind.Sdr, profile.Kind);
        Assert.False(profile.IsDolbyVision);
        Assert.True(profile.IsDirectPlayable);
    }

    [Fact]
    public void Classify_Does_Not_Trust_DisplayTitle_For_DolbyVision_Or_Hdr()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(
            videoRange: "PC",
            colorPrimaries: "",
            colorTransfer: "",
            colorSpace: "",
            codec: "hevc",
            displayTitle: "4K HEVC DoVi Profile 5 HDR10");

        Assert.Equal(HdrPlaybackKind.Sdr, profile.Kind);
        Assert.False(profile.IsDolbyVision);
        Assert.True(profile.IsDirectPlayable);
    }

    [Fact]
    public void Classify_Does_Not_Block_Ambiguous_DolbyVision_Without_Profile_Or_BaseLayer()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(
            videoRange: "Dolby Vision",
            colorPrimaries: "",
            colorTransfer: "",
            colorSpace: "",
            codec: "hevc",
            displayTitle: "4K HEVC");

        Assert.Equal(HdrPlaybackKind.UnknownHdr, profile.Kind);
        Assert.True(profile.IsDolbyVision);
        Assert.True(profile.IsDirectPlayable);
        Assert.Null(profile.DolbyVisionProfile);
        Assert.Null(profile.DolbyVisionCompatibilityId);
    }

    [Fact]
    public void Classify_Returns_UnknownHdr_For_Bt2020_Without_Transfer()
    {
        var profile = HdrPlaybackProfileClassifier.Classify(
            videoRange: "",
            colorPrimaries: "bt2020",
            colorTransfer: "",
            colorSpace: "bt2020nc",
            codec: "hevc",
            displayTitle: "4K BT.2020");

        Assert.Equal(HdrPlaybackKind.UnknownHdr, profile.Kind);
        Assert.True(profile.IsHdr);
        Assert.True(profile.IsDirectPlayable);
        Assert.Equal(5, profile.SelectionRank);
        Assert.Equal("Unknown HDR", profile.PlaybackStrategy);
    }
}
