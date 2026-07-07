using System.Collections.Generic;
using System.Linq;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using NextGenEmby.Core.PlaybackQuality;
using Xunit;

namespace NextGenEmby.Core.Tests.PlaybackQuality;

public sealed class PlaybackQualityReferenceManifestTests
{
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
        Assert.Contains("error-handling", result.Coverage.MissingPurposes);
        Assert.Contains("reference manifest is missing required playback quality purposes", result.Coverage.Reasons);
        Assert.Contains("Add reference cases", result.Coverage.SuggestedNextAction);
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
        Assert.Contains("error-handling", result.Coverage.CoveredPurposes);
        Assert.Contains("reference manifest covers required playback quality purposes", result.Coverage.Reasons);
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
        referenceCase.ForceSdrOutput = true;
        manifest.Cases.Add(referenceCase);

        var result = PlaybackQualityReferenceManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Contains(result.Cases, item =>
            item.CaseId == "emby/007-hdr10" &&
            item.ItemId == "item-007" &&
            item.MediaSourceId == "source-hdr10" &&
            item.StartPositionTicks == 123 &&
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
        report.ColorPipeline.ConversionStatus = "validated";

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

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

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            new[] { report });

        Assert.True(validation.IsValid);
        Assert.Equal(1, validation.MatchedCaseCount);
        Assert.Empty(validation.Errors);
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
            "src/NextGenEmby.Native/DxDeviceResources.cpp",
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
        entry.PresentSignals.Add("source.codec");
        entry.PresentSignals.Add("source.width");
        entry.PresentSignals.Add("source.height");
        entry.PresentSignals.Add("source.frameRate");
        entry.PresentSignals.Add("source.hdrKind");
        entry.PresentSignals.Add("colorPipeline.conversionStatus");
        entry.PresentSignals.Add("buffers.videoStarvedPasses");
        entry.PresentSignals.Add("buffers.audioStarvedPasses");

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
                Container = "mkv",
                Bitrate = 76_000_000,
                DurationTicks = 70_200_000_000
            },
            Tracks = new PlaybackQualityTracks
            {
                VideoTrackCount = 1,
                AudioTrackCount = 2,
                SubtitleTrackCount = 1,
                SelectedAudioStreamIndex = 2,
                IsSubtitleDisabled = true
            }
        };

        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.container"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.bitrate"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "source.durationTicks"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.videoTrackCount"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.audioTrackCount"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.subtitleTrackCount"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.selectedAudioStreamIndex"));
        Assert.True(PlaybackQualityRequiredSignalPolicy.HasReportSignal(report, "tracks.isSubtitleDisabled"));
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
        return new PlaybackQualityReport
        {
            RunId = runId,
            Source = new PlaybackQualitySource
            {
                Codec = codec,
                Width = width,
                Height = height,
                FrameRate = frameRate,
                HdrKind = hdrKind
            }
        };
    }
}
