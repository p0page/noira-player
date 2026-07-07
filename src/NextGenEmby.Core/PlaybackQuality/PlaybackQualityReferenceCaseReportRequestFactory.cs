using System;
using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityReferenceCaseReportRequestFactory
    {
        public static PlaybackQualityReportRequest CreateRequest(
            PlaybackQualityReferenceCase referenceCase,
            PlaybackDescriptor descriptor,
            PlaybackDisplayStatus? displayStatus = null,
            PlaybackQualityMetricsSnapshot? metrics = null,
            PlaybackQualityStartup? startup = null)
        {
            if (referenceCase == null)
            {
                throw new ArgumentNullException(nameof(referenceCase));
            }

            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            return new PlaybackQualityReportRequest
            {
                RunId = referenceCase.CaseId,
                CaseMetadata = new PlaybackQualityCaseMetadata
                {
                    CaseId = referenceCase.CaseId,
                    Category = string.IsNullOrWhiteSpace(referenceCase.Category)
                        ? "stable"
                        : referenceCase.Category,
                    Severity = string.IsNullOrWhiteSpace(referenceCase.Severity)
                        ? "medium"
                        : referenceCase.Severity,
                    Stability = string.IsNullOrWhiteSpace(referenceCase.Stability)
                        ? "stable"
                        : referenceCase.Stability
                },
                Descriptor = descriptor,
                DisplayStatus = displayStatus,
                Metrics = metrics,
                Startup = startup,
                Expected = CloneExpected(referenceCase.Expected),
                UseDefaultExpectedWhenMissing = false
            };
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
    }
}
