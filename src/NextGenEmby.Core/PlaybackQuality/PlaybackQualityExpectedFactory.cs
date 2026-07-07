using System;
using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityExpectedFactory
    {
        private const double DefaultSampleDurationSeconds = 5.0;
        private const double MaxFrameGapFrameMultiplier = 2.5;
        private const double MaxRenderIntervalP95FrameMultiplier = 1.25;
        private const double MaxRenderIntervalP99FrameMultiplier = 2.0;

        public static PlaybackQualityExpected CreateDefault(PlaybackDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var source = descriptor.MediaSource;
            var expected = new PlaybackQualityExpected
            {
                Codec = source.HdrProfile.Codec ?? "",
                Width = source.Width,
                Height = source.Height,
                FrameRate = source.VideoFrameRate > 0 ? source.VideoFrameRate : 0,
                HdrKind = source.HdrProfile.Kind.ToString(),
                VideoRange = source.HdrProfile.VideoRange,
                ColorPrimaries = source.HdrProfile.ColorPrimaries,
                ColorTransfer = source.HdrProfile.ColorTransfer,
                ColorSpace = source.HdrProfile.ColorSpace,
                HdrPlaybackStrategy = source.HdrProfile.PlaybackStrategy,
                IsHdr = source.HdrProfile.IsHdr,
                IsDirectPlayable = source.HdrProfile.IsDirectPlayable,
                IsDolbyVision = source.HdrProfile.IsDolbyVision,
                DolbyVisionProfile = source.HdrProfile.DolbyVisionProfile,
                DolbyVisionCompatibilityId = source.HdrProfile.DolbyVisionCompatibilityId,
                HasHdr10BaseLayer = source.HdrProfile.HasHdr10BaseLayer,
                HasHlgBaseLayer = source.HdrProfile.HasHlgBaseLayer,
                HdrOutput = MapExpectedHdrOutput(source.HdrProfile.Kind),
                DxgiInput = MapExpectedDxgiInput(source.HdrProfile.Kind),
                DxgiOutput = MapExpectedDxgiOutput(source.HdrProfile.Kind),
                MaxDroppedFrames = 0,
                MaxAudioVideoDriftMsP95 = 40,
                MaxVideoStarvedPasses = 0,
                MaxAudioStarvedPasses = 0,
                RequireValidatedConversion = true
            };

            if (!PlaybackRefreshRatePolicy.HasUsableVideoFrameRate(expected.FrameRate))
            {
                expected.RequireMatchedDisplayRefreshRate = false;
                return expected;
            }

            var frameDurationMs = 1000.0 / expected.FrameRate;
            expected.MinRenderedVideoFrames = (long)Math.Ceiling(
                expected.FrameRate * DefaultSampleDurationSeconds);
            expected.MaxFrameGapMs = frameDurationMs * MaxFrameGapFrameMultiplier;
            expected.MaxRenderIntervalMsP95 = frameDurationMs * MaxRenderIntervalP95FrameMultiplier;
            expected.MaxRenderIntervalMsP99 = frameDurationMs * MaxRenderIntervalP99FrameMultiplier;
            expected.RequireMatchedDisplayRefreshRate = true;
            return expected;
        }

        private static string MapExpectedHdrOutput(HdrPlaybackKind kind)
        {
            switch (kind)
            {
                case HdrPlaybackKind.Sdr:
                    return "Sdr";
                case HdrPlaybackKind.DolbyVisionUnsupported:
                    return "Unsupported";
                case HdrPlaybackKind.Hdr10:
                case HdrPlaybackKind.DolbyVisionWithHdr10Fallback:
                case HdrPlaybackKind.UnknownHdr:
                    return "Hdr10";
                default:
                    return "";
            }
        }

        private static string MapExpectedDxgiInput(HdrPlaybackKind kind)
        {
            switch (kind)
            {
                case HdrPlaybackKind.Sdr:
                    return "YCBCR_STUDIO_G22_LEFT_P709";
                case HdrPlaybackKind.Hdr10:
                case HdrPlaybackKind.DolbyVisionWithHdr10Fallback:
                case HdrPlaybackKind.UnknownHdr:
                    return "YCBCR_STUDIO_G2084_TOPLEFT_P2020";
                default:
                    return "";
            }
        }

        private static string MapExpectedDxgiOutput(HdrPlaybackKind kind)
        {
            switch (kind)
            {
                case HdrPlaybackKind.Sdr:
                    return "RGB_FULL_G22_NONE_P709";
                case HdrPlaybackKind.Hdr10:
                case HdrPlaybackKind.DolbyVisionWithHdr10Fallback:
                case HdrPlaybackKind.UnknownHdr:
                    return "RGB_FULL_G2084_NONE_P2020";
                default:
                    return "";
            }
        }
    }
}
