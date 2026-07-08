namespace NoiraPlayer.Core.Playback
{
    public sealed class HdrPlaybackProfile
    {
        public HdrPlaybackKind Kind { get; set; } = HdrPlaybackKind.Sdr;

        public string VideoRange { get; set; } = "";

        public string ColorPrimaries { get; set; } = "";

        public string ColorTransfer { get; set; } = "";

        public string ColorSpace { get; set; } = "";

        public string Codec { get; set; } = "";

        public string DisplayTitle { get; set; } = "";

        public string MediaSourceName { get; set; } = "";

        public bool IsDolbyVision { get; set; }

        public int? DolbyVisionProfile { get; set; }

        public int? DolbyVisionCompatibilityId { get; set; }

        public bool HasHdr10BaseLayer { get; set; }

        public bool HasHlgBaseLayer { get; set; }

        public bool IsHdr => Kind != HdrPlaybackKind.Sdr;

        public bool IsDirectPlayable => Kind != HdrPlaybackKind.DolbyVisionUnsupported;

        public int SelectionRank
        {
            get
            {
                switch (Kind)
                {
                    case HdrPlaybackKind.Sdr:
                        return 0;
                    case HdrPlaybackKind.Hdr10:
                        return 1;
                    case HdrPlaybackKind.Hlg:
                        return 2;
                    case HdrPlaybackKind.DolbyVisionWithHdr10Fallback:
                        return 3;
                    case HdrPlaybackKind.DolbyVisionWithHlgFallback:
                        return 4;
                    case HdrPlaybackKind.UnknownHdr:
                        return 5;
                    case HdrPlaybackKind.DolbyVisionUnsupported:
                        return 100;
                    default:
                        return 100;
                }
            }
        }

        public string PlaybackStrategy
        {
            get
            {
                switch (Kind)
                {
                    case HdrPlaybackKind.Sdr:
                        return "SDR";
                    case HdrPlaybackKind.Hdr10:
                        return "HDR10";
                    case HdrPlaybackKind.Hlg:
                        return "HLG";
                    case HdrPlaybackKind.DolbyVisionWithHdr10Fallback:
                        return "HDR10 fallback from Dolby Vision";
                    case HdrPlaybackKind.DolbyVisionWithHlgFallback:
                        return "HLG fallback from Dolby Vision";
                    case HdrPlaybackKind.DolbyVisionUnsupported:
                        return "Dolby Vision unsupported";
                    case HdrPlaybackKind.UnknownHdr:
                        return "Unknown HDR";
                    default:
                        return "Unknown HDR";
                }
            }
        }

        public static HdrPlaybackProfile Sdr()
        {
            return new HdrPlaybackProfile { Kind = HdrPlaybackKind.Sdr };
        }

        public static HdrPlaybackProfile LegacyHdr()
        {
            return new HdrPlaybackProfile { Kind = HdrPlaybackKind.UnknownHdr };
        }
    }
}
