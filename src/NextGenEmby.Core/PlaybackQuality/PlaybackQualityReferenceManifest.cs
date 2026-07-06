using System.Collections.Generic;

namespace NextGenEmby.Core.PlaybackQuality
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

        public string Uri { get; set; } = "";

        public int Tier { get; set; }

        public List<string> Purpose { get; } = new List<string>();

        public PlaybackQualityExpected Expected { get; set; } =
            new PlaybackQualityExpected();
    }

    public sealed class PlaybackQualityReferenceManifestValidation
    {
        public bool IsValid => Errors.Count == 0;

        public int CaseCount { get; set; }

        public List<int> Tiers { get; } = new List<int>();

        public List<string> Purposes { get; } = new List<string>();

        public List<PlaybackQualityReferenceCase> Cases { get; } =
            new List<PlaybackQualityReferenceCase>();

        public List<PlaybackQualityReferenceManifestError> Errors { get; } =
            new List<PlaybackQualityReferenceManifestError>();
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
                return validation;
            }

            validation.CaseCount = manifest.Cases.Count;
            var caseIds = new HashSet<string>();
            foreach (var referenceCase in manifest.Cases)
            {
                ValidateCase(validation, referenceCase, caseIds);
            }

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

            ValidateExpected(validation, caseId, referenceCase.Expected);
        }

        private static PlaybackQualityReferenceCase CloneCase(
            PlaybackQualityReferenceCase source)
        {
            var clone = new PlaybackQualityReferenceCase
            {
                CaseId = source.CaseId,
                Uri = source.Uri,
                Tier = source.Tier,
                Expected = CloneExpected(source.Expected)
            };

            foreach (var purpose in source.Purpose)
            {
                AddUnique(clone.Purpose, purpose);
            }

            return clone;
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
