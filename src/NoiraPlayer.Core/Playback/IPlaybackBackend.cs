using System;
using System.Threading.Tasks;

namespace NoiraPlayer.Core.Playback
{
    public interface IPlaybackBackend
    {
        event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        long CurrentPositionTicks { get; }

        Task StartAsync(PlaybackDescriptor descriptor);

        Task PauseAsync();

        Task ResumeAsync();

        Task SeekAsync(long positionTicks);

        Task StopAsync();
    }
}
