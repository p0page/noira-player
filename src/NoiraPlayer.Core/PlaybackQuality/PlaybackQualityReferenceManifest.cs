using System.Collections.Generic;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityReferenceManifest
    {
        public int SchemaVersion { get; set; } = 1;

        public List<PlaybackQualityReferenceCase> Cases { get; } =
            new List<PlaybackQualityReferenceCase>();
    }

    public sealed class PlaybackQualityReferenceCase
    {
        public string CaseId { get; set; } = "";

        public string Category { get; set; } = "stable";

        public string Severity { get; set; } = "medium";

        public string Stability { get; set; } = "stable";

        public string Uri { get; set; } = "";

        public string ItemId { get; set; } = "";

        public string MediaSourceId { get; set; } = "";

        public long StartPositionTicks { get; set; }

        public bool ForceSdrOutput { get; set; }

        public int PauseSeconds { get; set; }

        public int Tier { get; set; }

        public List<string> Purpose { get; } = new List<string>();

        public PlaybackQualityExpected Expected { get; set; } =
            new PlaybackQualityExpected();

        public PlaybackQualityExecutionRequirement ExecutionRequirement { get; set; } =
            new PlaybackQualityExecutionRequirement();
    }

    public sealed class PlaybackQualityReferenceManifestValidation
    {
        public int SchemaVersion { get; set; } = 1;
        public string EvaluationVersion { get; set; } =
            PlaybackQualityRunResult.CurrentEvaluationVersion;

        public bool IsValid => Errors.Count == 0;

        public int CaseCount { get; set; }

        public PlaybackQualityReferenceManifestCoverage Coverage { get; set; } =
            new PlaybackQualityReferenceManifestCoverage();

        public List<int> Tiers { get; } = new List<int>();

        public List<string> Purposes { get; } = new List<string>();

        public List<string> Categories { get; } = new List<string>();

        public List<string> Severities { get; } = new List<string>();

        public List<string> Stabilities { get; } = new List<string>();

        public List<PlaybackQualityReferenceCase> Cases { get; } =
            new List<PlaybackQualityReferenceCase>();

        public List<PlaybackQualityReferenceManifestError> Errors { get; } =
            new List<PlaybackQualityReferenceManifestError>();
    }

    public sealed class PlaybackQualityReferenceManifestCoverage
    {
        public string Status { get; set; } = "incomplete";

        public bool IsCoreEvaluationReady => MissingPurposes.Count == 0;

        public string SuggestedNextAction { get; set; } = "";

        public List<string> RequiredPurposes { get; } = new List<string>();

        public List<string> CoveredPurposes { get; } = new List<string>();

        public List<string> MissingPurposes { get; } = new List<string>();

        public List<string> Reasons { get; } = new List<string>();
    }

    public sealed class PlaybackQualityReferenceManifestError
    {
        public string Code { get; set; } = "";

        public string CaseId { get; set; } = "";

        public string Signal { get; set; } = "";

        public string Message { get; set; } = "";
    }

    public static class PlaybackQualityReferenceManifestValidator
    {
        private static readonly string[] RequiredCoreEvaluationPurposes =
        {
            "sdr-smoke",
            "hdr-output",
            "hdr-force-sdr",
            "dv-reject",
            "dv-fallback",
            "cadence-23.976",
            "frame-pacing",
            "av-sync",
            "buffering",
            "timeline",
            "tracks",
            "subtitles",
            "end-of-stream",
            "error-handling"
        };

        public static PlaybackQualityReferenceManifestValidation Validate(
            PlaybackQualityReferenceManifest manifest)
        {
            var validation = new PlaybackQualityReferenceManifestValidation();
            if (manifest == null)
            {
                AddError(
                    validation,
                    "manifest.missing",
                    "",
                    "manifest",
                    "Playback quality reference manifest is missing.");
                ApplyCoverage(validation);
                return validation;
            }

            validation.CaseCount = manifest.Cases.Count;
            var caseIds = new HashSet<string>();
            foreach (var referenceCase in manifest.Cases)
            {
                ValidateCase(validation, referenceCase, caseIds);
            }

            ApplyCoverage(validation);
            return validation;
        }

        private static void ValidateCase(
            PlaybackQualityReferenceManifestValidation validation,
            PlaybackQualityReferenceCase referenceCase,
            HashSet<string> caseIds)
        {
            if (referenceCase == null)
            {
                AddError(
                    validation,
                    "case.missing",
                    "",
                    "case",
                    "Playback quality reference case is missing.");
                return;
            }

            validation.Cases.Add(CloneCase(referenceCase));

            var caseId = referenceCase.CaseId ?? "";
            var category = NormalizeCaseCategory(referenceCase.Category);
            var severity = NormalizeCaseSeverity(referenceCase.Severity);
            var stability = NormalizeCaseStability(referenceCase.Stability);
            if (string.IsNullOrWhiteSpace(caseId))
            {
                AddError(
                    validation,
                    "case.id.missing",
                    "",
                    "caseId",
                    "Playback quality reference case requires caseId.");
            }
            else if (!caseIds.Add(caseId))
            {
                AddError(
                    validation,
                    "case.duplicate-id",
                    caseId,
                    "caseId",
                    "Playback quality reference caseId is duplicated.");
            }

            if (!IsValidCaseCategory(category))
            {
                AddError(
                    validation,
                    "case.category.invalid",
                    caseId,
                    "category",
                    "Playback quality reference category must be stable, challenge, or quarantine.");
            }
            else
            {
                AddUnique(validation.Categories, category);
            }

            if (!IsValidCaseSeverity(severity))
            {
                AddError(
                    validation,
                    "case.severity.invalid",
                    caseId,
                    "severity",
                    "Playback quality reference severity must be info, low, medium, high, or critical.");
            }
            else
            {
                AddUnique(validation.Severities, severity);
            }

            if (!IsValidCaseStability(stability))
            {
                AddError(
                    validation,
                    "case.stability.invalid",
                    caseId,
                    "stability",
                    "Playback quality reference stability must be stable, variable, flaky, or unknown.");
            }
            else
            {
                AddUnique(validation.Stabilities, stability);
            }

            if (string.IsNullOrWhiteSpace(referenceCase.Uri))
            {
                AddError(
                    validation,
                    "case.uri.missing",
                    caseId,
                    "uri",
                    "Playback quality reference case requires uri.");
            }

            if (referenceCase.Tier < 0 || referenceCase.Tier > 4)
            {
                AddError(
                    validation,
                    "case.tier.invalid",
                    caseId,
                    "tier",
                    "Playback quality reference tier must be between 0 and 4.");
            }
            else
            {
                AddUnique(validation.Tiers, referenceCase.Tier);
            }

            if (referenceCase.PauseSeconds < 0 || referenceCase.PauseSeconds > 900)
            {
                AddError(
                    validation,
                    "case.pause-seconds.invalid",
                    caseId,
                    "pauseSeconds",
                    "Playback quality reference pauseSeconds must be between 0 and 900.");
            }

            if (referenceCase.Purpose.Count == 0)
            {
                AddError(
                    validation,
                    "case.purpose.missing",
                    caseId,
                    "purpose",
                    "Playback quality reference case requires at least one purpose.");
            }
            else
            {
                foreach (var purpose in referenceCase.Purpose)
                {
                    AddUnique(validation.Purposes, purpose);
                }
            }

            var minimumEvidenceLevel = referenceCase.ExecutionRequirement?.MinimumEvidenceLevel ?? "";
            if (!PlaybackQualityEvidenceLevel.IsKnown(minimumEvidenceLevel))
            {
                AddError(
                    validation,
                    "case.execution.minimum-evidence-level.invalid",
                    caseId,
                    "executionRequirement.minimumEvidenceLevel",
                    "Playback quality reference case requires a known minimum execution evidence level.");
            }

            ValidateExpected(validation, caseId, referenceCase.Expected);
        }

        private static PlaybackQualityReferenceCase CloneCase(
            PlaybackQualityReferenceCase source)
        {
            var clone = new PlaybackQualityReferenceCase
            {
                CaseId = source.CaseId,
                Category = NormalizeCaseCategory(source.Category),
                Severity = NormalizeCaseSeverity(source.Severity),
                Stability = NormalizeCaseStability(source.Stability),
                Uri = source.Uri,
                ItemId = source.ItemId,
                MediaSourceId = source.MediaSourceId,
                StartPositionTicks = source.StartPositionTicks,
                ForceSdrOutput = source.ForceSdrOutput,
                PauseSeconds = source.PauseSeconds,
                Tier = source.Tier,
                ExecutionRequirement = new PlaybackQualityExecutionRequirement
                {
                    MinimumEvidenceLevel = source.ExecutionRequirement?.MinimumEvidenceLevel ?? ""
                },
                Expected = CloneExpected(source.Expected)
            };

            foreach (var purpose in source.Purpose)
            {
                AddUnique(clone.Purpose, purpose);
            }

            return clone;
        }

        private static string NormalizeCaseCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? "stable" : category;
        }

        private static bool IsValidCaseCategory(string category)
        {
            return category == "stable" ||
                category == "challenge" ||
                category == "quarantine";
        }

        private static string NormalizeCaseSeverity(string severity)
        {
            return string.IsNullOrWhiteSpace(severity) ? "medium" : severity;
        }

        private static bool IsValidCaseSeverity(string severity)
        {
            return severity == "info" ||
                severity == "low" ||
                severity == "medium" ||
                severity == "high" ||
                severity == "critical";
        }

        private static string NormalizeCaseStability(string stability)
        {
            return string.IsNullOrWhiteSpace(stability) ? "stable" : stability;
        }

        private static bool IsValidCaseStability(string stability)
        {
            return stability == "stable" ||
                stability == "variable" ||
                stability == "flaky" ||
                stability == "unknown";
        }

        private static PlaybackQualityExpected CloneExpected(PlaybackQualityExpected source)
        {
            if (source == null)
            {
                return new PlaybackQualityExpected();
            }

            return new PlaybackQualityExpected
            {
                Codec = source.Codec,
                Width = source.Width,
                Height = source.Height,
                FrameRate = source.FrameRate,
                HdrKind = source.HdrKind,
                VideoRange = source.VideoRange,
                ColorPrimaries = source.ColorPrimaries,
                ColorTransfer = source.ColorTransfer,
                ColorSpace = source.ColorSpace,
                HdrPlaybackStrategy = source.HdrPlaybackStrategy,
                IsHdr = source.IsHdr,
                IsDirectPlayable = source.IsDirectPlayable,
                IsDolbyVision = source.IsDolbyVision,
                DolbyVisionProfile = source.DolbyVisionProfile,
                DolbyVisionCompatibilityId = source.DolbyVisionCompatibilityId,
                HasHdr10BaseLayer = source.HasHdr10BaseLayer,
                HasHlgBaseLayer = source.HasHlgBaseLayer,
                HdrOutput = source.HdrOutput,
                DxgiInput = source.DxgiInput,
                DxgiOutput = source.DxgiOutput,
                MaxStartupDurationMs = source.MaxStartupDurationMs,
                MinRenderedVideoFrames = source.MinRenderedVideoFrames,
                MaxDroppedFrames = source.MaxDroppedFrames,
                MaxFrameGapMs = source.MaxFrameGapMs,
                MaxRenderIntervalMsP95 = source.MaxRenderIntervalMsP95,
                MaxRenderIntervalMsP99 = source.MaxRenderIntervalMsP99,
                MaxAudioVideoDriftMsP95 = source.MaxAudioVideoDriftMsP95,
                MaxSeekPositionErrorMs = source.MaxSeekPositionErrorMs,
                MaxVideoStarvedPasses = source.MaxVideoStarvedPasses,
                MaxAudioStarvedPasses = source.MaxAudioStarvedPasses,
                RequireValidatedConversion = source.RequireValidatedConversion,
                RequireMatchedDisplayRefreshRate = source.RequireMatchedDisplayRefreshRate
            };
        }

        private static void ValidateExpected(
            PlaybackQualityReferenceManifestValidation validation,
            string caseId,
            PlaybackQualityExpected expected)
        {
            if (expected == null)
            {
                AddError(
                    validation,
                    "case.expected.missing",
                    caseId,
                    "expected",
                    "Playback quality reference case requires expected metadata.");
                return;
            }

            if (string.IsNullOrWhiteSpace(expected.Codec))
            {
                AddExpectedMissing(validation, caseId, "codec");
            }

            if (expected.Width <= 0)
            {
                AddExpectedMissing(validation, caseId, "width");
            }

            if (expected.Height <= 0)
            {
                AddExpectedMissing(validation, caseId, "height");
            }

            if (expected.FrameRate <= 0)
            {
                AddExpectedMissing(validation, caseId, "frameRate");
            }

            if (string.IsNullOrWhiteSpace(expected.HdrKind))
            {
                AddExpectedMissing(validation, caseId, "hdrKind");
            }
        }

        private static void AddExpectedMissing(
            PlaybackQualityReferenceManifestValidation validation,
            string caseId,
            string field)
        {
            AddError(
                validation,
                "case.expected." + field + ".missing",
                caseId,
                "expected." + field,
                "Playback quality reference expected." + field + " is required.");
        }

        private static void ApplyCoverage(PlaybackQualityReferenceManifestValidation validation)
        {
            validation.Coverage = new PlaybackQualityReferenceManifestCoverage();
            foreach (var purpose in RequiredCoreEvaluationPurposes)
            {
                AddUnique(validation.Coverage.RequiredPurposes, purpose);
                if (HasExecutablePurpose(validation.Cases, purpose))
                {
                    AddUnique(validation.Coverage.CoveredPurposes, purpose);
                }
                else
                {
                    AddUnique(validation.Coverage.MissingPurposes, purpose);
                }
            }

            if (validation.Coverage.MissingPurposes.Count == 0)
            {
                validation.Coverage.Status = "ready";
                validation.Coverage.SuggestedNextAction =
                    "Use this manifest for baseline/candidate playback Core evaluation.";
                AddUnique(
                    validation.Coverage.Reasons,
                    "reference manifest covers required playback quality purposes");
                return;
            }

            validation.Coverage.Status = "incomplete";
            validation.Coverage.SuggestedNextAction =
                "Add reference cases for missing playback quality purposes before relying on broad Core candidate evaluation.";
            AddUnique(
                validation.Coverage.Reasons,
                "reference manifest is missing required playback quality purposes");
        }

        private static bool HasExecutablePurpose(
            IReadOnlyList<PlaybackQualityReferenceCase> cases,
            string purpose)
        {
            foreach (var referenceCase in cases)
            {
                if (NormalizeCaseCategory(referenceCase.Category) != "quarantine" &&
                    referenceCase.Purpose.Contains(purpose))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddError(
            PlaybackQualityReferenceManifestValidation validation,
            string code,
            string caseId,
            string signal,
            string message)
        {
            validation.Errors.Add(new PlaybackQualityReferenceManifestError
            {
                Code = code,
                CaseId = caseId,
                Signal = signal,
                Message = message
            });
        }

        private static void AddUnique(List<int> values, int value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }
    }
}
