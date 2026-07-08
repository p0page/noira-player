namespace NoiraPlayer.Core.Playback
{
    public enum HdrPlaybackKind
    {
        Sdr = 0,
        Hdr10,
        Hlg,
        DolbyVisionWithHdr10Fallback,
        DolbyVisionWithHlgFallback,
        DolbyVisionUnsupported,
        UnknownHdr
    }
}
