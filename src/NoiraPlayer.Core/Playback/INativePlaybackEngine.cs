using System;
using System.Threading.Tasks;

namespace NoiraPlayer.Core.Playback
{
    public interface INativePlaybackEngine : IPlaybackBackendDiagnostics
    {
        event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        long CurrentPositionTicks { get; }

        long DurationTicks { get; }

        Task OpenAsync(NativePlaybackOpenRequest request);

        Task PauseAsync();

        Task ResumeAsync();

        Task SeekAsync(long positionTicks);

        Task StopAsync();

        Task SwitchAudioStreamAsync(int audioStreamIndex);

        Task SwitchSubtitleStreamAsync(int? subtitleStreamIndex);
    }
}
