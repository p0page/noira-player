using System;
using System.IO;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityDecoderEagainEvidenceContractTests
{
    private static readonly string[] EvidenceNames =
    {
        "VideoDecoderSendPacketEagainCount",
        "VideoDecoderDoubleEagainRetryCount",
        "VideoDecoderDoubleEagainRecoveryCount",
        "VideoDecoderDoubleEagainExhaustedCount"
    };

    [Fact]
    public void Decoder_Eagain_Evidence_Is_Projected_Across_Native_App_Core_And_Headless()
    {
        var files = new[]
        {
            Read("src", "NoiraPlayer.Native", "Media", "PlaybackQualityMetrics.h"),
            Read("src", "NoiraPlayer.Native", "NativePlaybackEngine.idl"),
            Read("src", "NoiraPlayer.Native", "NativePlaybackQualityMetrics.h"),
            Read("src", "NoiraPlayer.Native", "NativePlaybackEngine.cpp"),
            Read("src", "NoiraPlayer.App", "Playback", "WinRtNativePlaybackEngine.cs"),
            Read("src", "NoiraPlayer.Core", "PlaybackQuality", "PlaybackQualityMetricsSnapshot.cs"),
            Read("src", "NoiraPlayer.Core", "PlaybackQuality", "PlaybackQualityReport.cs"),
            Read("src", "NoiraPlayer.Core", "PlaybackQuality", "PlaybackQualityReportMapper.cs")
        };

        foreach (var evidenceName in EvidenceNames)
        {
            foreach (var file in files)
            {
                Assert.Contains(evidenceName, file, StringComparison.Ordinal);
            }
        }

        var helper = Read("tests", "NoiraPlayer.Native.Tests", "NativePlaybackGraphHeadlessSmokeTests.cpp");
        var parser = Read("tools", "NoiraPlayer.PlaybackQuality.Headless", "Program.cs");
        foreach (var evidenceName in EvidenceNames)
        {
            var jsonName = char.ToLowerInvariant(evidenceName[0]) + evidenceName.Substring(1);
            Assert.Contains(jsonName + "=", helper, StringComparison.Ordinal);
            Assert.Contains("\"" + jsonName + "\"", parser, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Decoder_Eagain_Evidence_Uses_A_New_Evaluation_Version()
    {
        var composer = Read(
            "src",
            "NoiraPlayer.Core",
            "PlaybackQuality",
            "PlaybackQualityReportComposer.cs");

        Assert.Contains(
            "CurrentEvaluationVersion = \"playback-quality-v0.17\"",
            composer,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Mapper_Preserves_Decoder_Eagain_Evidence()
    {
        var report = new PlaybackQualityReport();
        var metrics = new PlaybackQualityMetricsSnapshot
        {
            VideoDecoderSendPacketEagainCount = 7,
            VideoDecoderDoubleEagainRetryCount = 4,
            VideoDecoderDoubleEagainRecoveryCount = 1,
            VideoDecoderDoubleEagainExhaustedCount = 0
        };

        PlaybackQualityReportMapper.ApplyMetrics(report, metrics);

        Assert.Equal(7UL, report.Timing.VideoDecoderSendPacketEagainCount);
        Assert.Equal(4UL, report.Timing.VideoDecoderDoubleEagainRetryCount);
        Assert.Equal(1UL, report.Timing.VideoDecoderDoubleEagainRecoveryCount);
        Assert.Equal(0UL, report.Timing.VideoDecoderDoubleEagainExhaustedCount);
    }

    [Fact]
    public void Exhausted_Double_Eagain_Is_A_Player_Core_Failure()
    {
        var report = new PlaybackQualityReport();
        report.Timing.VideoDecoderSendPacketEagainCount = 5;
        report.Timing.VideoDecoderDoubleEagainRetryCount = 4;
        report.Timing.VideoDecoderDoubleEagainExhaustedCount = 1;

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.Equal("fail", report.Result);
        var check = Assert.Single(
            report.Checks,
            candidate => candidate.Signal == "timing.videoDecoderDoubleEagainExhaustedCount");
        Assert.Equal("fail", check.Status);
        Assert.Equal("decoder-recovery", check.FailureArea);
        Assert.Equal(PlaybackQualityFailureClassification.PlayerCoreBug, check.FailureClass);
    }

    [Fact]
    public void NonNative_Report_Does_Not_Claim_Zero_Exhaustion_As_Playback_Evidence()
    {
        var report = new PlaybackQualityReport();
        report.Execution.EvidenceLevel = PlaybackQualityEvidenceLevel.Orchestration;

        PlaybackQualityEvaluator.Evaluate(report);

        Assert.DoesNotContain(
            report.Checks,
            candidate => candidate.Signal == "timing.videoDecoderDoubleEagainExhaustedCount");
    }

    private static string Read(params string[] relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(relativePath)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NoiraPlayer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
