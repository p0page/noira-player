using System;
using System.Threading.Tasks;

namespace NextGenEmby.Core.Playback
{
    public interface INativePlaybackEngine : IPlaybackBackendDiagnostics
    {
        event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        long CurrentPositionTicks { get; }

        Task OpenAsync(NativePlaybackOpenRequest request);

        Task PauseAsync();

        Task ResumeAsync();

        Task SeekAsync(long positionTicks);

        Task StopAsync();

        Task SwitchAudioStreamAsync(int audioStreamIndex);

        Task SwitchSubtitleStreamAsync(int? subtitleStreamIndex);
    }
}
