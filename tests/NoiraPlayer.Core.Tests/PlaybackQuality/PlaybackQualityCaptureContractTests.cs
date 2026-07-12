using System;
using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Playback;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityCaptureContractTests
{
    [Theory]
    [InlineData("local/sdr-smoke", "local/sdr-smoke.json")]
    [InlineData("local/sdr-smoke.json", "local/sdr-smoke.json")]
    [InlineData(@"local\sdr-smoke", "local/sdr-smoke.json")]
    public void GetReportRelativePath_Uses_RunId_As_Report_Set_Key(
        string runId,
        string expectedPath)
    {
        var path = PlaybackQualityCapturedReportPath.GetReportRelativePath(runId);

        Assert.Equal(expectedPath, path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("../escape")]
    [InlineData("local/../escape")]
    [InlineData("/absolute")]
    [InlineData(@"C:\absolute")]
    public void GetReportRelativePath_Rejects_Unsafe_RunIds(string runId)
    {
        Assert.Throws<ArgumentException>(() =>
            PlaybackQualityCapturedReportPath.GetReportRelativePath(runId));
    }

    [Fact]
    public void CreateReferenceCase_Preserves_Playback_Descriptor_And_Expected_Evidence()
    {
        var descriptor = CreateDescriptor();
        var expected = new PlaybackQualityExpected
        {
            Codec = "hevc",
            Width = 3840,
            Height = 2160,
            FrameRate = 23.976,
            HdrKind = "Hdr10",
            HdrOutput = "Hdr10",
            MaxDroppedFrames = 1,
            MaxAudioVideoDriftMsP95 = 40
        };

        var referenceCase = PlaybackQualityCaptureReferenceCaseFactory.Create(
            "local/hdr10-smoke",
            descriptor,
            expected,
            scenario: PlaybackQualityExecutionScenario.SubtitleSwitch,
            category: "challenge",
            severity: "high",
            stability: "stable");

        Assert.Equal("local/hdr10-smoke", referenceCase.CaseId);
        Assert.Equal("challenge", referenceCase.Category);
        Assert.Equal("high", referenceCase.Severity);
        Assert.Equal("stable", referenceCase.Stability);
        Assert.Equal(PlaybackQualityExecutionScenario.SubtitleSwitch, referenceCase.ExecutionRequirement.Scenario);
        Assert.Equal("https://example.invalid/hdr10.mp4", referenceCase.Uri);
        Assert.Equal("item-1", referenceCase.ItemId);
        Assert.Equal("source-1", referenceCase.MediaSourceId);
        Assert.Equal(123_000_000, referenceCase.StartPositionTicks);
        Assert.Equal("hevc", referenceCase.Expected.Codec);
        Assert.Equal(3840, referenceCase.Expected.Width);
        Assert.Equal(23.976, referenceCase.Expected.FrameRate);
        Assert.Equal("Hdr10", referenceCase.Expected.HdrOutput);
        Assert.Equal(1, referenceCase.Expected.MaxDroppedFrames);
        Assert.Equal(40, referenceCase.Expected.MaxAudioVideoDriftMsP95);
    }

    [Fact]
    public void CreateReferenceCase_Preserves_Manifest_Source_Locator()
    {
        var referenceCase = PlaybackQualityCaptureReferenceCaseFactory.Create(
            "private/item/pause-resume",
            CreateDescriptor(),
            expected: null,
            uri: "emby://items/item-1");

        Assert.Equal("emby://items/item-1", referenceCase.Uri);
    }

    [Fact]
    public void CreateReferenceCase_For_Failed_Launch_Preserves_Command_Evidence()
    {
        var referenceCase = PlaybackQualityCaptureReferenceCaseFactory.Create(
            "local/open-failed",
            itemId: "item-1",
            mediaSourceId: "source-1",
            startPositionTicks: 456_000_000,
            forceSdrOutput: true,
            expected: new PlaybackQualityExpected
            {
                Codec = "hevc",
                HdrKind = "Hdr10",
                HdrOutput = "Hdr10"
            },
            scenario: PlaybackQualityExecutionScenario.PauseResume,
            category: "stable",
            severity: "high",
            stability: "quarantine");

        Assert.Equal("local/open-failed", referenceCase.CaseId);
        Assert.Equal("item-1", referenceCase.ItemId);
        Assert.Equal("source-1", referenceCase.MediaSourceId);
        Assert.Equal(456_000_000, referenceCase.StartPositionTicks);
        Assert.True(referenceCase.ForceSdrOutput);
        Assert.Equal("stable", referenceCase.Category);
        Assert.Equal("high", referenceCase.Severity);
        Assert.Equal("quarantine", referenceCase.Stability);
        Assert.Equal(PlaybackQualityExecutionScenario.PauseResume, referenceCase.ExecutionRequirement.Scenario);
        Assert.Equal("hevc", referenceCase.Expected.Codec);
        Assert.Equal("Hdr10", referenceCase.Expected.HdrOutput);
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

        return new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 123_000_000);
    }
}
