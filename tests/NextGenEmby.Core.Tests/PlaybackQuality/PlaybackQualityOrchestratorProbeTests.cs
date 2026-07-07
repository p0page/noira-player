using System.Linq;
using System.Threading.Tasks;
using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityOrchestratorProbeTests
{
    [Fact]
    public async Task RunAsync_Executes_Core_Lifecycle_And_Returns_Model_Consumable_Report()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/core-probe-sdr-timeline-tracks",
            Category = "stable",
            Severity = "high",
            Stability = "stable",
            Uri = "emby://quality-cases/core-probe-sdr-timeline-tracks",
            ItemId = "quality-case-core-probe",
            MediaSourceId = "quality-source-core-probe",
            StartPositionTicks = 600_000_000,
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 1920,
                Height = 1080,
                FrameRate = 60.0,
                HdrKind = "Sdr",
                IsHdr = false,
                IsDirectPlayable = true,
                HdrOutput = "Sdr",
                DxgiInput = "YCBCR_STUDIO_G22_LEFT_P709",
                DxgiOutput = "RGB_FULL_G22_NONE_P709",
                MaxStartupDurationMs = 5000.0,
                MinRenderedVideoFrames = 120,
                MaxDroppedFrames = 0,
                MaxFrameGapMs = 40.0,
                MaxRenderIntervalMsP95 = 20.0,
                MaxRenderIntervalMsP99 = 25.0,
                MaxAudioVideoDriftMsP95 = 80.0,
                MaxSeekPositionErrorMs = 500.0,
                MaxVideoStarvedPasses = 0,
                MaxAudioStarvedPasses = 0,
                RequireValidatedConversion = true
            }
        };
        referenceCase.Purpose.Add("sdr-smoke");
        referenceCase.Purpose.Add("timeline");
        referenceCase.Purpose.Add("tracks");
        referenceCase.Purpose.Add("audio-switch");
        referenceCase.Purpose.Add("subtitle-switch");
        referenceCase.Purpose.Add("frame-pacing");
        referenceCase.Purpose.Add("av-sync");
        referenceCase.Purpose.Add("buffering");

        var result = await PlaybackQualityOrchestratorProbe.RunAsync(
            referenceCase,
            new PlaybackQualityEnvironment
            {
                CollectorVersion = "core-probe-test",
                PlayerCoreVersion = "NextGenEmby.Core.Tests",
                SourceRevision = "test-revision",
                BuildConfiguration = "Debug"
            });

        Assert.Equal("local/core-probe-sdr-timeline-tracks", result.CaseMetadata.CaseId);
        Assert.Equal("stable", result.CaseMetadata.Category);
        Assert.Equal("high", result.CaseMetadata.Severity);
        Assert.Equal("stable", result.CaseMetadata.Stability);
        Assert.Equal("pass", result.Report.Result);
        Assert.Equal("quality-case-core-probe", result.Report.Source.ItemId);
        Assert.Equal("quality-source-core-probe", result.Report.Source.MediaSourceId);
        Assert.Equal("hevc", result.Report.Source.Codec);
        Assert.Equal(1920, result.Report.Source.Width);
        Assert.Equal(1080, result.Report.Source.Height);
        Assert.Equal(60.0, result.Report.Source.FrameRate);
        Assert.Equal("Sdr", result.Report.ColorPipeline.ActualHdrOutput);
        Assert.Equal("validated", result.Report.ColorPipeline.ConversionStatus);
        Assert.Equal(600_000_000, result.Report.Position.RequestedStartPositionTicks);
        Assert.Equal(900_000_000, result.Report.Position.SeekTargetPositionTicks);
        Assert.Equal(900_000_000, result.Report.Position.ActualPositionTicks);
        Assert.Equal(0, result.Report.Position.SeekPositionErrorMs);
        Assert.True(result.Report.Startup.StartupDurationMs > 0);
        Assert.Equal("core-probe:returned-snapshot", result.Report.RuntimeMetrics.ProviderStatus);
        Assert.Equal("core-probe:returned-snapshot", result.ModelAnalysis.RuntimeMetrics.ProviderStatus);
        Assert.True(result.Report.Timing.RenderedVideoFrames >= 120);
        Assert.Equal(1, result.Report.Tracks.VideoTrackCount);
        Assert.Equal(2, result.Report.Tracks.AudioTrackCount);
        Assert.Equal(1, result.Report.Tracks.SubtitleTrackCount);
        Assert.Equal(1, result.Report.Tracks.SelectedAudioStreamIndex);
        Assert.Equal(3, result.Report.Tracks.SelectedSubtitleStreamIndex);
        var lifecycleOperations = result.Report.Lifecycle.Events
            .Select(item => item.Operation)
            .ToArray();
        Assert.Contains("load", lifecycleOperations);
        Assert.Contains("play", lifecycleOperations);
        Assert.Contains("pause", lifecycleOperations);
        Assert.Contains("resume", lifecycleOperations);
        Assert.Contains("seek", lifecycleOperations);
        Assert.Contains("stop", lifecycleOperations);
        Assert.All(result.Report.Lifecycle.Events, item => Assert.Equal("observed", item.Status));
        Assert.Equal("observed", result.ModelAnalysis.Lifecycle.Status);
        Assert.Contains("lifecycle.load", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("lifecycle.play", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("lifecycle.pause", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("lifecycle.resume", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("lifecycle.seek", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("lifecycle.stop", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains(
            "core-probe: PlaybackOrchestrator lifecycle was executed with an in-process diagnostic backend",
            result.Report.Limitations);
        Assert.Contains(
            "core-probe: native playback graph, decoder, renderer, network I/O, and HDMI output were not opened",
            result.Report.Limitations);
        Assert.Contains("position.seekPositionErrorMs", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("tracks.selectedAudioStreamIndex", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("tracks.selectedSubtitleStreamIndex", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("tracks.video.isDefault", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("tracks.video.isForced", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("tracks.audio.isDefault", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("tracks.audio.isForced", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("tracks.subtitles.isDefault", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains("tracks.subtitles.isForced", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains(
            "core-probe: native playback graph, decoder, renderer, network I/O, and HDMI output were not opened",
            result.ModelAnalysis.Limitations);
    }

    [Fact]
    public async Task RunAsync_Records_EndOfStream_Lifecycle_When_Case_Requires_EndOfStream()
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = "local/core-probe-end-of-stream",
            Category = "challenge",
            Severity = "medium",
            Stability = "stable",
            Uri = "emby://quality-cases/core-probe-end-of-stream",
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 1920,
                Height = 1080,
                FrameRate = 60.0,
                HdrKind = "Sdr",
                IsHdr = false,
                IsDirectPlayable = true,
                HdrOutput = "Sdr",
                DxgiInput = "YCBCR_STUDIO_G22_LEFT_P709",
                DxgiOutput = "RGB_FULL_G22_NONE_P709",
                MaxStartupDurationMs = 5000.0,
                MaxVideoStarvedPasses = 0,
                MaxAudioStarvedPasses = 0,
                RequireValidatedConversion = true
            }
        };
        referenceCase.Purpose.Add("end-of-stream");

        var result = await PlaybackQualityOrchestratorProbe.RunAsync(referenceCase);

        Assert.Contains(result.Report.Lifecycle.Events, item =>
            item.Operation == "endOfStream" &&
            item.Status == "observed");
        Assert.Contains("lifecycle.endOfStream", result.ModelAnalysis.EvidenceSignals);
        Assert.Contains(
            "core-probe: end-of-stream is a diagnostic lifecycle marker, not proof of natural media EOF",
            result.Report.Limitations);
    }
}
