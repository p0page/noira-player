namespace NoiraPlayer.Core.Playback
{
    public interface IPlaybackBackendDiagnostics
    {
        PlaybackBackendCapabilities Capabilities { get; }

        PlaybackDisplayStatus DisplayStatus { get; }
    }
}
