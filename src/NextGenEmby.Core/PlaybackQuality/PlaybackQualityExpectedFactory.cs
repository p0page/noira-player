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
                FrameRate = source.VideoFrameRate > 0 ? source.VideoFrameRate : 0,
                HdrOutput = MapExpectedHdrOutput(source.HdrProfile.Kind),
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
    }
}
