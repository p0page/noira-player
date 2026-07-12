using System;
using NoiraPlayer.Core.Playback;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityCaptureReferenceCaseFactory
    {
        public static PlaybackQualityReferenceCase Create(
            string runId,
            PlaybackDescriptor descriptor,
            PlaybackQualityExpected? expected,
            string scenario = PlaybackQualityExecutionScenario.Playback,
            string category = "stable",
            string severity = "medium",
            string stability = "stable",
            string uri = "")
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var source = descriptor.MediaSource;
            var referenceCase = new PlaybackQualityReferenceCase
            {
                CaseId = Normalize(runId, nameof(runId)),
                Category = NormalizeOrDefault(category, "stable"),
                Severity = NormalizeOrDefault(severity, "medium"),
                Stability = NormalizeOrDefault(stability, "stable"),
                Uri = string.IsNullOrWhiteSpace(uri) ? source.DirectStreamUrl ?? "" : uri.Trim(),
                ItemId = descriptor.ItemId,
                MediaSourceId = source.Id ?? "",
                StartPositionTicks = descriptor.StartPositionTicks,
                Expected = CloneExpected(expected)
            };
            referenceCase.ExecutionRequirement.Scenario = NormalizeScenario(scenario);
            return referenceCase;
        }

        public static PlaybackQualityReferenceCase Create(
            string runId,
            string itemId,
            string mediaSourceId,
            long startPositionTicks,
            bool forceSdrOutput,
            PlaybackQualityExpected? expected,
            string scenario = PlaybackQualityExecutionScenario.Playback,
            string uri = "",
            string category = "stable",
            string severity = "medium",
            string stability = "stable")
        {
            var referenceCase = new PlaybackQualityReferenceCase
            {
                CaseId = Normalize(runId, nameof(runId)),
                Category = NormalizeOrDefault(category, "stable"),
                Severity = NormalizeOrDefault(severity, "medium"),
                Stability = NormalizeOrDefault(stability, "stable"),
                Uri = uri ?? "",
                ItemId = itemId ?? "",
                MediaSourceId = mediaSourceId ?? "",
                StartPositionTicks = startPositionTicks < 0 ? 0 : startPositionTicks,
                ForceSdrOutput = forceSdrOutput,
                Expected = CloneExpected(expected)
            };
            referenceCase.ExecutionRequirement.Scenario = NormalizeScenario(scenario);
            return referenceCase;
        }

        private static string NormalizeScenario(string scenario)
        {
            var normalized = string.IsNullOrWhiteSpace(scenario)
                ? ""
                : scenario.Trim().ToLowerInvariant();
            if (!PlaybackQualityExecutionScenario.IsKnown(normalized))
            {
                throw new ArgumentException(
                    "Playback quality capture scenario is unknown.",
                    nameof(scenario));
            }

            return normalized;
        }

        private static string Normalize(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Playback quality capture value is required.", parameterName);
            }

            return value.Trim();
        }

        private static string NormalizeOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static PlaybackQualityExpected CloneExpected(PlaybackQualityExpected? source)
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
    }
}
