using System.Collections.Generic;
using System.Linq;
using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Playback;
using NoiraPlayer.Core.PlaybackQuality;
using Xunit;

namespace NoiraPlayer.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityReferenceManifestTests
{
    [Fact]
    public void Validate_Accepts_Explicit_Sdr_Display_Fallback()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("hdr/environment-aware", tier: 2, purpose: "hdr-output");
        referenceCase.Expected.SdrDisplayFallback = new PlaybackQualityColorExpected
        {
            HdrOutput = "Sdr",
            DxgiOutput = "RGB_FULL_G22_NONE_P709",
            RequiredConversionStatus = "tone-mapped-hable"
        };
        referenceCase.Expected.SdrDisplayFallback.DxgiInputAnyOf.Add("YCBCR_STUDIO_G22_LEFT_P2020");
        referenceCase.Expected.SdrDisplayFallback.DxgiInputAnyOf.Add("YCBCR_STUDIO_G22_TOPLEFT_P2020");
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Null(result.Cases[0].Expected.SdrDisplayFallback!.IsTenBitSwapChain);
        Assert.Equal(2, result.Cases[0].Expected.SdrDisplayFallback!.DxgiInputAnyOf.Count);
    }

    [Fact]
    public void Validate_Rejects_Incomplete_Sdr_Display_Fallback()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("hdr/incomplete-fallback", tier: 2, purpose: "hdr-output");
        referenceCase.Expected.SdrDisplayFallback = new PlaybackQualityColorExpected();
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.sdrDisplayFallback.hdrOutput.missing");
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.sdrDisplayFallback.dxgiInputAnyOf.missing");
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.sdrDisplayFallback.requiredConversionStatus.missing");
    }

    [Fact]
    public void Validate_Rejects_Duplicate_Sdr_Display_Fallback_Dxgi_Inputs()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("hdr/duplicate-fallback-input", tier: 2, purpose: "hdr-output");
        referenceCase.Expected.SdrDisplayFallback = new PlaybackQualityColorExpected
        {
            HdrOutput = "Sdr",
            DxgiOutput = "RGB_FULL_G22_NONE_P709",
            RequiredConversionStatus = "tone-mapped-hable"
        };
        referenceCase.Expected.SdrDisplayFallback.DxgiInputAnyOf.Add("YCBCR_STUDIO_G22_LEFT_P2020");
        referenceCase.Expected.SdrDisplayFallback.DxgiInputAnyOf.Add("YCBCR_STUDIO_G22_LEFT_P2020");
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.sdrDisplayFallback.dxgiInputAnyOf.duplicate");
    }

    [Fact]
    public void Validate_Accepts_Unique_Cases_With_Expected_Source_Metadata()
    {
        var manifest = new PlaybackQualityReferenceManifest
        {
            SchemaVersion = 1
        };
        manifest.Cases.Add(CreateCase(
            "netflix/chimera-4k-2398-hdr-pq",
            tier: 2,
            purpose: "hdr-output"));
        manifest.Cases.Add(CreateCase(
            "jellyfin/dv-profile5-hevc-4k",
            tier: 3,
            purpose: "dv-reject"));

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.Equal(PlaybackQualityRunResult.CurrentEvaluationVersion, result.EvaluationVersion);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.CaseCount);
        Assert.Contains(2, result.Tiers);
        Assert.Contains(3, result.Tiers);
        Assert.Contains("hdr-output", result.Purposes);
        Assert.Contains("dv-reject", result.Purposes);
        Assert.Contains(result.Cases, referenceCase =>
            referenceCase.CaseId == "netflix/chimera-4k-2398-hdr-pq" &&
            referenceCase.Uri == "https://example.invalid/netflix/chimera-4k-2398-hdr-pq.mp4" &&
            referenceCase.Tier == 2 &&
            referenceCase.Purpose.Contains("hdr-output") &&
            referenceCase.Expected.Codec == "hevc" &&
            referenceCase.Expected.FrameRate == 23.976 &&
            referenceCase.Expected.HdrKind == "Hdr10");
        Assert.Equal("incomplete", result.Coverage.Status);
        Assert.False(result.Coverage.IsCoreEvaluationReady);
        Assert.Contains("sdr-smoke", result.Coverage.MissingPurposes);
        Assert.Contains("dv-fallback", result.Coverage.MissingPurposes);
        Assert.Contains("timeline", result.Coverage.MissingPurposes);
        Assert.Contains("tracks", result.Coverage.MissingPurposes);
        Assert.Contains("subtitles", result.Coverage.MissingPurposes);
        Assert.Contains("end-of-stream", result.Coverage.MissingPurposes);
        Assert.Contains("error-handling", result.Coverage.MissingPurposes);
        Assert.Contains("reference manifest is missing required playback quality purposes", result.Coverage.Reasons);
        Assert.Contains("Add reference cases", result.Coverage.SuggestedNextAction);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_Rejects_Invalid_Interaction_Recovery_Threshold(double threshold)
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "tracks/audio-switch-recovery",
            tier: 1,
            purpose: "audio-switch");
        referenceCase.ExecutionRequirement.Scenario = "audio-switch";
        referenceCase.Expected.MaxInteractionRecoveryDurationMs = threshold;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.maxInteractionRecoveryDurationMs.invalid" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "expected.maxInteractionRecoveryDurationMs");
    }

    [Fact]
    public void Validate_Accepts_Positive_Interaction_Recovery_Threshold_For_Interaction_Case()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "tracks/audio-switch-recovery",
            tier: 1,
            purpose: "audio-switch");
        referenceCase.ExecutionRequirement.Scenario = "audio-switch";
        referenceCase.Expected.MaxInteractionRecoveryDurationMs = 2000;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_Rejects_Invalid_Seek_Recovery_Threshold(double threshold)
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("timeline/seek-recovery", tier: 1, purpose: "timeline");
        referenceCase.ExecutionRequirement.Scenario = "timeline";
        referenceCase.Expected.MaxSeekRecoveryDurationMs = threshold;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.maxSeekRecoveryDurationMs.invalid" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "expected.maxSeekRecoveryDurationMs");
    }

    [Fact]
    public void Validate_Accepts_Positive_Seek_Recovery_Threshold_For_Timeline_Case()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("timeline/seek-recovery", tier: 1, purpose: "timeline");
        referenceCase.ExecutionRequirement.Scenario = "timeline";
        referenceCase.Expected.MaxSeekRecoveryDurationMs = 2000;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Rejects_Timeline_Case_Without_Explicit_Seek_Target()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("timeline/missing-target", tier: 1, purpose: "timeline");
        referenceCase.SeekTargetPositionTicks = null;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.seek-target.missing" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "seekTargetPositionTicks");
    }

    [Fact]
    public void Validate_Rejects_Timeline_Seek_Target_Equal_To_Start_Position()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("timeline/no-op-target", tier: 1, purpose: "timeline");
        referenceCase.StartPositionTicks = 600_000_000;
        referenceCase.SeekTargetPositionTicks = 600_000_000;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.seek-target.no-op" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "seekTargetPositionTicks");
    }

    [Fact]
    public void Validate_Accepts_Backward_Seek_To_Zero_And_Preserves_Target()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("timeline/backward-to-zero", tier: 1, purpose: "timeline");
        referenceCase.StartPositionTicks = 600_000_000;
        referenceCase.SeekTargetPositionTicks = 0;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Equal(0, Assert.Single(result.Cases).SeekTargetPositionTicks);
    }

    [Fact]
    public void Validate_Rejects_Seek_Target_Outside_Timeline_Scenario()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("playback/unused-seek-target", tier: 1, purpose: "sdr-smoke");
        referenceCase.SeekTargetPositionTicks = 900_000_000;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.seek-target.scenario.invalid" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "seekTargetPositionTicks");
    }

    [Fact]
    public void Validate_Rejects_Interaction_Recovery_Threshold_For_Playback_Case()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "playback/not-an-interaction",
            tier: 1,
            purpose: "sdr-smoke");
        referenceCase.ExecutionRequirement.Scenario = "playback";
        referenceCase.Expected.MaxInteractionRecoveryDurationMs = 2000;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.maxInteractionRecoveryDurationMs.scenario.invalid" &&
            error.Signal == "expected.maxInteractionRecoveryDurationMs");
    }

    [Fact]
    public void RequiredSignalPolicy_Requires_Interaction_Recovery_When_Threshold_Is_Configured()
    {
        var referenceCase = CreateCase(
            "tracks/audio-switch-recovery",
            tier: 1,
            purpose: "audio-switch");
        referenceCase.ExecutionRequirement.Scenario = "audio-switch";
        referenceCase.Expected.MaxInteractionRecoveryDurationMs = 2000;

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("interaction.recoveryDurationMs", requiredSignals);
        Assert.Contains("interaction.operationDurationMs", requiredSignals);
        Assert.Contains("interaction.positionDeltaTicks", requiredSignals);
        Assert.Contains("interaction.submittedAudioFrameDelta", requiredSignals);
    }

    [Fact]
    public void RequiredSignalPolicy_Separates_Subtitle_Playback_Recovery_From_Cue_Rendering()
    {
        var referenceCase = CreateCase(
            "subtitles/subtitle-switch-recovery",
            tier: 1,
            purpose: "subtitle-switch");
        referenceCase.ExecutionRequirement.Scenario = "subtitle-switch";
        referenceCase.Expected.MaxInteractionRecoveryDurationMs = 2000;

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("interaction.recoveryDurationMs", requiredSignals);
        Assert.Contains("interaction.cueRenderDurationMs", requiredSignals);
        Assert.Contains("interaction.renderedVideoFrameDelta", requiredSignals);
        Assert.Contains("interaction.subtitleCueRenderCountDelta", requiredSignals);
    }

    [Fact]
    public void Validate_Reports_Ready_Corpus_Coverage_When_Core_Risk_Purposes_Are_Present()
    {
        var manifest = new PlaybackQualityReferenceManifest
        {
            SchemaVersion = 1
        };
        manifest.Cases.Add(CreateCase(
            "sdr/1080p-h264-24",
            tier: 0,
            purpose: "sdr-smoke"));
        manifest.Cases.Add(CreateCase(
            "hdr/hdr10-2398",
            tier: 1,
            purpose: "hdr-output"));
        manifest.Cases.Add(CreateCase(
            "hdr/hdr10-force-sdr",
            tier: 1,
            purpose: "hdr-force-sdr"));
        manifest.Cases.Add(CreateCase(
            "dv/profile5",
            tier: 2,
            purpose: "dv-reject"));
        manifest.Cases.Add(CreateCase(
            "dv/profile8-hdr10",
            tier: 2,
            purpose: "dv-fallback"));
        manifest.Cases.Add(CreateCase(
            "cadence/23976",
            tier: 1,
            purpose: "cadence-23.976"));
        manifest.Cases.Add(CreateCase(
            "timing/frame-pacing",
            tier: 1,
            purpose: "frame-pacing"));
        manifest.Cases.Add(CreateCase(
            "sync/av-sync",
            tier: 1,
            purpose: "av-sync"));
        manifest.Cases.Add(CreateCase(
            "network/buffering",
            tier: 1,
            purpose: "buffering"));
        manifest.Cases.Add(CreateCase(
            "timeline/seek-position",
            tier: 1,
            purpose: "timeline"));
        manifest.Cases.Add(CreateCase(
            "tracks/discovery",
            tier: 1,
            purpose: "tracks"));
        manifest.Cases.Add(CreateCase(
            "subtitles/discovery",
            tier: 1,
            purpose: "subtitles"));
        manifest.Cases.Add(CreateCase(
            "lifecycle/end-of-stream",
            tier: 1,
            purpose: "end-of-stream"));
        manifest.Cases.Add(CreateCase(
            "errors/missing-file",
            tier: 1,
            purpose: "error-handling"));

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Equal("ready", result.Coverage.Status);
        Assert.True(result.Coverage.IsCoreEvaluationReady);
        Assert.Empty(result.Coverage.MissingPurposes);
        Assert.Contains("hdr-output", result.Coverage.CoveredPurposes);
        Assert.Contains("frame-pacing", result.Coverage.CoveredPurposes);
        Assert.Contains("timeline", result.Coverage.CoveredPurposes);
        Assert.Contains("tracks", result.Coverage.CoveredPurposes);
        Assert.Contains("subtitles", result.Coverage.CoveredPurposes);
        Assert.Contains("end-of-stream", result.Coverage.CoveredPurposes);
        Assert.Contains("error-handling", result.Coverage.CoveredPurposes);
        Assert.Contains("reference manifest covers required playback quality purposes", result.Coverage.Reasons);
    }

    [Fact]
    public void Validate_Does_Not_Count_Quarantine_Cases_As_Executable_Coverage()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var quarantineCase = CreateCase("coverage/quarantine-only", 1, "end-of-stream");
        quarantineCase.Category = "quarantine";
        manifest.Cases.Add(quarantineCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.Equal("incomplete", result.Coverage.Status);
        Assert.Contains("end-of-stream", result.Coverage.MissingPurposes);
        Assert.DoesNotContain("end-of-stream", result.Coverage.CoveredPurposes);
    }

    [Fact]
    public void RequiredSignals_Include_NonSensitive_Direct_Stream_Locator_Evidence()
    {
        var referenceCase = CreateCase(
            "source/direct-stream-protocol",
            tier: 1,
            purpose: "sdr-smoke");

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("source.videoMetadataProvider", requiredSignals);
        Assert.Contains("source.videoMetadataStatus", requiredSignals);
        Assert.Contains("source.hasDirectStreamUrl", requiredSignals);
        Assert.Contains("source.directStreamProtocol", requiredSignals);
    }

    [Fact]
    public void RequiredSignals_For_SubtitleSwitch_Require_Operation_And_Rendered_Cue()
    {
        var referenceCase = CreateCase(
            "subtitles/pgs-switch",
            tier: 1,
            purpose: "subtitle-switch");
        referenceCase.ExecutionRequirement.Scenario =
            PlaybackQualityExecutionScenario.SubtitleSwitch;

        var requiredSignals =
            PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("lifecycle.subtitle-switch", requiredSignals);
        Assert.Contains("tracks.selectedSubtitleStreamIndex", requiredSignals);
        Assert.Contains("tracks.subtitleCueRenderCount", requiredSignals);

        var report = CreateReport(
            referenceCase.CaseId,
            "hevc",
            3840,
            2160,
            23.976,
            "Sdr");
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "subtitle-switch",
            Status = "success"
        });
        report.Tracks.SelectedSubtitleStreamIndex = 3;

        Assert.False(PlaybackQualityRequiredSignalPolicy.HasReportSignal(
            report,
            "tracks.subtitleCueRenderCount"));

        report.Tracks.SubtitleCueRenderCount = 1;
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(
            report,
            "tracks.subtitleCueRenderCount"));
    }

    [Fact]
    public void SignalCatalog_Includes_Source_Color_Metadata_Evidence()
    {
        var reportSignals = new HashSet<string>(
            PlaybackQualitySignalCatalog.ReportSignals.Select(signal => signal.Signal));

        Assert.Contains("source.videoRange", reportSignals);
        Assert.Contains("source.colorPrimaries", reportSignals);
        Assert.Contains("source.colorTransfer", reportSignals);
        Assert.Contains("source.colorSpace", reportSignals);
    }

    [Fact]
    public void SignalCatalog_Includes_Present_Duration_Timing_Evidence()
    {
        var reportSignals = new HashSet<string>(
            PlaybackQualitySignalCatalog.ReportSignals.Select(signal => signal.Signal));

        Assert.Contains("timing.presentDurationMsP50", reportSignals);
        Assert.Contains("timing.presentDurationMsP95", reportSignals);
        Assert.Contains("timing.presentDurationMsP99", reportSignals);
        Assert.Contains("timing.presentDurationMsMax", reportSignals);
    }

    [Fact]
    public void SignalCatalog_Includes_Video_Pipeline_Stage_Timing_Evidence()
    {
        var reportSignals = new HashSet<string>(
            PlaybackQualitySignalCatalog.ReportSignals.Select(signal => signal.Signal));

        Assert.Contains("timing.videoDecodeDurationMsP50", reportSignals);
        Assert.Contains("timing.videoDecodeDurationMsP95", reportSignals);
        Assert.Contains("timing.videoDecodeDurationMsP99", reportSignals);
        Assert.Contains("timing.videoDecodeDurationMsMax", reportSignals);
        Assert.Contains("timing.videoDecodeDeviceMode", reportSignals);
        Assert.Contains("timing.videoDecodeSynchronizationMode", reportSignals);
        Assert.Contains("timing.videoDecodeWorkerActive", reportSignals);
        Assert.Contains("timing.videoDecodeQueueCapacity", reportSignals);
        Assert.Contains("timing.videoDecodeQueueMaxDepth", reportSignals);
        Assert.Contains("timing.videoDecodeQueueProducerWaitCount", reportSignals);
        Assert.Contains("timing.videoDecodePacketReadDurationMsP50", reportSignals);
        Assert.Contains("timing.videoDecodePacketReadDurationMsP95", reportSignals);
        Assert.Contains("timing.videoDecodeSendPacketDurationMsP50", reportSignals);
        Assert.Contains("timing.videoDecodeSendPacketDurationMsP95", reportSignals);
        Assert.Contains("timing.videoDecodeReceiveFrameDurationMsP50", reportSignals);
        Assert.Contains("timing.videoDecodeReceiveFrameDurationMsP95", reportSignals);
        Assert.Contains("timing.videoDecodeFrameMaterializeDurationMsP50", reportSignals);
        Assert.Contains("timing.videoDecodeFrameMaterializeDurationMsP95", reportSignals);
        Assert.Contains("timing.videoRenderDurationMsP50", reportSignals);
        Assert.Contains("timing.videoRenderDurationMsP95", reportSignals);
        Assert.Contains("timing.videoRenderDurationMsP99", reportSignals);
        Assert.Contains("timing.videoRenderDurationMsMax", reportSignals);
        Assert.Contains("timing.videoRenderDirectCopyFrameCount", reportSignals);
        Assert.Contains("timing.videoRenderVideoProcessorFrameCount", reportSignals);
        Assert.Contains("timing.videoRenderBgraFrameCount", reportSignals);
        Assert.Contains("timing.videoRenderPostProcessFrameCount", reportSignals);
        Assert.Contains("timing.videoProcessorSetupCpuSampleCount", reportSignals);
        Assert.Contains("timing.videoProcessorSetupCpuDurationMsP50", reportSignals);
        Assert.Contains("timing.videoProcessorSetupCpuDurationMsP95", reportSignals);
        Assert.Contains("timing.videoProcessorSetupCpuDurationMsP99", reportSignals);
        Assert.Contains("timing.videoProcessorSetupCpuDurationMsMax", reportSignals);
        Assert.Contains("timing.videoProcessorViewTargetCpuSampleCount", reportSignals);
        Assert.Contains("timing.videoProcessorViewTargetCpuDurationMsP50", reportSignals);
        Assert.Contains("timing.videoProcessorViewTargetCpuDurationMsP95", reportSignals);
        Assert.Contains("timing.videoProcessorViewTargetCpuDurationMsP99", reportSignals);
        Assert.Contains("timing.videoProcessorViewTargetCpuDurationMsMax", reportSignals);
        Assert.Contains("timing.videoProcessorClearCpuSampleCount", reportSignals);
        Assert.Contains("timing.videoProcessorClearCpuDurationMsP50", reportSignals);
        Assert.Contains("timing.videoProcessorClearCpuDurationMsP95", reportSignals);
        Assert.Contains("timing.videoProcessorClearCpuDurationMsP99", reportSignals);
        Assert.Contains("timing.videoProcessorClearCpuDurationMsMax", reportSignals);
        Assert.Contains("timing.videoProcessorBltCpuSampleCount", reportSignals);
        Assert.Contains("timing.videoProcessorBltCpuDurationMsP50", reportSignals);
        Assert.Contains("timing.videoProcessorBltCpuDurationMsP95", reportSignals);
        Assert.Contains("timing.videoProcessorBltCpuDurationMsP99", reportSignals);
        Assert.Contains("timing.videoProcessorBltCpuDurationMsMax", reportSignals);
        Assert.Contains("timing.videoProcessorPostProcessCpuSampleCount", reportSignals);
        Assert.Contains("timing.videoProcessorPostProcessCpuDurationMsP50", reportSignals);
        Assert.Contains("timing.videoProcessorPostProcessCpuDurationMsP95", reportSignals);
        Assert.Contains("timing.videoProcessorPostProcessCpuDurationMsP99", reportSignals);
        Assert.Contains("timing.videoProcessorPostProcessCpuDurationMsMax", reportSignals);
    }

    [Fact]
    public void RequiredSignals_For_Native_Playback_Include_Complete_Video_Render_Phase_Evidence()
    {
        var referenceCase = CreateCase("render/phases", 1, "frame-pacing");

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        var expectedSignals = new[]
        {
            "timing.videoRenderDirectCopyFrameCount",
            "timing.videoRenderVideoProcessorFrameCount",
            "timing.videoRenderBgraFrameCount",
            "timing.videoRenderPostProcessFrameCount",
            "timing.videoProcessorSetupCpuSampleCount",
            "timing.videoProcessorSetupCpuDurationMsP50",
            "timing.videoProcessorSetupCpuDurationMsP95",
            "timing.videoProcessorSetupCpuDurationMsP99",
            "timing.videoProcessorSetupCpuDurationMsMax",
            "timing.videoProcessorViewTargetCpuSampleCount",
            "timing.videoProcessorViewTargetCpuDurationMsP50",
            "timing.videoProcessorViewTargetCpuDurationMsP95",
            "timing.videoProcessorViewTargetCpuDurationMsP99",
            "timing.videoProcessorViewTargetCpuDurationMsMax",
            "timing.videoProcessorClearCpuSampleCount",
            "timing.videoProcessorClearCpuDurationMsP50",
            "timing.videoProcessorClearCpuDurationMsP95",
            "timing.videoProcessorClearCpuDurationMsP99",
            "timing.videoProcessorClearCpuDurationMsMax",
            "timing.videoProcessorBltCpuSampleCount",
            "timing.videoProcessorBltCpuDurationMsP50",
            "timing.videoProcessorBltCpuDurationMsP95",
            "timing.videoProcessorBltCpuDurationMsP99",
            "timing.videoProcessorBltCpuDurationMsMax",
            "timing.videoProcessorPostProcessCpuSampleCount",
            "timing.videoProcessorPostProcessCpuDurationMsP50",
            "timing.videoProcessorPostProcessCpuDurationMsP95",
            "timing.videoProcessorPostProcessCpuDurationMsP99",
            "timing.videoProcessorPostProcessCpuDurationMsMax"
        };

        Assert.All(expectedSignals, signal => Assert.Contains(signal, requiredSignals));
    }

    [Fact]
    public void SignalCatalog_Includes_Audio_Ahead_Wait_Duration_Timing_Evidence()
    {
        var reportSignals = new HashSet<string>(
            PlaybackQualitySignalCatalog.ReportSignals.Select(signal => signal.Signal));

        Assert.Contains("timing.audioAheadWaitDurationMsP50", reportSignals);
        Assert.Contains("timing.audioAheadWaitDurationMsP95", reportSignals);
        Assert.Contains("timing.audioAheadWaitDurationMsP99", reportSignals);
        Assert.Contains("timing.audioAheadWaitDurationMsMax", reportSignals);
        Assert.Contains("timing.audioAheadWaitTargetMsP50", reportSignals);
        Assert.Contains("timing.audioAheadWaitTargetMsP95", reportSignals);
        Assert.Contains("timing.audioAheadWaitTargetMsP99", reportSignals);
        Assert.Contains("timing.audioAheadWaitTargetMsMax", reportSignals);
        Assert.Contains("timing.audioAheadWaitOversleepMsP50", reportSignals);
        Assert.Contains("timing.audioAheadWaitOversleepMsP95", reportSignals);
        Assert.Contains("timing.audioAheadWaitOversleepMsP99", reportSignals);
        Assert.Contains("timing.audioAheadWaitOversleepMsMax", reportSignals);
        Assert.Contains("timing.audioAheadWaitFinalDeltaAbsMsP50", reportSignals);
        Assert.Contains("timing.audioAheadWaitFinalDeltaAbsMsP95", reportSignals);
        Assert.Contains("timing.audioAheadWaitFinalDeltaAbsMsP99", reportSignals);
        Assert.Contains("timing.audioAheadWaitFinalDeltaAbsMsMax", reportSignals);
        Assert.Contains("timing.videoAheadWaitCount", reportSignals);
        Assert.Contains("timing.audioAheadWaitCount", reportSignals);
        Assert.Contains("timing.videoClockWaitCount", reportSignals);
    }

    [Fact]
    public void SignalCatalog_Includes_Decode_Mode_Timing_Evidence()
    {
        var reportSignals = new HashSet<string>(
            PlaybackQualitySignalCatalog.ReportSignals.Select(signal => signal.Signal));

        Assert.Contains("timing.hardwareDecodedVideoFrames", reportSignals);
        Assert.Contains("timing.softwareDecodedVideoFrames", reportSignals);
    }

    [Fact]
    public void SignalCatalog_Includes_Runtime_Process_Cost_Evidence()
    {
        var reportSignals = new HashSet<string>(
            PlaybackQualitySignalCatalog.ReportSignals.Select(signal => signal.Signal));

        Assert.Contains("runtimeMetrics.processWallClockMs", reportSignals);
        Assert.Contains("runtimeMetrics.processCpuTimeMs", reportSignals);
        Assert.Contains("runtimeMetrics.processCpuUtilizationRatio", reportSignals);
    }

    [Fact]
    public void SignalCatalog_Includes_Requested_Sample_Duration_Evidence()
    {
        var reportSignals = new HashSet<string>(
            PlaybackQualitySignalCatalog.ReportSignals.Select(signal => signal.Signal));

        Assert.Contains("execution.requestedSampleDurationMs", reportSignals);
        Assert.Contains("execution.observedSampleWallClockDurationMs", reportSignals);
    }

    [Fact]
    public void RequiredSignals_Include_Source_Color_Metadata_When_Expected()
    {
        var referenceCase = CreateCase(
            "source/color-metadata",
            tier: 1,
            purpose: "hdr-output");
        referenceCase.Expected.VideoRange = "HDR10";
        referenceCase.Expected.ColorPrimaries = "bt2020";
        referenceCase.Expected.ColorTransfer = "smpte2084";
        referenceCase.Expected.ColorSpace = "bt2020nc";

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("source.videoRange", requiredSignals);
        Assert.Contains("source.colorPrimaries", requiredSignals);
        Assert.Contains("source.colorTransfer", requiredSignals);
        Assert.Contains("source.colorSpace", requiredSignals);
    }

    [Fact]
    public void Validate_Preserves_Optional_Emby_Item_Capture_Metadata()
    {
        var manifest = new PlaybackQualityReferenceManifest
        {
            SchemaVersion = 1
        };
        var referenceCase = CreateCase(
            "emby/007-hdr10",
            tier: 1,
            purpose: "hdr-output");
        referenceCase.ItemId = "item-007";
        referenceCase.MediaSourceId = "source-hdr10";
        referenceCase.StartPositionTicks = 123;
        referenceCase.SeekTargetPositionTicks = 456;
        referenceCase.Purpose.Clear();
        referenceCase.Purpose.Add("timeline");
        referenceCase.ExecutionRequirement.Scenario = PlaybackQualityExecutionScenario.Timeline;
        referenceCase.ForceSdrOutput = true;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Contains(result.Cases, item =>
            item.CaseId == "emby/007-hdr10" &&
            item.ItemId == "item-007" &&
            item.MediaSourceId == "source-hdr10" &&
            item.StartPositionTicks == 123 &&
            item.SeekTargetPositionTicks == 456 &&
            item.ForceSdrOutput);
    }

    [Fact]
    public void Validate_Preserves_And_Validates_Case_Category()
    {
        var manifest = new PlaybackQualityReferenceManifest
        {
            SchemaVersion = 1
        };
        var challengeCase = CreateCase(
            "jellyfin/hdr10-4k60-50m",
            tier: 3,
            purpose: "buffering");
        challengeCase.Category = "challenge";
        manifest.Cases.Add(challengeCase);
        var quarantineCase = CreateCase(
            "local/flaky-network",
            tier: 4,
            purpose: "buffering");
        quarantineCase.Category = "quarantine";
        manifest.Cases.Add(quarantineCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Contains("challenge", result.Categories);
        Assert.Contains("quarantine", result.Categories);
        Assert.Contains(result.Cases, item =>
            item.CaseId == "jellyfin/hdr10-4k60-50m" &&
            item.Category == "challenge");
        Assert.Contains(result.Cases, item =>
            item.CaseId == "local/flaky-network" &&
            item.Category == "quarantine");
    }

    [Fact]
    public void Validate_Preserves_And_Validates_Case_Severity_And_Stability()
    {
        var manifest = new PlaybackQualityReferenceManifest
        {
            SchemaVersion = 1
        };
        var referenceCase = CreateCase(
            "jellyfin/hdr10-critical",
            tier: 1,
            purpose: "hdr-output");
        referenceCase.Severity = "critical";
        referenceCase.Stability = "variable";
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Contains("critical", result.Severities);
        Assert.Contains("variable", result.Stabilities);
        Assert.Contains(result.Cases, item =>
            item.CaseId == "jellyfin/hdr10-critical" &&
            item.Severity == "critical" &&
            item.Stability == "variable");
    }

    [Fact]
    public void Validate_Rejects_Invalid_Case_Severity_And_Stability()
    {
        var manifest = new PlaybackQualityReferenceManifest
        {
            SchemaVersion = 1
        };
        var referenceCase = CreateCase(
            "jellyfin/invalid-triage-fields",
            tier: 1,
            purpose: "hdr-output");
        referenceCase.Severity = "urgent";
        referenceCase.Stability = "sometimes";
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.severity.invalid" &&
            error.Signal == "severity");
        Assert.Contains(result.Errors, error =>
            error.Code == "case.stability.invalid" &&
            error.Signal == "stability");
    }

    [Fact]
    public void Validate_Rejects_Invalid_Case_Category()
    {
        var manifest = new PlaybackQualityReferenceManifest
        {
            SchemaVersion = 1
        };
        var referenceCase = CreateCase(
            "jellyfin/unknown-category",
            tier: 1,
            purpose: "sdr-smoke");
        referenceCase.Category = "nightly";
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.category.invalid" &&
            error.Signal == "category" &&
            error.CaseId == "jellyfin/unknown-category");
    }

    [Fact]
    public void Validate_Preserves_Optional_Hdr_Source_Strategy_Metadata()
    {
        var manifest = new PlaybackQualityReferenceManifest
        {
            SchemaVersion = 1
        };
        var referenceCase = CreateCase(
            "emby/dv-profile8-hdr10-fallback",
            tier: 2,
            purpose: "dv-fallback");
        referenceCase.Expected.HdrPlaybackStrategy = "HDR10 fallback from Dolby Vision";
        referenceCase.Expected.IsHdr = true;
        referenceCase.Expected.IsDirectPlayable = true;
        referenceCase.Expected.IsDolbyVision = true;
        referenceCase.Expected.DolbyVisionProfile = 8;
        referenceCase.Expected.DolbyVisionCompatibilityId = 1;
        referenceCase.Expected.HasHdr10BaseLayer = true;
        referenceCase.Expected.HasHlgBaseLayer = false;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Contains(result.Cases, item =>
            item.CaseId == "emby/dv-profile8-hdr10-fallback" &&
            item.Expected.HdrPlaybackStrategy == "HDR10 fallback from Dolby Vision" &&
            item.Expected.IsHdr == true &&
            item.Expected.IsDirectPlayable == true &&
            item.Expected.IsDolbyVision == true &&
            item.Expected.DolbyVisionProfile == 8 &&
            item.Expected.DolbyVisionCompatibilityId == 1 &&
            item.Expected.HasHdr10BaseLayer == true &&
            item.Expected.HasHlgBaseLayer == false);
    }

    [Fact]
    public void Validate_Rejects_Duplicate_Cases_And_Incomplete_Expected_Metadata()
    {
        var manifest = new PlaybackQualityReferenceManifest
        {
            SchemaVersion = 1
        };
        manifest.Cases.Add(CreateCase(
            "duplicate",
            tier: 5,
            purpose: ""));
        manifest.Cases[0].Uri = "";
        manifest.Cases[0].Expected.Codec = "";
        manifest.Cases[0].Expected.Width = 0;
        manifest.Cases[0].Expected.Height = 0;
        manifest.Cases[0].Expected.FrameRate = 0;
        manifest.Cases[0].Expected.HdrKind = "";
        manifest.Cases.Add(CreateCase(
            "duplicate",
            tier: 1,
            purpose: "sdr-smoke"));

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.duplicate-id" &&
            error.CaseId == "duplicate");
        Assert.Contains(result.Errors, error =>
            error.Code == "case.uri.missing" &&
            error.CaseId == "duplicate");
        Assert.Contains(result.Errors, error =>
            error.Code == "case.purpose.missing" &&
            error.CaseId == "duplicate");
        Assert.Contains(result.Errors, error =>
            error.Code == "case.tier.invalid" &&
            error.CaseId == "duplicate");
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.codec.missing" &&
            error.Signal == "expected.codec");
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.frameRate.missing" &&
            error.Signal == "expected.frameRate");
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.hdrKind.missing" &&
            error.Signal == "expected.hdrKind");
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.width.missing" &&
            error.Signal == "expected.width");
        Assert.Contains(result.Errors, error =>
            error.Code == "case.expected.height.missing" &&
            error.Signal == "expected.height");
    }

    [Fact]
    public void CreateReportRequest_Uses_CaseId_And_Expected_Metadata()
    {
        var referenceCase = CreateCase(
            "netflix/chimera-4k-2398-hdr-pq",
            tier: 2,
            purpose: "hdr-output");
        referenceCase.Expected.VideoRange = "HDR10";
        referenceCase.Expected.ColorPrimaries = "bt2020";
        referenceCase.Expected.ColorTransfer = "smpte2084";
        referenceCase.Expected.ColorSpace = "bt2020nc";
        var descriptor = CreateDescriptor(
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: HdrPlaybackKind.Hdr10);

        var request = PlaybackQualityReferenceCaseReportRequestFactory.CreateRequest(
            referenceCase,
            descriptor);
        referenceCase.Expected.Codec = "av1";
        referenceCase.Severity = "critical";

        Assert.Equal("netflix/chimera-4k-2398-hdr-pq", request.RunId);
        Assert.NotNull(request.CaseMetadata);
        Assert.Equal("netflix/chimera-4k-2398-hdr-pq", request.CaseMetadata!.CaseId);
        Assert.Equal("stable", request.CaseMetadata.Category);
        Assert.Equal("medium", request.CaseMetadata.Severity);
        Assert.Equal("stable", request.CaseMetadata.Stability);
        Assert.Same(descriptor, request.Descriptor);
        Assert.NotNull(request.Expected);
        Assert.Equal("hevc", request.Expected!.Codec);
        Assert.Equal(3840, request.Expected.Width);
        Assert.Equal(2160, request.Expected.Height);
        Assert.Equal(23.976, request.Expected.FrameRate);
        Assert.Equal("Hdr10", request.Expected.HdrKind);
        Assert.Equal("HDR10", request.Expected.VideoRange);
        Assert.Equal("bt2020", request.Expected.ColorPrimaries);
        Assert.Equal("smpte2084", request.Expected.ColorTransfer);
        Assert.Equal("bt2020nc", request.Expected.ColorSpace);
        Assert.False(request.UseDefaultExpectedWhenMissing);
    }

    [Fact]
    public void ReferenceCase_ReportRequest_Fails_As_Unsupported_Source_When_Descriptor_Does_Not_Match()
    {
        var referenceCase = CreateCase(
            "netflix/chimera-4k-2398-hdr-pq",
            tier: 2,
            purpose: "hdr-output");
        var descriptor = CreateDescriptor(
            codec: "av1",
            width: 1920,
            height: 1080,
            frameRate: 23.976,
            hdrKind: HdrPlaybackKind.Sdr);

        var request = PlaybackQualityReferenceCaseReportRequestFactory.CreateRequest(
            referenceCase,
            descriptor);
        var result = PlaybackQualityReportComposer.Compose(request);

        Assert.Equal("fail", result.Report.Result);
        Assert.Equal("unsupported-source", result.Report.Analysis.PrimaryFailureArea);
        Assert.Contains("source.codec", result.Report.Analysis.RelevantSignals);
        Assert.Contains("source.width", result.Report.Analysis.RelevantSignals);
        Assert.Contains("source.height", result.Report.Analysis.RelevantSignals);
        Assert.Contains("source.hdrKind", result.Report.Analysis.RelevantSignals);
    }

    [Fact]
    public void ReferenceCase_ReportRequest_Preserves_ForceSdrOutput_Evidence()
    {
        var referenceCase = CreateCase(
            "netflix/chimera-4k-2398-hdr-pq-force-sdr",
            tier: 2,
            purpose: "hdr-force-sdr");
        referenceCase.ForceSdrOutput = true;
        var descriptor = CreateDescriptor(
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: HdrPlaybackKind.Hdr10);

        var request = PlaybackQualityReferenceCaseReportRequestFactory.CreateRequest(
            referenceCase,
            descriptor);
        var result = PlaybackQualityReportComposer.Compose(request);

        Assert.True(result.Report.ColorPipeline.ForceSdrOutput);
        Assert.Contains(
            "colorPipeline.forceSdrOutput",
            result.ModelAnalysis.ColorPipeline.Signals);
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(
            result.Report,
            "colorPipeline.forceSdrOutput"));
    }

    [Fact]
    public void ValidateReportSet_Accepts_One_Matching_Report_Per_Reference_Case()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "netflix/chimera-4k-2398-hdr-pq",
            tier: 2,
            purpose: "hdr-output");
        referenceCase.Category = "challenge";
        referenceCase.Severity = "high";
        referenceCase.Stability = "variable";
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "netflix/chimera-4k-2398-hdr-pq",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        AddCapturedRuntimeMetrics(report);
        report.ColorPipeline.ConversionStatus = "validated";

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.Equal(PlaybackQualityRunResult.CurrentEvaluationVersion, validation.EvaluationVersion);
        Assert.True(validation.IsValid);
        Assert.Equal(1, validation.ExpectedCaseCount);
        Assert.Equal(1, validation.ReportCount);
        Assert.Equal(1, validation.MatchedCaseCount);
        Assert.Empty(validation.Errors);
        Assert.Contains(validation.Cases, item =>
            item.CaseId == "netflix/chimera-4k-2398-hdr-pq" &&
            item.Category == "challenge" &&
            item.Severity == "high" &&
            item.Stability == "variable" &&
            item.Status == "matched" &&
            item.ReportRunId == "netflix/chimera-4k-2398-hdr-pq");
    }

    [Fact]
    public void CreateReportRequest_Projects_Native_Source_Timeline()
    {
        var request = PlaybackQualityReferenceCaseReportRequestFactory.CreateRequest(
            CreateCase("timeline/native", tier: 1, purpose: "timeline"),
            CreateDescriptor("hevc", 1920, 1080, 23.976, HdrPlaybackKind.Sdr),
            metrics: new PlaybackQualityMetricsSnapshot
            {
                ContainerStartTimeTicks = 1_400_000,
                VideoStreamStartTimeTicks = 1_421_333
            });

        Assert.NotNull(request.SourceTimeline);
        Assert.Equal(1_400_000, request.SourceTimeline!.ContainerStartTimeTicks);
        Assert.Equal(1_421_333, request.SourceTimeline.VideoStreamStartTimeTicks);
    }

    [Fact]
    public void Validate_Rejects_Pause_Duration_Outside_Native_Helper_Limit()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "pause/invalid-duration",
            tier: 1,
            purpose: "pause-resume");
        referenceCase.PauseSeconds = 901;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "case.pause-seconds.invalid" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "pauseSeconds");
    }

    [Fact]
    public void ValidateReportSet_Rejects_CoreProbe_Report_For_Native_Playback_Case()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/hdr10-hevc-main10-4k60-50m",
            tier: 2,
            purpose: "hdr-output");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        AddCapturedRuntimeMetrics(report);
        report.RuntimeMetrics.ProviderStatus = "core-probe:returned-snapshot";
        report.ColorPipeline.ConversionStatus = "validated";
        report.Execution.EvidenceLevel = PlaybackQualityEvidenceLevel.Orchestration;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.evidence-level.insufficient" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "execution.evidenceLevel");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Native_Playback_Report_Without_Attempt_Identity()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/native-without-attempt",
            tier: 1,
            purpose: "sdr-smoke");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        AddCapturedRuntimeMetrics(report);
        report.ColorPipeline.ConversionStatus = "validated";
        report.Execution.AttemptId = "";
        report.Execution.EvidenceLevel = PlaybackQualityEvidenceLevel.NativePlayback;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.attempt-id.missing" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "execution.attemptId");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Pass_Without_Observed_Playback_Sample()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/native-without-playback-sample",
            tier: 1,
            purpose: "sdr-smoke");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        AddCapturedRuntimeMetrics(report);
        report.ColorPipeline.ConversionStatus = "validated";
        report.Execution.AttemptId = "attempt-without-sample";
        report.Execution.EvidenceLevel = PlaybackQualityEvidenceLevel.NativePlayback;
        report.Execution.SourceOpenAttempted = true;
        report.Execution.SourceOpened = true;
        report.Execution.NativeGraphOpened = true;
        report.Execution.DemuxStarted = true;
        report.Execution.DecoderOpened = true;
        report.Execution.PlaybackSampleObserved = false;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.playback-sample.missing" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "execution.playbackSampleObserved");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Source_Locator_Hash_From_Different_Case()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/native-wrong-source",
            tier: 1,
            purpose: "sdr-smoke");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        AddCapturedRuntimeMetrics(report);
        report.ColorPipeline.ConversionStatus = "validated";
        report.Execution.AttemptId = "attempt-wrong-source";
        report.Execution.EvidenceLevel = PlaybackQualityEvidenceLevel.NativePlayback;
        report.Execution.SourceLocatorHash = "sha256:not-the-manifest-locator";
        report.Execution.SourceOpenAttempted = true;
        report.Execution.SourceOpened = true;
        report.Execution.NativeGraphOpened = true;
        report.Execution.DemuxStarted = true;
        report.Execution.DecoderOpened = true;
        report.Execution.PlaybackSampleObserved = true;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.source-locator-hash.mismatch" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "execution.sourceLocatorHash");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Pass_Without_Opened_Decoder()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/native-without-decoder",
            tier: 1,
            purpose: "sdr-smoke");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        AddCapturedRuntimeMetrics(report);
        report.Timing.DecodedVideoFrames = 240;
        report.Timing.RenderedVideoFrames = 240;
        report.ColorPipeline.ConversionStatus = "validated";
        report.Execution = new PlaybackQualityExecutionEvidence
        {
            AttemptId = "attempt-without-decoder",
            Runner = "native-headless",
            Scenario = referenceCase.ExecutionRequirement.Scenario,
            EvidenceLevel = PlaybackQualityEvidenceLevel.NativePlayback,
            Status = "completed",
            SourceLocatorHash = PlaybackQualitySourceFingerprint.Compute(referenceCase.Uri),
            OpenedSourceHash = "sha256:opened-source",
            StartedAtUtc = "2026-07-11T00:00:00Z",
            DurationMs = 5000,
            SourceOpenAttempted = true,
            SourceOpened = true,
            NativeGraphOpened = true,
            DemuxStarted = true,
            DecoderOpened = false,
            PlaybackSampleObserved = true
        };

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.decoder-opened.missing" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "execution.decoderOpened");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Completed_Playback_Without_Requested_Sample_Duration()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/native-without-requested-sample-duration",
            tier: 1,
            purpose: "sdr-smoke");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        report.Execution.RequestedSampleDurationMs = 0;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.requested-sample-duration.invalid" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "execution.requestedSampleDurationMs");
    }

    [Fact]
    public void ValidateReportSet_Counts_Dropped_Frames_Toward_Requested_Observation_Coverage()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "local/requested-window-with-drops",
            tier: 1,
            purpose: "frame-pacing");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 1920,
            height: 1080,
            frameRate: 24,
            hdrKind: "Sdr");
        report.Timing.RenderedVideoFrames = 96;
        report.Timing.DroppedVideoFrames = 24;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.DoesNotContain(validation.Errors, error =>
            error.Code == "report.execution.sample-window.incomplete");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Incomplete_Sample_That_Is_Still_Marked_Pass()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "local/unreported-incomplete-window",
            tier: 1,
            purpose: "frame-pacing");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 1920,
            height: 1080,
            frameRate: 60,
            hdrKind: "Sdr");
        report.Timing.RenderedVideoFrames = 60;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.sample-window.incomplete");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Completed_Playback_When_Wall_Clock_Observation_Is_Short()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "local/short-wall-clock-observation",
            tier: 1,
            purpose: "frame-pacing");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 1920,
            height: 1080,
            frameRate: 60,
            hdrKind: "Sdr");
        report.Execution.ObservedSampleWallClockDurationMs = 1000;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.sample-wall-clock.incomplete" &&
            error.Signal == "execution.observedSampleWallClockDurationMs");
    }

    [Fact]
    public void ValidateReportSet_Accepts_Incomplete_Sample_When_Evaluator_Reports_The_Playback_Failure()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "local/reported-incomplete-window",
            tier: 1,
            purpose: "frame-pacing");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 1920,
            height: 1080,
            frameRate: 60,
            hdrKind: "Sdr");
        report.Expected = new PlaybackQualityExpected
        {
            RequireValidatedConversion = false
        };
        report.Timing.RenderedVideoFrames = 60;
        report.Buffers.PlaybackTransportProvider = "instrumented-ffmpeg-avio";
        report.Buffers.PlaybackTransportCallEvidenceStatus = "available";
        report.Buffers.PlaybackTransportReadWaitMs = 50;
        PlaybackQualityEvaluator.Evaluate(report);

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.DoesNotContain(validation.Errors, error =>
            error.Code == "report.execution.sample-window.incomplete");
    }

    [Fact]
    public void ValidateReportSet_Allows_Natural_End_Of_Stream_Before_Requested_Window()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "local/end-of-stream-shorter-than-window",
            tier: 1,
            purpose: "end-of-stream");
        referenceCase.ExecutionRequirement.Scenario = PlaybackQualityExecutionScenario.EndOfStream;
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 1920,
            height: 1080,
            frameRate: 24,
            hdrKind: "Sdr");
        report.Execution.Scenario = PlaybackQualityExecutionScenario.EndOfStream;
        report.Execution.RequestedSampleDurationMs = 12000;
        report.Source.DurationTicks = 50000000;
        report.Timing.RenderedVideoFrames = 120;
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "endOfStream",
            Status = "completed"
        });

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.DoesNotContain(validation.Errors, error =>
            error.Code == "report.execution.sample-window.incomplete");
    }

    [Fact]
    public void ValidateReportSet_Allows_Missing_Quarantine_Case_Without_Counting_It_As_Matched()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "quarantine/flaky-network-source",
            tier: 4,
            purpose: "buffering");
        referenceCase.Category = "quarantine";
        referenceCase.Stability = "flaky";
        manifest.Cases.Add(referenceCase);

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new PlaybackQualityReport[0]);

        Assert.True(validation.IsValid);
        Assert.Equal(0, validation.MatchedCaseCount);
        Assert.Contains(validation.Cases, item =>
            item.CaseId == referenceCase.CaseId &&
            item.Status == "quarantine-missing");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Incomplete_Native_Execution_Metadata()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/incomplete-execution-metadata",
            tier: 1,
            purpose: "sdr-smoke");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        AddCapturedRuntimeMetrics(report);
        report.ColorPipeline.ConversionStatus = "validated";
        report.Execution.Runner = "";
        report.Execution.Status = "mystery";
        report.Execution.OpenedSourceHash = "";
        report.Execution.OpenedSourceHashKind = "legacy-locator-hash";
        report.Execution.StartedAtUtc = "not-a-time";
        report.Execution.DurationMs = -1;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.False(validation.ExecutionValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.runner.missing");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.status.invalid");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.opened-source-hash.missing");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.opened-source-hash-kind.invalid");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.started-at.invalid");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.duration.invalid");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Opened_Source_Hash_That_Aliases_Locator()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("local/opened-source-alias", tier: 1, purpose: "sdr-smoke");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(referenceCase.CaseId, "h264", 320, 180, 30, "Sdr");
        AddCapturedRuntimeMetrics(report);
        report.ColorPipeline.ConversionStatus = "validated";
        report.Execution.OpenedSourceHash = report.Execution.SourceLocatorHash;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(manifest, new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.opened-source-hash.aliases-locator");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Playback_Sample_Claim_Without_Runtime_And_Frame_Evidence()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/unconfirmed-playback-sample",
            tier: 1,
            purpose: "sdr-smoke");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            referenceCase.CaseId,
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        report.ColorPipeline.ConversionStatus = "validated";
        report.RuntimeMetrics = PlaybackQualityRuntimeMetricsFactory.Unavailable(
            "native-headless:no-snapshot");
        report.Timing.DecodedVideoFrames = 0;
        report.Timing.RenderedVideoFrames = 0;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.False(validation.ExecutionValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.runtime-playback-sample.missing");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.decoded-frame.missing");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.rendered-frame.missing");
    }

    [Fact]
    public void ValidateReportSet_Reports_Execution_Coverage_Without_Counting_Quarantine_Missing()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var completedCase = CreateCase("coverage/completed", 1, "sdr-smoke");
        var failedCase = CreateCase("coverage/failed", 1, "error-handling");
        var missingCase = CreateCase("coverage/missing", 1, "tracks");
        var quarantineCase = CreateCase("coverage/quarantine", 4, "buffering");
        quarantineCase.Category = "quarantine";
        quarantineCase.Stability = "flaky";
        manifest.Cases.Add(completedCase);
        manifest.Cases.Add(failedCase);
        manifest.Cases.Add(missingCase);
        manifest.Cases.Add(quarantineCase);

        var completedReport = CreateReport(
            completedCase.CaseId,
            "hevc",
            3840,
            2160,
            23.976,
            "Hdr10");
        AddCapturedRuntimeMetrics(completedReport);
        completedReport.ColorPipeline.ConversionStatus = "validated";
        var failedReport = new PlaybackQualityReport
        {
            RunId = failedCase.CaseId,
            Result = PlaybackQualityReportResult.Error,
            Error = new PlaybackQualityError
            {
                Code = "source.open.failed",
                Message = "The media source could not be opened.",
                FailureClass = "environment issue",
                FailureArea = "error-handling"
            }
        };
        AddTestEnvironment(failedReport);
        AddNativeExecutionEvidence(
            failedReport,
            failedCase,
            "failed",
            sourceOpened: false,
            decoderOpened: false,
            playbackSampleObserved: false);

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { completedReport, failedReport });

        Assert.False(validation.IsValid);
        Assert.Equal(4, validation.ExecutionCoverage.DeclaredCaseCount);
        Assert.Equal(2, validation.ExecutionCoverage.AttemptedCaseCount);
        Assert.Equal(1, validation.ExecutionCoverage.OpenedCaseCount);
        Assert.Equal(1, validation.ExecutionCoverage.DecodedCaseCount);
        Assert.Equal(1, validation.ExecutionCoverage.RenderedCaseCount);
        Assert.Equal(1, validation.ExecutionCoverage.CompletedCaseCount);
        Assert.Equal(1, validation.ExecutionCoverage.FailedCaseCount);
        Assert.Equal(0, validation.ExecutionCoverage.UnsupportedCaseCount);
        Assert.Equal(0, validation.ExecutionCoverage.SkippedCaseCount);
        Assert.Equal(1, validation.ExecutionCoverage.MissingCaseCount);
        Assert.Equal(1, validation.ExecutionCoverage.QuarantineMissingCaseCount);
    }

    [Fact]
    public void ValidateReportSet_Accepts_Structured_Timeline_Error_Without_Success_Only_Seek_Evidence()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("timeline/network-error", 1, "timeline");
        referenceCase.ExecutionRequirement.Scenario = PlaybackQualityExecutionScenario.Timeline;
        referenceCase.Expected.MaxSeekPositionErrorMs = 500;
        referenceCase.Expected.MaxSeekRecoveryDurationMs = 2000;
        manifest.Cases.Add(referenceCase);

        var report = CreateReport(referenceCase.CaseId, "hevc", 3840, 2160, 23.976, "Hdr10");
        report.Result = PlaybackQualityReportResult.Error;
        report.Error = new PlaybackQualityError
        {
            Code = "native-headless.network-io-failed",
            Message = "HTTP response ended while seeking.",
            Operation = "seek",
            ExceptionType = "native-helper-exit",
            FailureClass = PlaybackQualityFailureClassification.ExternalServiceOrProtocolIssue,
            FailureArea = "error-handling"
        };
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "error",
            Status = "error",
            Message = report.Error.Message
        });
        AddCapturedRuntimeMetrics(report);
        AddNativeExecutionEvidence(
            report,
            referenceCase,
            PlaybackQualityExecutionStatus.Failed,
            sourceOpened: true,
            decoderOpened: true,
            playbackSampleObserved: true);

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(manifest, new[] { report });

        Assert.True(validation.StructureValid);
        Assert.True(validation.ExecutionValid);
        Assert.True(validation.IsValid);
        Assert.DoesNotContain(validation.Errors, error => error.Code == "report.requiredSignal.missing");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Stable_Skip_Even_When_A_Runner_Claims_Native_Evidence()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("native-harness/claimed-native-skip", 1, "frame-pacing");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(referenceCase.CaseId, "hevc", 3840, 2160, 23.976, "Hdr10");
        report.Result = PlaybackQualityReportResult.Skip;
        report.Execution.Status = PlaybackQualityExecutionStatus.Skipped;
        report.Execution.SourceOpened = false;
        report.Execution.NativeGraphOpened = false;
        report.Execution.DemuxStarted = false;
        report.Execution.DecoderOpened = false;
        report.Execution.PlaybackSampleObserved = false;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.stable-skip.not-allowed");
    }

    [Theory]
    [InlineData(PlaybackQualityReportResult.Pass, PlaybackQualityExecutionStatus.Failed, PlaybackQualityExecutionStatus.Completed)]
    [InlineData(PlaybackQualityReportResult.Error, PlaybackQualityExecutionStatus.Completed, "failed, timed-out, cancelled")]
    [InlineData(PlaybackQualityReportResult.Unsupported, PlaybackQualityExecutionStatus.Completed, PlaybackQualityExecutionStatus.Unsupported)]
    public void ValidateReportSet_Rejects_Result_And_Execution_Status_Mismatch(
        string result,
        string executionStatus,
        string expectedStatus)
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("execution/status-mismatch-" + result, 1, "error-handling");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(referenceCase.CaseId, "hevc", 3840, 2160, 23.976, "Hdr10");
        AddCapturedRuntimeMetrics(report);
        report.ColorPipeline.ConversionStatus = "validated";
        report.Result = result;
        report.Execution.Status = executionStatus;
        if (result != PlaybackQualityReportResult.Pass)
        {
            report.Error = new PlaybackQualityError
            {
                Code = "execution.status-mismatch",
                Message = "Execution status does not match result.",
                FailureClass = "evaluation harness bug",
                FailureArea = "error-handling"
            };
        }

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.status-result.mismatch" &&
            error.Expected == expectedStatus &&
            error.Actual == executionStatus);
    }

    [Fact]
    public void ValidateManifest_Rejects_Unknown_Minimum_Execution_Evidence_Level()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("manifest/unknown-evidence-level", 1, "sdr-smoke");
        referenceCase.ExecutionRequirement.MinimumEvidenceLevel = "native-ish";
        manifest.Cases.Add(referenceCase);

        var validation = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "case.execution.minimum-evidence-level.invalid" &&
            error.CaseId == referenceCase.CaseId);
    }

    [Fact]
    public void ValidateManifest_Rejects_Unknown_Execution_Scenario()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("manifest/unknown-execution-scenario", 1, "sdr-smoke");
        referenceCase.ExecutionRequirement.Scenario = "do-everything";
        manifest.Cases.Add(referenceCase);

        var validation = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "case.execution.scenario.invalid" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "executionRequirement.scenario");
    }

    [Fact]
    public void ValidateManifest_Rejects_Missing_Execution_Scenario()
    {
        Assert.Equal("", new PlaybackQualityExecutionRequirement().Scenario);

        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("manifest/missing-execution-scenario", 1, "sdr-smoke");
        referenceCase.ExecutionRequirement.Scenario = "";
        manifest.Cases.Add(referenceCase);

        var validation = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "case.execution.scenario.invalid" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "executionRequirement.scenario");
    }

    [Fact]
    public void ValidateManifest_Preserves_Explicit_Execution_Scenario()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("manifest/subtitle-switch", 1, "subtitle-switch");
        referenceCase.ExecutionRequirement.Scenario = "subtitle-switch";
        manifest.Cases.Add(referenceCase);

        var validation = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(validation.IsValid);
        Assert.Equal("subtitle-switch", Assert.Single(validation.Cases).ExecutionRequirement.Scenario);
    }

    [Fact]
    public void ValidateManifest_Accepts_End_Of_Stream_Execution_Scenario()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("manifest/end-of-stream", 1, "end-of-stream");
        referenceCase.ExecutionRequirement.Scenario = PlaybackQualityExecutionScenario.EndOfStream;
        manifest.Cases.Add(referenceCase);

        var validation = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(validation.IsValid);
        Assert.Equal(
            PlaybackQualityExecutionScenario.EndOfStream,
            Assert.Single(validation.Cases).ExecutionRequirement.Scenario);
    }

    [Theory]
    [InlineData("timeline", "playback", 0)]
    [InlineData("audio-switch", "playback", 0)]
    [InlineData("subtitle-switch", "playback", 0)]
    [InlineData("pause-resume", "playback", 30)]
    [InlineData("end-of-stream", "playback", 0)]
    [InlineData("sdr-smoke", "pause-resume", 0)]
    public void ValidateManifest_Rejects_Execution_Scenario_That_Does_Not_Match_Case_Intent(
        string purpose,
        string scenario,
        int pauseSeconds)
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("manifest/scenario-intent-mismatch", 1, purpose);
        referenceCase.ExecutionRequirement.Scenario = scenario;
        referenceCase.PauseSeconds = pauseSeconds;
        manifest.Cases.Add(referenceCase);

        var validation = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "case.execution.scenario-intent.mismatch" &&
            error.CaseId == referenceCase.CaseId);
    }

    [Fact]
    public void ValidateManifest_Rejects_Multiple_Active_Execution_Intents()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("manifest/multiple-active-execution-intents", 1, "timeline");
        referenceCase.Purpose.Add("subtitle-switch");
        referenceCase.ExecutionRequirement.Scenario = PlaybackQualityExecutionScenario.Timeline;
        manifest.Cases.Add(referenceCase);

        var validation = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "case.execution.scenario-intent.multiple" &&
            error.CaseId == referenceCase.CaseId &&
            error.Signal == "purpose");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Execution_Scenario_Mismatch()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase("execution/subtitle-switch", 1, "subtitle-switch");
        referenceCase.ExecutionRequirement.Scenario = PlaybackQualityExecutionScenario.SubtitleSwitch;
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(referenceCase.CaseId, "hevc", 3840, 2160, 23.976, "Hdr10");
        AddCapturedRuntimeMetrics(report);
        report.ColorPipeline.ConversionStatus = "validated";
        report.Execution.Scenario = PlaybackQualityExecutionScenario.Playback;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(manifest, new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.scenario.mismatch" &&
            error.CaseId == referenceCase.CaseId &&
            error.Expected == PlaybackQualityExecutionScenario.SubtitleSwitch &&
            error.Actual == PlaybackQualityExecutionScenario.Playback);
    }

    [Fact]
    public void ValidateReportSet_Rejects_Unknown_Report_Result()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "netflix/chimera-4k-2398-hdr-pq",
            tier: 2,
            purpose: "hdr-output");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "netflix/chimera-4k-2398-hdr-pq",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        report.ColorPipeline.ConversionStatus = "validated";
        report.Result = "observed";

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.result.invalid" &&
            error.Signal == "result" &&
            error.Actual == "observed" &&
            error.FailureClass == "evaluation harness bug");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Missing_Player_Identity()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "netflix/chimera-4k-2398-hdr-pq",
            tier: 2,
            purpose: "hdr-output");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "netflix/chimera-4k-2398-hdr-pq",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        report.Environment.PlayerCoreVersion = "";
        report.Environment.SourceRevision = "";
        report.ColorPipeline.ConversionStatus = "validated";

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.environment.missing" &&
            error.Signal == "environment.playerCoreVersion" &&
            error.FailureClass == "insufficient instrumentation");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.environment.missing" &&
            error.Signal == "environment.sourceRevision" &&
            error.FailureClass == "insufficient instrumentation");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Null_Player_Identity()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "netflix/chimera-4k-2398-hdr-pq",
            tier: 2,
            purpose: "hdr-output");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "netflix/chimera-4k-2398-hdr-pq",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        report.Environment = null!;
        report.ColorPipeline.ConversionStatus = "validated";

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.environment.missing" &&
            error.Signal == "environment.playerCoreVersion" &&
            error.FailureClass == "insufficient instrumentation");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.environment.missing" &&
            error.Signal == "environment.sourceRevision" &&
            error.FailureClass == "insufficient instrumentation");
    }

    [Fact]
    public void ValidateReportSet_Accepts_Error_Handling_Report_Without_Source_Metadata()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "errors/missing-file",
            tier: 1,
            purpose: "error-handling");
        manifest.Cases.Add(referenceCase);
        var report = new PlaybackQualityReport
        {
            RunId = "errors/missing-file",
            Result = "error",
            Error = new PlaybackQualityError
            {
                Code = "source.open.missing-file",
                Message = "The media file was not found.",
                FailureClass = "sample issue",
                FailureArea = "error-handling"
            }
        };
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "open",
            Status = "error",
            Message = "The media file was not found."
        });
        AddTestEnvironment(report);
        AddNativeExecutionEvidence(
            report,
            referenceCase,
            status: "failed",
            sourceOpened: false,
            decoderOpened: false,
            playbackSampleObserved: false);

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.True(validation.IsValid);
        Assert.Equal(1, validation.MatchedCaseCount);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public void ValidateReportSet_Rejects_Unknown_Error_Failure_Class()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "errors/unknown-class",
            tier: 1,
            purpose: "error-handling");
        manifest.Cases.Add(referenceCase);
        var report = new PlaybackQualityReport
        {
            RunId = "errors/unknown-class",
            Result = "error",
            Error = new PlaybackQualityError
            {
                Code = "source.open.failed",
                Message = "The media file could not be opened.",
                FailureClass = "mystery class",
                FailureArea = "error-handling"
            }
        };
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "open",
            Status = "error"
        });

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.failureClass.invalid" &&
            error.Signal == "error.failureClass" &&
            error.Actual == "mystery class" &&
            error.FailureClass == "evaluation harness bug");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Unknown_Error_Failure_Area()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "errors/unknown-area",
            tier: 1,
            purpose: "error-handling");
        manifest.Cases.Add(referenceCase);
        var report = new PlaybackQualityReport
        {
            RunId = "errors/unknown-area",
            Result = "error",
            Error = new PlaybackQualityError
            {
                Code = "source.open.failed",
                Message = "The media file could not be opened.",
                FailureClass = "sample issue",
                FailureArea = "mystery-area"
            }
        };
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = "open",
            Status = "error"
        });

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.failureArea.invalid" &&
            error.Signal == "error.failureArea" &&
            error.Actual == "mystery-area" &&
            error.FailureClass == "evaluation harness bug");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Stable_Skip_Report_Without_Native_Attempt()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "native-harness/not-implemented",
            tier: 1,
            purpose: "frame-pacing");
        manifest.Cases.Add(referenceCase);
        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeSkipRunResult(
            referenceCase,
            new PlaybackQualitySkip
            {
                Code = "native-harness.not-implemented",
                Reason = "Native playback harness is not implemented.",
                Operation = "materialize-native-harness",
                FailureClass = "insufficient instrumentation",
                FailureArea = "evidence-collection",
                IsExpected = true,
                IsRetriable = true
            });
        AddTestEnvironment(result.Report);

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { result.Report });

        Assert.False(validation.IsValid);
        Assert.Equal(0, validation.MatchedCaseCount);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.execution.evidence-level.insufficient" &&
            error.CaseId == referenceCase.CaseId);
    }

    [Fact]
    public void ValidateReportSet_Rejects_Unknown_Skip_Failure_Class()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "native-harness/unknown-class",
            tier: 1,
            purpose: "frame-pacing");
        manifest.Cases.Add(referenceCase);
        var result = PlaybackQualityRuntimeEvidenceCollector.ComposeSkipRunResult(
            referenceCase,
            new PlaybackQualitySkip
            {
                Code = "native-harness.not-implemented",
                Reason = "Native playback harness is not implemented.",
                Operation = "materialize-native-harness",
                FailureClass = "mystery class",
                FailureArea = "evidence-collection",
                IsExpected = true,
                IsRetriable = true
            });

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { result.Report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.failureClass.invalid" &&
            error.Signal == "skip.failureClass" &&
            error.Actual == "mystery class" &&
            error.FailureClass == "evaluation harness bug");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Unknown_Check_Failure_Area()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "netflix/chimera-4k-2398-hdr-pq",
            tier: 2,
            purpose: "hdr-output");
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "netflix/chimera-4k-2398-hdr-pq",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        report.Result = "fail";
        report.ColorPipeline.ConversionStatus = "validated";
        report.Checks.Add(new PlaybackQualityCheck
        {
            Name = "MysteryCheck",
            Signal = "timing.maxFrameGapMs",
            Status = "fail",
            FailureArea = "mystery-area",
            Expected = "within threshold",
            Actual = "above threshold"
        });

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.failureArea.invalid" &&
            error.Signal == "checks.failureArea" &&
            error.Actual == "mystery-area" &&
            error.FailureClass == "evaluation harness bug");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Missing_Extra_Duplicate_And_Mismatched_Reports()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        manifest.Cases.Add(CreateCase(
            "netflix/chimera-4k-2398-hdr-pq",
            tier: 2,
            purpose: "hdr-output"));
        manifest.Cases.Add(CreateCase(
            "jellyfin/dv-profile5-hevc-4k",
            tier: 3,
            purpose: "dv-reject"));
        var reports = new[]
        {
            CreateReport(
                "netflix/chimera-4k-2398-hdr-pq",
                codec: "av1",
                width: 1920,
                height: 1080,
                frameRate: 24.0,
                hdrKind: "Sdr"),
            CreateReport(
                "netflix/chimera-4k-2398-hdr-pq",
                codec: "hevc",
                width: 3840,
                height: 2160,
                frameRate: 23.976,
                hdrKind: "Hdr10"),
            CreateReport(
                "unexpected/case",
                codec: "hevc",
                width: 3840,
                height: 2160,
                frameRate: 23.976,
                hdrKind: "Hdr10")
        };

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            reports);

        Assert.False(validation.IsValid);
        Assert.Equal(2, validation.ExpectedCaseCount);
        Assert.Equal(3, validation.ReportCount);
        Assert.Equal(0, validation.MatchedCaseCount);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.duplicate-run-id" &&
            error.CaseId == "netflix/chimera-4k-2398-hdr-pq" &&
            error.FailureClass == "evaluation harness bug");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.missing" &&
            error.CaseId == "jellyfin/dv-profile5-hevc-4k" &&
            error.FailureClass == "environment issue");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.extra" &&
            error.ReportRunId == "unexpected/case" &&
            error.FailureClass == "evaluation harness bug");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.source.codec.mismatch" &&
            error.Signal == "source.codec" &&
            error.FailureClass == "external service/protocol issue");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.source.width.mismatch" &&
            error.Signal == "source.width");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.source.height.mismatch" &&
            error.Signal == "source.height");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.source.frameRate.mismatch" &&
            error.Signal == "source.frameRate");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.source.hdrKind.mismatch" &&
            error.Signal == "source.hdrKind");
        Assert.Contains(validation.Cases, item =>
            item.CaseId == "jellyfin/dv-profile5-hevc-4k" &&
            item.Status == "missing");
        Assert.Contains(validation.Cases, item =>
            item.ReportRunId == "unexpected/case" &&
            item.Status == "extra");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Hdr_Source_Strategy_Mismatches()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "emby/dv-profile8-hdr10-fallback",
            tier: 2,
            purpose: "dv-fallback");
        referenceCase.Expected.HdrKind = "DolbyVisionWithHdr10Fallback";
        referenceCase.Expected.HdrPlaybackStrategy = "HDR10 fallback from Dolby Vision";
        referenceCase.Expected.IsDirectPlayable = true;
        referenceCase.Expected.IsDolbyVision = true;
        referenceCase.Expected.DolbyVisionProfile = 8;
        referenceCase.Expected.DolbyVisionCompatibilityId = 1;
        referenceCase.Expected.HasHdr10BaseLayer = true;
        referenceCase.Expected.HasHlgBaseLayer = false;
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "emby/dv-profile8-hdr10-fallback",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "DolbyVisionWithHdr10Fallback");
        report.Source.HdrPlaybackStrategy = "Dolby Vision unsupported";
        report.Source.IsDirectPlayable = false;
        report.Source.IsDolbyVision = true;
        report.Source.DolbyVisionProfile = 5;
        report.Source.DolbyVisionCompatibilityId = 0;
        report.Source.HasHdr10BaseLayer = false;
        report.Source.HasHlgBaseLayer = false;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Equal(0, validation.MatchedCaseCount);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.source.hdrPlaybackStrategy.mismatch" &&
            error.Signal == "source.hdrPlaybackStrategy");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.source.isDirectPlayable.mismatch" &&
            error.Signal == "source.isDirectPlayable");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.source.dolbyVisionProfile.mismatch" &&
            error.Signal == "source.dolbyVisionProfile");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.source.dolbyVisionCompatibilityId.mismatch" &&
            error.Signal == "source.dolbyVisionCompatibilityId");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.source.hasHdr10BaseLayer.mismatch" &&
            error.Signal == "source.hasHdr10BaseLayer");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Missing_Required_Telemetry_Signals()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/hdr10-4k-23976",
            tier: 1,
            purpose: "hdr-output");
        referenceCase.Expected.HdrOutput = "Hdr10";
        referenceCase.Expected.DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020";
        referenceCase.Expected.DxgiOutput = "RGB_FULL_G2084_NONE_P2020";
        referenceCase.Expected.MaxAudioVideoDriftMsP95 = 40;
        referenceCase.Expected.RequireMatchedDisplayRefreshRate = true;
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "jellyfin/hdr10-4k-23976",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Equal(0, validation.MatchedCaseCount);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "colorPipeline.actualHdrOutput" &&
            error.FailureClass == "insufficient instrumentation");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "colorPipeline.dxgiInput");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "colorPipeline.dxgiOutput");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "colorPipeline.conversionStatus");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "sync.audioVideoDriftMsP95");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "display.refreshRateHz");
        Assert.Contains(validation.Cases, item =>
            item.CaseId == "jellyfin/hdr10-4k-23976" &&
            item.Status == "mismatch" &&
            item.Signals.Contains("colorPipeline.actualHdrOutput") &&
            item.Signals.Contains("display.refreshRateHz"));
    }

    [Fact]
    public void ValidateReportSet_Rejects_Hdr_Output_Without_Display_And_SwapChain_Evidence()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/hdr10-4k-color-pipeline",
            tier: 1,
            purpose: "hdr-output");
        referenceCase.Expected.HdrOutput = "Hdr10";
        referenceCase.Expected.DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020";
        referenceCase.Expected.DxgiOutput = "RGB_FULL_G2084_NONE_P2020";
        referenceCase.Expected.RequireValidatedConversion = true;
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "jellyfin/hdr10-4k-color-pipeline",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        report.ColorPipeline.ActualHdrOutput = "Hdr10";
        report.ColorPipeline.DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020";
        report.ColorPipeline.DxgiOutput = "RGB_FULL_G2084_NONE_P2020";
        report.ColorPipeline.ConversionStatus = "validated";

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "display.hdrStatus");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "colorPipeline.swapChainFormat");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "colorPipeline.swapChainColorSpace");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "colorPipeline.isTenBitSwapChain");
    }

    [Fact]
    public void ValidateReportSet_Rejects_Missing_Source_Color_Metadata_When_Expected()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/hdr10-source-color-metadata",
            tier: 1,
            purpose: "hdr-output");
        referenceCase.Expected.VideoRange = "HDR10";
        referenceCase.Expected.ColorPrimaries = "bt2020";
        referenceCase.Expected.ColorTransfer = "smpte2084";
        referenceCase.Expected.ColorSpace = "bt2020nc";
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "jellyfin/hdr10-source-color-metadata",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "source.videoRange" &&
            error.FailureClass == "insufficient instrumentation");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "source.colorPrimaries" &&
            error.FailureClass == "insufficient instrumentation");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "source.colorTransfer" &&
            error.FailureClass == "insufficient instrumentation");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "source.colorSpace" &&
            error.FailureClass == "insufficient instrumentation");
    }

    [Fact]
    public void ValidateReportSet_Annotates_Missing_Signals_With_Model_Triage_Targets()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/hdr10-4k-missing-display-state",
            tier: 1,
            purpose: "hdr-output");
        referenceCase.Expected.HdrOutput = "Hdr10";
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "jellyfin/hdr10-4k-missing-display-state",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });
        var error = validation.Errors.Single(item =>
            item.Code == "report.requiredSignal.missing" &&
            item.Signal == "display.hdrStatus");

        Assert.Equal("color-pipeline", error.FailureArea);
        Assert.Equal("insufficient instrumentation", error.FailureClass);
        Assert.Contains(
            "src/NoiraPlayer.Native/DxDeviceResources.cpp",
            error.CodeTargets);
        Assert.Contains(
            "display HDR state",
            error.SuggestedNextAction);
    }

    [Fact]
    public void ValidateReportSet_Accepts_Explicit_Zero_Required_Counters_When_Signal_Presence_Is_Captured()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/4k-buffering-zero-starvation",
            tier: 1,
            purpose: "buffering");
        referenceCase.Expected.MaxVideoStarvedPasses = 0;
        referenceCase.Expected.MaxAudioStarvedPasses = 0;
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "jellyfin/4k-buffering-zero-starvation",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        report.ColorPipeline.ConversionStatus = "validated";
        var entry = new PlaybackQualityReferenceReportSetEntry(report)
        {
            HasSignalPresenceEvidence = true
        };
        entry.PresentSignals.Add("source.videoMetadataProvider");
        entry.PresentSignals.Add("source.videoMetadataStatus");
        entry.PresentSignals.Add("source.codec");
        entry.PresentSignals.Add("source.hasDirectStreamUrl");
        entry.PresentSignals.Add("source.directStreamProtocol");
        entry.PresentSignals.Add("source.width");
        entry.PresentSignals.Add("source.height");
        entry.PresentSignals.Add("source.frameRate");
        entry.PresentSignals.Add("source.hdrKind");
        entry.PresentSignals.Add("lifecycle.load");
        entry.PresentSignals.Add("lifecycle.play");
        entry.PresentSignals.Add("lifecycle.pause");
        entry.PresentSignals.Add("lifecycle.resume");
        entry.PresentSignals.Add("lifecycle.stop");
        AddCapturedRuntimeMetrics(report);
        entry.PresentSignals.Add("runtimeMetrics.status");
        entry.PresentSignals.Add("runtimeMetrics.providerStatus");
        entry.PresentSignals.Add("runtimeMetrics.hasSnapshot");
        entry.PresentSignals.Add("runtimeMetrics.hasPlaybackSample");
        entry.PresentSignals.Add("timing.videoDecoderSendPacketEagainCount");
        entry.PresentSignals.Add("timing.videoDecoderDoubleEagainRetryCount");
        entry.PresentSignals.Add("timing.videoDecoderDoubleEagainRecoveryCount");
        entry.PresentSignals.Add("timing.videoDecoderDoubleEagainExhaustedCount");
        entry.PresentSignals.Add("colorPipeline.conversionStatus");
        entry.PresentSignals.Add("buffers.videoStarvedPasses");
        entry.PresentSignals.Add("buffers.audioStarvedPasses");
        foreach (var signal in PlaybackQualityVideoRenderPhaseEvidence.Signals)
        {
            entry.PresentSignals.Add(signal);
        }
        foreach (var componentName in PlaybackQualityStartupTransportCallEvidence.ComponentNames)
        {
            foreach (var fieldName in PlaybackQualityStartupTransportCallEvidence.FieldNames)
            {
                entry.PresentSignals.Add(
                    PlaybackQualityStartupTransportCallEvidence.CreateSignal(
                        componentName,
                        fieldName));
            }
        }

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { entry });

        Assert.True(validation.IsValid);
        Assert.Equal(1, validation.MatchedCaseCount);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public void ValidateReportSet_Rejects_Missing_Required_Counter_Telemetry_When_Signal_Presence_Is_Captured()
    {
        var manifest = new PlaybackQualityReferenceManifest();
        var referenceCase = CreateCase(
            "jellyfin/4k-buffering-missing-starvation",
            tier: 1,
            purpose: "buffering");
        referenceCase.Expected.MaxVideoStarvedPasses = 0;
        referenceCase.Expected.MaxAudioStarvedPasses = 0;
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "jellyfin/4k-buffering-missing-starvation",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");
        report.ColorPipeline.ConversionStatus = "validated";
        var entry = new PlaybackQualityReferenceReportSetEntry(report)
        {
            HasSignalPresenceEvidence = true
        };
        entry.PresentSignals.Add("source.codec");
        entry.PresentSignals.Add("source.width");
        entry.PresentSignals.Add("source.height");
        entry.PresentSignals.Add("source.frameRate");
        entry.PresentSignals.Add("source.hdrKind");
        entry.PresentSignals.Add("colorPipeline.conversionStatus");

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { entry });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "buffers.videoStarvedPasses");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "buffers.audioStarvedPasses");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "timing.videoDecoderDoubleEagainExhaustedCount" &&
            error.FailureArea == "decoder-recovery" &&
            error.CodeTargets.Contains("src/NoiraPlayer.Native/Media/VideoDecoder.cpp"));
    }

    [Fact]
    public void RequiredSignalPolicy_Only_Emits_Known_Report_Signals()
    {
        var referenceCase = CreateCase(
            "jellyfin/rich-required-signal-case",
            tier: 1,
            purpose: "cadence-23.976");
        referenceCase.Purpose.Add("frame-pacing");
        referenceCase.Purpose.Add("av-sync");
        referenceCase.Purpose.Add("buffering");
        referenceCase.Purpose.Add("tracks");
        referenceCase.Purpose.Add("subtitles");
        referenceCase.Purpose.Add("hdr-force-sdr");
        referenceCase.Expected.HdrPlaybackStrategy = "HDR10 direct";
        referenceCase.Expected.IsHdr = true;
        referenceCase.Expected.IsDirectPlayable = true;
        referenceCase.Expected.IsDolbyVision = false;
        referenceCase.Expected.DolbyVisionProfile = 8;
        referenceCase.Expected.DolbyVisionCompatibilityId = 1;
        referenceCase.Expected.HasHdr10BaseLayer = true;
        referenceCase.Expected.HasHlgBaseLayer = false;
        referenceCase.Expected.MaxStartupDurationMs = 5000;
        referenceCase.Expected.MinRenderedVideoFrames = 120;
        referenceCase.Expected.MaxDroppedFrames = 0;
        referenceCase.Expected.MaxFrameGapMs = 105;
        referenceCase.Expected.MaxRenderIntervalMsP95 = 55;
        referenceCase.Expected.MaxRenderIntervalMsP99 = 80;
        referenceCase.Expected.MaxAudioVideoDriftMsP95 = 40;
        referenceCase.Expected.MaxSeekPositionErrorMs = 250;
        referenceCase.Expected.MaxVideoStarvedPasses = 0;
        referenceCase.Expected.MaxAudioStarvedPasses = 0;
        referenceCase.Expected.HdrOutput = "Hdr10";
        referenceCase.Expected.DxgiInput = "YCBCR_STUDIO_G2084_TOPLEFT_P2020";
        referenceCase.Expected.DxgiOutput = "RGB_FULL_G2084_NONE_P2020";
        referenceCase.Expected.RequireValidatedConversion = true;
        referenceCase.Expected.RequireMatchedDisplayRefreshRate = true;
        referenceCase.ForceSdrOutput = true;

        var knownSignals = new HashSet<string>(
            PlaybackQualitySignalCatalog.ReportSignals.Select(signal => signal.Signal));
        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.All(requiredSignals, signal => Assert.Contains(signal, knownSignals));
    }

    [Fact]
    public void RequiredSignalPolicy_Requires_Position_Signals_For_Seek_Threshold()
    {
        var referenceCase = CreateCase(
            "timeline/seek-position",
            tier: 1,
            purpose: "timeline");
        referenceCase.Expected.MaxSeekPositionErrorMs = 250;

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("position.seekTargetPositionTicks", requiredSignals);
        Assert.Contains("position.actualPositionTicks", requiredSignals);
        Assert.Contains("position.seekPositionErrorMs", requiredSignals);
        Assert.Contains("position.seekOperationDurationMs", requiredSignals);
        Assert.Contains("position.seekRecoveryDurationMs", requiredSignals);
        Assert.Contains("position.seekPacketCacheEnabled", requiredSignals);
        Assert.Contains("position.seekPacketCacheHit", requiredSignals);
        Assert.Contains("position.seekPacketCachePacketCount", requiredSignals);
        Assert.Contains("position.seekPacketCacheBytes", requiredSignals);
        Assert.Contains("position.seekPacketCacheWindowDurationTicks", requiredSignals);
        Assert.Contains("position.seekFallbackReason", requiredSignals);
    }

    [Fact]
    public void RequiredSignalPolicy_Requires_EndOfStream_Lifecycle_Signal_For_EndOfStream_Purpose()
    {
        var referenceCase = CreateCase(
            "lifecycle/end-of-stream",
            tier: 1,
            purpose: "end-of-stream");

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("lifecycle.endOfStream", requiredSignals);
    }

    [Fact]
    public void RequiredSignalPolicy_Requires_Runtime_Metrics_State_For_Playable_Cases()
    {
        var referenceCase = CreateCase(
            "runtime-metrics/playable-case",
            tier: 1,
            purpose: "sdr-smoke");

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("runtimeMetrics.status", requiredSignals);
        Assert.Contains("runtimeMetrics.providerStatus", requiredSignals);
        Assert.Contains("runtimeMetrics.hasSnapshot", requiredSignals);
        Assert.Contains("runtimeMetrics.hasPlaybackSample", requiredSignals);
    }

    [Fact]
    public void RequiredSignalPolicy_Requires_Native_Startup_Transport_Call_Evidence()
    {
        var referenceCase = CreateCase(
            "startup/native-transport-calls",
            tier: 1,
            purpose: "sdr-smoke");

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains(
            "startup.stage.native.open.component.ffmpeg.open-input.transportProvider",
            requiredSignals);
        Assert.Contains(
            "startup.stage.native.open.component.ffmpeg.find-stream-info.transportReadCalls",
            requiredSignals);
        Assert.Contains(
            "startup.stage.native.open.component.native.startup-seek.transportSeekWaitMs",
            requiredSignals);
        Assert.Contains(
            "startup.stage.native.open.component.native.first-frame.demux-read.transportSeekDistanceBytes",
            requiredSignals);
    }

    [Fact]
    public void ValidateReportSet_Rejects_Missing_Native_Startup_Transport_Call_Json_Field()
    {
        var referenceCase = CreateCase(
            "startup/missing-native-transport-field",
            tier: 1,
            purpose: "sdr-smoke");
        var manifest = new PlaybackQualityReferenceManifest();
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(referenceCase.CaseId, "hevc", 3840, 2160, 23.976, "Hdr10");
        AddCapturedRuntimeMetrics(report);
        AddStartupTransportCallEvidence(report, "instrumented-ffmpeg-avio", "measured");
        var entry = new PlaybackQualityReferenceReportSetEntry(report)
        {
            HasSignalPresenceEvidence = true
        };
        foreach (var signal in PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase))
        {
            entry.PresentSignals.Add(signal);
        }

        const string missingSignal =
            "startup.stage.native.open.component.native.startup-seek.transportSeekWaitMs";
        entry.PresentSignals.Remove(missingSignal);

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(manifest, new[] { entry });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == missingSignal);
    }

    [Fact]
    public void ValidateReportSet_Rejects_Missing_Video_Render_Phase_Json_Field()
    {
        var referenceCase = CreateCase(
            "render/missing-phase-field",
            tier: 1,
            purpose: "frame-pacing");
        var manifest = new PlaybackQualityReferenceManifest();
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(referenceCase.CaseId, "hevc", 1920, 1080, 60, "Sdr");
        AddCapturedRuntimeMetrics(report);
        var entry = new PlaybackQualityReferenceReportSetEntry(report)
        {
            HasSignalPresenceEvidence = true
        };
        foreach (var signal in PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase))
        {
            entry.PresentSignals.Add(signal);
        }

        const string missingSignal = "timing.videoProcessorSetupCpuDurationMsP95";
        entry.PresentSignals.Remove(missingSignal);

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(manifest, new[] { entry });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == missingSignal);
    }

    [Fact]
    public void ValidateReportSet_Rejects_Contradictory_Native_Startup_Transport_Call_Status()
    {
        var referenceCase = CreateCase(
            "startup/contradictory-native-transport-status",
            tier: 1,
            purpose: "sdr-smoke");
        var manifest = new PlaybackQualityReferenceManifest();
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(referenceCase.CaseId, "hevc", 3840, 2160, 23.976, "Hdr10");
        AddCapturedRuntimeMetrics(report);
        AddStartupTransportCallEvidence(report, "instrumented-ffmpeg-avio", "unavailable");

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(manifest, new[] { report });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.startup.transport-call.contract.invalid" &&
            error.Signal.EndsWith(
                ".transportCallEvidenceStatus",
                System.StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateReportSet_Accepts_Mixed_Startup_Transport_Providers_When_Each_Component_Is_Consistent()
    {
        var referenceCase = CreateCase(
            "startup/mixed-native-transport-providers",
            tier: 2,
            purpose: "hdr-output");
        referenceCase.Category = "challenge";
        var manifest = new PlaybackQualityReferenceManifest();
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(referenceCase.CaseId, "hevc", 3840, 2160, 23.976, "Hdr10");
        AddCapturedRuntimeMetrics(report);
        report.ColorPipeline.ConversionStatus = "validated";

        var streamInfo = PlaybackQualityStartupTransportCallEvidence.FindComponent(
            report,
            "ffmpeg.find-stream-info");
        Assert.NotNull(streamInfo);
        streamInfo.TransportProvider = PlaybackQualityStartupTransportCallEvidence.InstrumentedProvider;
        streamInfo.TransportCallEvidenceStatus = PlaybackQualityStartupTransportCallEvidence.MeasuredStatus;
        streamInfo.TransportReadCalls = 2;
        streamInfo.TransportSeekCalls = 1;
        streamInfo.TransportReadWaitMs = 5;
        streamInfo.TransportSeekWaitMs = 3;
        streamInfo.TransportSeekDistanceBytes = 1024;

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(manifest, new[] { report });

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public void ValidateReportSet_Rejects_Playable_Report_Missing_Runtime_Metrics_State()
    {
        var referenceCase = CreateCase(
            "runtime-metrics/missing-state",
            tier: 1,
            purpose: "sdr-smoke");
        referenceCase.Expected.RequireValidatedConversion = false;
        var manifest = new PlaybackQualityReferenceManifest();
        manifest.Cases.Add(referenceCase);
        var report = CreateReport(
            "runtime-metrics/missing-state",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { new PlaybackQualityReferenceReportSetEntry(report) });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "runtimeMetrics.status" &&
            error.FailureClass == PlaybackQualityFailureClassification.InsufficientInstrumentation);
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "runtimeMetrics.providerStatus");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "runtimeMetrics.hasSnapshot");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.requiredSignal.missing" &&
            error.Signal == "runtimeMetrics.hasPlaybackSample");
    }

    [Fact]
    public void RequiredSignalPolicy_Requires_Track_And_Subtitle_Signals_For_Track_Purposes()
    {
        var referenceCase = CreateCase(
            "tracks/subtitle-rich",
            tier: 1,
            purpose: "tracks");
        referenceCase.Purpose.Add("subtitles");
        referenceCase.Purpose.Add("audio-switch");
        referenceCase.Purpose.Add("subtitle-switch");

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("tracks.videoTrackCount", requiredSignals);
        Assert.Contains("tracks.audioTrackCount", requiredSignals);
        Assert.Contains("tracks.subtitleTrackCount", requiredSignals);
        Assert.Contains("tracks.selectedAudioStreamIndex", requiredSignals);
        Assert.Contains("tracks.selectedSubtitleStreamIndex", requiredSignals);
        Assert.Contains("tracks.isSubtitleDisabled", requiredSignals);
        Assert.Contains("tracks.video.isExternal", requiredSignals);
        Assert.Contains("tracks.video.isDefault", requiredSignals);
        Assert.Contains("tracks.video.isForced", requiredSignals);
        Assert.Contains("tracks.audio.isExternal", requiredSignals);
        Assert.Contains("tracks.audio.channels", requiredSignals);
        Assert.Contains("tracks.audio.isDefault", requiredSignals);
        Assert.Contains("tracks.audio.isForced", requiredSignals);
        Assert.Contains("tracks.subtitles.isExternal", requiredSignals);
        Assert.Contains("tracks.subtitles.isDefault", requiredSignals);
        Assert.Contains("tracks.subtitles.isForced", requiredSignals);
    }

    [Fact]
    public void RequiredSignalPolicy_Requires_Error_Signals_For_Error_Handling_Purpose()
    {
        var referenceCase = CreateCase(
            "errors/missing-file",
            tier: 1,
            purpose: "error-handling");

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("error.code", requiredSignals);
        Assert.Contains("error.message", requiredSignals);
        Assert.Contains("error.failureClass", requiredSignals);
        Assert.Contains("error.failureArea", requiredSignals);
    }

    [Fact]
    public void RequiredSignalPolicy_Does_Not_Require_Color_Conversion_For_Explicit_Unsupported_Source()
    {
        var referenceCase = CreateCase(
            "jellyfin/dv-profile5-hevc-4k",
            tier: 3,
            purpose: "dv-reject");
        referenceCase.Expected.HdrKind = "DolbyVisionUnsupported";
        referenceCase.Expected.HdrPlaybackStrategy = "Dolby Vision unsupported";
        referenceCase.Expected.IsHdr = true;
        referenceCase.Expected.IsDirectPlayable = false;
        referenceCase.Expected.IsDolbyVision = true;
        referenceCase.Expected.DolbyVisionProfile = 5;
        referenceCase.Expected.HasHdr10BaseLayer = false;
        referenceCase.Expected.HasHlgBaseLayer = false;
        referenceCase.Expected.RequireValidatedConversion = true;

        var requiredSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase);

        Assert.Contains("source.hdrKind", requiredSignals);
        Assert.Contains("source.hdrPlaybackStrategy", requiredSignals);
        Assert.Contains("source.isDirectPlayable", requiredSignals);
        Assert.Contains("source.isDolbyVision", requiredSignals);
        Assert.Contains("source.dolbyVisionProfile", requiredSignals);
        Assert.DoesNotContain("colorPipeline.actualHdrOutput", requiredSignals);
        Assert.DoesNotContain("colorPipeline.dxgiInput", requiredSignals);
        Assert.DoesNotContain("colorPipeline.dxgiOutput", requiredSignals);
        Assert.DoesNotContain("colorPipeline.conversionStatus", requiredSignals);
    }

    [Fact]
    public void RequiredSignalPolicy_Recognizes_Source_And_Track_Report_Signals()
    {
        var report = new PlaybackQualityReport
        {
            Source = new PlaybackQualitySource
            {
                VideoMetadataProvider = "native-playback",
                VideoMetadataStatus = "observed",
                Container = "mkv",
                Bitrate = 76_000_000,
                DurationTicks = 70_200_000_000,
                HasChapterMetadata = true,
                ChapterCount = 1
            },
            RuntimeMetrics = new PlaybackQualityRuntimeMetrics
            {
                Status = "captured",
                ProviderStatus = "returned-snapshot",
                Reason = "Runtime metrics snapshot contains playback sample evidence.",
                HasSnapshot = true,
                HasPlaybackSample = true,
                ProcessWallClockMs = 5123.4,
                ProcessCpuTimeMs = 245.6,
                ProcessCpuUtilizationRatio = 0.048
            },
            Tracks = new PlaybackQualityTracks
            {
                VideoTrackCount = 1,
                AudioTrackCount = 2,
                SubtitleTrackCount = 1,
                SelectedAudioStreamIndex = 2,
                IsSubtitleDisabled = true
            },
            Timing = new PlaybackQualityTiming
            {
                AudioAheadWaitPassDurationMsP95 = 12.0,
                AudioAheadWaitPassTargetMsP95 = 4.0,
                AudioAheadWaitPassOversleepMsP95 = 8.0
            }
        };
        report.Tracks.Video.Add(new PlaybackQualityTrack
        {
            Index = 0,
            IsDefault = true,
            IsForced = false
        });
        report.Tracks.Audio.Add(new PlaybackQualityTrack
        {
            Index = 2,
            Channels = 8,
            IsDefault = true,
            IsForced = false
        });
        report.Tracks.Subtitles.Add(new PlaybackQualityTrack
        {
            Index = 3,
            IsDefault = false,
            IsForced = true
        });
        report.Source.Chapters.Add(new PlaybackQualityChapter
        {
            Name = "Opening",
            StartPositionTicks = 0,
            ImageTag = "chapter-0"
        });

        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.videoMetadataProvider"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.videoMetadataStatus"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.container"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.bitrate"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.durationTicks"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.hasChapterMetadata"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.chapterCount"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.chapters.name"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.chapters.startPositionTicks"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.chapters.imageTag"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "runtimeMetrics.status"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "runtimeMetrics.providerStatus"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "runtimeMetrics.reason"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "runtimeMetrics.hasSnapshot"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "runtimeMetrics.hasPlaybackSample"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "runtimeMetrics.processWallClockMs"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "runtimeMetrics.processCpuTimeMs"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "runtimeMetrics.processCpuUtilizationRatio"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.videoTrackCount"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.audioTrackCount"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.subtitleTrackCount"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.selectedAudioStreamIndex"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.isSubtitleDisabled"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.video.isExternal"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.video.isDefault"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.video.isForced"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.audio.isExternal"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.audio.channels"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.audio.isDefault"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.audio.isForced"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.subtitles.isExternal"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.subtitles.isDefault"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.subtitles.isForced"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "timing.audioAheadWaitPassDurationMsP95"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "timing.audioAheadWaitPassTargetMsP95"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "timing.audioAheadWaitPassOversleepMsP95"));
        Assert.False(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.selectedSubtitleStreamIndex"));
    }

    private static PlaybackQualityReferenceCase CreateCase(
        string caseId,
        int tier,
        string purpose)
    {
        var referenceCase = new PlaybackQualityReferenceCase
        {
            CaseId = caseId,
            Uri = "https://example.invalid/" + caseId + ".mp4",
            Tier = tier,
            Expected = new PlaybackQualityExpected
            {
                Codec = "hevc",
                Width = 3840,
                Height = 2160,
                FrameRate = 23.976,
                HdrKind = "Hdr10"
            }
        };

        if (!string.IsNullOrWhiteSpace(purpose))
        {
            referenceCase.Purpose.Add(purpose);
            referenceCase.ExecutionRequirement.Scenario = purpose switch
            {
                "timeline" => PlaybackQualityExecutionScenario.Timeline,
                "audio-switch" => PlaybackQualityExecutionScenario.AudioSwitch,
                "subtitle-switch" => PlaybackQualityExecutionScenario.SubtitleSwitch,
                "pause-resume" => PlaybackQualityExecutionScenario.PauseResume,
                "end-of-stream" => PlaybackQualityExecutionScenario.EndOfStream,
                _ => PlaybackQualityExecutionScenario.Playback
            };
            if (purpose == "timeline")
            {
                referenceCase.SeekTargetPositionTicks = 600_000_000;
            }
        }

        return referenceCase;
    }

    private static PlaybackDescriptor CreateDescriptor(
        string codec,
        int width,
        int height,
        double frameRate,
        HdrPlaybackKind hdrKind)
    {
        var source = new EmbyMediaSource
        {
            Id = "source-1",
            Width = width,
            Height = height,
            VideoFrameRate = frameRate,
            HdrProfile = new HdrPlaybackProfile
            {
                Kind = hdrKind,
                Codec = codec
            }
        };
        source.Streams.Add(new EmbyMediaStream
        {
            Kind = EmbyStreamKind.Video,
            Codec = codec,
            Index = 0
        });

        return new PlaybackDescriptor(
            "item-1",
            source,
            new[] { source },
            startPositionTicks: 0);
    }

    private static PlaybackQualityReport CreateReport(
        string runId,
        string codec,
        int width,
        int height,
        double frameRate,
        string hdrKind)
    {
        var report = new PlaybackQualityReport
        {
            RunId = runId,
            Result = "pass",
            Environment = new PlaybackQualityEnvironment
            {
                PlayerCoreVersion = "test-core",
                SourceRevision = "test-revision",
                BuildConfiguration = "Debug"
            },
            Source = new PlaybackQualitySource
            {
                Codec = codec,
                HasDirectStreamUrl = true,
                DirectStreamProtocol = "https",
                Width = width,
                Height = height,
                FrameRate = frameRate,
                HdrKind = hdrKind
            }
        };
        AddObservedLifecycle(report);
        AddNativeExecutionEvidence(
            report,
            new PlaybackQualityReferenceCase
            {
                CaseId = runId,
                Uri = "https://example.invalid/" + runId + ".mp4",
                ExecutionRequirement = new PlaybackQualityExecutionRequirement
                {
                    Scenario = PlaybackQualityExecutionScenario.Playback
                }
            },
            status: "completed",
            sourceOpened: true,
            decoderOpened: true,
            playbackSampleObserved: true);
        AddStartupTransportCallEvidence(
            report,
            PlaybackQualityStartupTransportCallEvidence.BuiltinProvider,
            PlaybackQualityStartupTransportCallEvidence.UnavailableStatus);
        return report;
    }

    [Fact]
    public void RequiredSignalPolicy_Requires_Pause_And_Resume_Only_For_Pause_Resume_Cases()
    {
        var playbackCase = CreateCase("lifecycle/playback", 1, "sdr-smoke");
        var playbackSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(playbackCase);

        Assert.Contains("lifecycle.load", playbackSignals);
        Assert.Contains("lifecycle.play", playbackSignals);
        Assert.Contains("lifecycle.stop", playbackSignals);
        Assert.DoesNotContain("lifecycle.pause", playbackSignals);
        Assert.DoesNotContain("lifecycle.resume", playbackSignals);

        var pauseResumeCase = CreateCase("lifecycle/pause-resume", 1, "pause-resume");
        pauseResumeCase.PauseSeconds = 30;
        var pauseResumeSignals = PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(pauseResumeCase);

        Assert.Contains("lifecycle.pause", pauseResumeSignals);
        Assert.Contains("lifecycle.resume", pauseResumeSignals);
    }

    private static void AddNativeExecutionEvidence(
        PlaybackQualityReport report,
        PlaybackQualityReferenceCase referenceCase,
        string status,
        bool sourceOpened,
        bool decoderOpened,
        bool playbackSampleObserved)
    {
        if (sourceOpened)
        {
            report.Source.VideoMetadataProvider = "native-playback";
            report.Source.VideoMetadataStatus = "observed";
        }

        report.Execution = new PlaybackQualityExecutionEvidence
        {
            AttemptId = "attempt-" + referenceCase.CaseId.Replace('/', '-'),
            Runner = "native-headless",
            Scenario = referenceCase.ExecutionRequirement.Scenario,
            EvidenceLevel = PlaybackQualityEvidenceLevel.NativePlayback,
            Status = status,
            SourceLocatorHash = PlaybackQualitySourceFingerprint.Compute(referenceCase.Uri),
            OpenedSourceHash = "",
            OpenedSourceHashKind = sourceOpened
                ? PlaybackQualitySourceFingerprint.OpenedMediaSignatureKind
                : "",
            StartedAtUtc = "2026-07-11T00:00:00Z",
            DurationMs = 5000,
            RequestedSampleDurationMs = 5000,
            ObservedSampleWallClockDurationMs = 5000,
            SourceOpenAttempted = true,
            SourceOpened = sourceOpened,
            NativeGraphOpened = sourceOpened,
            DemuxStarted = sourceOpened,
            DecoderOpened = decoderOpened,
            PlaybackSampleObserved = playbackSampleObserved
        };
        if (sourceOpened)
        {
            report.Execution.OpenedSourceHash =
                PlaybackQualitySourceFingerprint.ComputeOpenedMediaSignature(report);
        }
    }

    private static void AddObservedLifecycle(PlaybackQualityReport report)
    {
        AddObservedLifecycleEvent(report, "load");
        AddObservedLifecycleEvent(report, "play");
        AddObservedLifecycleEvent(report, "pause");
        AddObservedLifecycleEvent(report, "resume");
        AddObservedLifecycleEvent(report, "stop");
    }

    private static void AddTestEnvironment(PlaybackQualityReport report)
    {
        report.Environment.PlayerCoreVersion = "test-core";
        report.Environment.SourceRevision = "test-revision";
        report.Environment.BuildConfiguration = "Debug";
    }

    private static void AddCapturedRuntimeMetrics(PlaybackQualityReport report)
    {
        report.RuntimeMetrics = PlaybackQualityRuntimeMetricsFactory.FromSnapshot(
            new PlaybackQualityMetricsSnapshot
            {
                DecodedVideoFrames = 240,
                RenderedVideoFrames = 240,
                VideoRenderVideoProcessorFrameCount = 240
            },
            "test-provider:returned-snapshot");
        report.Timing.DecodedVideoFrames = 240;
        report.Timing.RenderedVideoFrames = 240;
        report.Timing.VideoRenderVideoProcessorFrameCount = 240;
        report.RuntimeMetrics.ProcessWallClockMs = 5123.4;
        report.RuntimeMetrics.ProcessCpuTimeMs = 245.6;
        report.RuntimeMetrics.ProcessCpuUtilizationRatio = 0.048;
    }

    private static void AddStartupTransportCallEvidence(
        PlaybackQualityReport report,
        string provider,
        string status)
    {
        report.Startup.Stages.RemoveAll(stage =>
            string.Equals(
                stage.Name,
                PlaybackQualityStartupTransportCallEvidence.StageName,
                System.StringComparison.Ordinal));
        var stage = new PlaybackQualityStartupStage
        {
            Name = PlaybackQualityStartupTransportCallEvidence.StageName,
            DurationMs = 100
        };
        foreach (var componentName in new[]
        {
            "ffmpeg.open-input",
            "ffmpeg.find-stream-info",
            "native.startup-seek",
            "native.first-frame.demux-read"
        })
        {
            var component = new PlaybackQualityStartupComponent
            {
                Name = componentName,
                DurationMs = 25,
                TransportProvider = provider,
                TransportCallEvidenceStatus = status
            };
            if (string.Equals(
                status,
                PlaybackQualityStartupTransportCallEvidence.MeasuredStatus,
                System.StringComparison.Ordinal))
            {
                component.TransportReadCalls = 2;
                component.TransportSeekCalls = 1;
                component.TransportReadWaitMs = 5;
                component.TransportSeekWaitMs = 3;
                component.TransportSeekDistanceBytes = 1024;
            }

            stage.Components.Add(component);
        }

        report.Startup.Stages.Add(stage);
    }

    private static void AddObservedLifecycleEvent(
        PlaybackQualityReport report,
        string operation)
    {
        report.Lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = operation,
            Status = "observed"
        });
    }
}
