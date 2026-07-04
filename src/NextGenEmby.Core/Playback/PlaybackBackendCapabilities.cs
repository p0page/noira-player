using System;

namespace NextGenEmby.Core.Playback
{
    [Flags]
    public enum PlaybackBackendFeature
    {
        None = 0,
        DirectPlayHttp = 1,
        Hevc = 2,
        HevcMain10 = 4,
        Hdr10 = 8,
        AudioStreamSwitching = 16,
        SubtitleStreamSwitching = 32,
        MediaSourceSwitching = 64,
        Transcoding = 128
    }

    public sealed class PlaybackBackendCapabilities
    {
        public PlaybackBackendCapabilities(PlaybackBackendFeature features)
        {
            Features = features;
        }

        public PlaybackBackendFeature Features { get; }

        public bool Supports(PlaybackBackendFeature feature)
        {
            return (Features & feature) == feature;
        }
    }
}
