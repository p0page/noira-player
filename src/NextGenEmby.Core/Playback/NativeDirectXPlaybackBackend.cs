using System;
using System.Threading.Tasks;

namespace NextGenEmby.Core.Playback
{
    public sealed class NativeDirectXPlaybackBackend :
        IPlaybackBackend,
        IPlaybackBackendDiagnostics,
        IPlaybackStreamSwitchingBackend
    {
        private readonly INativePlaybackEngine _engine;

        public NativeDirectXPlaybackBackend(INativePlaybackEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _engine.StateChanged += Engine_OnStateChanged;
        }

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public long CurrentPositionTicks => _engine.CurrentPositionTicks;

        public PlaybackBackendCapabilities Capabilities => _engine.Capabilities;

        public PlaybackDisplayStatus DisplayStatus => _engine.DisplayStatus;

        public Task StartAsync(PlaybackDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var source = descriptor.MediaSource;
            var request = new NativePlaybackOpenRequest(
                descriptor.ItemId,
                source.Id,
                source.DirectStreamUrl,
                descriptor.StartPositionTicks,
                descriptor.AudioStreamIndex,
                descriptor.SubtitleStreamIndex,
                source.VideoFrameRate);

            return _engine.OpenAsync(request);
        }

        public Task PauseAsync()
        {
            return _engine.PauseAsync();
        }

        public Task ResumeAsync()
        {
            return _engine.ResumeAsync();
        }

        public Task SeekAsync(long positionTicks)
        {
            return _engine.SeekAsync(positionTicks);
        }

        public Task StopAsync()
        {
            return _engine.StopAsync();
        }

        public Task SwitchAudioStreamAsync(int audioStreamIndex)
        {
            return _engine.SwitchAudioStreamAsync(audioStreamIndex);
        }

        public Task SwitchSubtitleStreamAsync(int? subtitleStreamIndex)
        {
            return _engine.SwitchSubtitleStreamAsync(subtitleStreamIndex);
        }

        private void Engine_OnStateChanged(object? sender, PlaybackStateChangedEventArgs args)
        {
            StateChanged?.Invoke(this, args);
        }
    }
}
