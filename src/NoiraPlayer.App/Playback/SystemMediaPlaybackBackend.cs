using System;
using System.Threading.Tasks;
using NoiraPlayer.Core.Playback;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;
using CorePlaybackState = NoiraPlayer.Core.Playback.PlaybackState;

namespace NoiraPlayer.App.Playback
{
    public sealed class SystemMediaPlaybackBackend : IPlaybackBackend, IDisposable
    {
        private readonly MediaPlayerElement _element;
        private readonly MediaPlayer _player;
        private TimeSpan? _pendingStartPosition;
        private bool _disposed;

        public SystemMediaPlaybackBackend(MediaPlayerElement element)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _player = new MediaPlayer
            {
                AutoPlay = false
            };

            _player.MediaOpened += Player_OnMediaOpened;
            _player.MediaEnded += Player_OnMediaEnded;
            _player.MediaFailed += Player_OnMediaFailed;
            _player.PlaybackSession.PlaybackStateChanged += PlaybackSession_OnPlaybackStateChanged;

            _element.SetMediaPlayer(_player);
            _element.AreTransportControlsEnabled = false;
        }

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public long CurrentPositionTicks => _player.PlaybackSession.Position.Ticks;

        public long DurationTicks => _player.PlaybackSession.NaturalDuration.Ticks;

        public Task StartAsync(PlaybackDescriptor descriptor)
        {
            ThrowIfDisposed();

            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (descriptor.StartPositionTicks < 0)
            {
                RaiseState(CorePlaybackState.Failed, "Start position cannot be negative.");
                throw new ArgumentOutOfRangeException(nameof(descriptor), "Start position cannot be negative.");
            }

            var streamUri = CreateValidatedDirectStreamUri(descriptor.MediaSource.DirectStreamUrl);
            _pendingStartPosition = descriptor.StartPositionTicks > 0
                ? TimeSpan.FromTicks(descriptor.StartPositionTicks)
                : (TimeSpan?)null;

            RaiseState(CorePlaybackState.Opening);
            _player.Source = MediaSource.CreateFromUri(streamUri);
            _player.Play();

            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            ThrowIfDisposed();
            _player.Pause();
            RaiseState(CorePlaybackState.Paused);
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            ThrowIfDisposed();
            _player.Play();
            RaiseState(CorePlaybackState.Playing);
            return Task.CompletedTask;
        }

        public Task SeekAsync(long positionTicks)
        {
            ThrowIfDisposed();
            if (positionTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(positionTicks), "Seek position cannot be negative.");
            }

            _player.PlaybackSession.Position = TimeSpan.FromTicks(positionTicks);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            ThrowIfDisposed();
            _pendingStartPosition = null;
            _player.Pause();
            _player.Source = null;
            RaiseState(CorePlaybackState.Stopped);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _pendingStartPosition = null;
            _player.MediaOpened -= Player_OnMediaOpened;
            _player.MediaEnded -= Player_OnMediaEnded;
            _player.MediaFailed -= Player_OnMediaFailed;
            _player.PlaybackSession.PlaybackStateChanged -= PlaybackSession_OnPlaybackStateChanged;
            _player.Pause();
            _player.Source = null;
            _element.SetMediaPlayer(null);
            _player.Dispose();
            _disposed = true;
        }

        private static CorePlaybackState? MapPlaybackState(MediaPlaybackState state)
        {
            switch (state)
            {
                case MediaPlaybackState.Opening:
                    return CorePlaybackState.Opening;
                case MediaPlaybackState.Buffering:
                    return CorePlaybackState.Buffering;
                case MediaPlaybackState.Playing:
                    return CorePlaybackState.Playing;
                case MediaPlaybackState.Paused:
                    return CorePlaybackState.Paused;
                default:
                    return null;
            }
        }

        private Uri CreateValidatedDirectStreamUri(string directStreamUrl)
        {
            if (string.IsNullOrWhiteSpace(directStreamUrl))
            {
                RaiseState(CorePlaybackState.Failed, "Playback requires a direct stream URL.");
                throw new InvalidOperationException("Playback requires a direct stream URL.");
            }

            var trimmed = directStreamUrl.Trim();
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var streamUri) ||
                (streamUri.Scheme != Uri.UriSchemeHttp && streamUri.Scheme != Uri.UriSchemeHttps))
            {
                RaiseState(CorePlaybackState.Failed, "Direct stream URL must be an absolute HTTP or HTTPS URL.");
                throw new InvalidOperationException("Direct stream URL must be an absolute HTTP or HTTPS URL.");
            }

            return streamUri;
        }

        private void PlaybackSession_OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            var mappedState = MapPlaybackState(sender.PlaybackState);
            if (mappedState.HasValue)
            {
                RaiseState(mappedState.Value);
            }
        }

        private void Player_OnMediaOpened(MediaPlayer sender, object args)
        {
            if (_pendingStartPosition.HasValue)
            {
                sender.PlaybackSession.Position = _pendingStartPosition.Value;
                _pendingStartPosition = null;
            }
        }

        private void Player_OnMediaEnded(MediaPlayer sender, object args)
        {
            _pendingStartPosition = null;
            RaiseState(CorePlaybackState.Stopped, "Playback ended.");
        }

        private void Player_OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            _pendingStartPosition = null;
            RaiseState(CorePlaybackState.Failed, args.ErrorMessage ?? "Media playback failed.");
        }

        private void RaiseState(CorePlaybackState state, string message = "")
        {
            StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(state, message, CurrentPositionTicks));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SystemMediaPlaybackBackend));
            }
        }
    }
}
