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

        Assert.Equal("netflix/chimera-4k-2398-hdr-pq", request.RunId);
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
        manifest.Cases.Add(CreateCase(
            "netflix/chimera-4k-2398-hdr-pq",
            tier: 2,
            purpose: "hdr-output"));
        var report = CreateReport(
            "netflix/chimera-4k-2398-hdr-pq",
            codec: "hevc",
            width: 3840,
            height: 2160,
            frameRate: 23.976,
            hdrKind: "Hdr10");

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
            item.Status == "matched" &&
            item.ReportRunId == "netflix/chimera-4k-2398-hdr-pq");
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
            error.CaseId == "netflix/chimera-4k-2398-hdr-pq");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.missing" &&
            error.CaseId == "jellyfin/dv-profile5-hevc-4k");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.extra" &&
            error.ReportRunId == "unexpected/case");
        Assert.Contains(validation.Errors, error =>
            error.Code == "report.source.codec.mismatch" &&
            error.Signal == "source.codec");
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
