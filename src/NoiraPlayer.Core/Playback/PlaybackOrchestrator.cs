using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NoiraPlayer.Core.Emby;

namespace NoiraPlayer.Core.Playback
{
    public sealed class PlaybackOrchestrator
    {
        private readonly IPlaybackBackend _backend;
        private IReadOnlyList<EmbyMediaSource> _availableSources = Array.Empty<EmbyMediaSource>();
        private string _currentItemId = "";
        private int? _audioStreamIndex;
        private int? _subtitleStreamIndex;
        private bool _backendStartInProgress;
        private bool _terminalStateDuringBackendStart;
        private bool _backendCommandInProgress;
        private bool _terminalStateDuringBackendCommand;

        public PlaybackOrchestrator(IPlaybackBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _backend.StateChanged += Backend_OnStateChanged;
        }

        public PlaybackState State { get; private set; } = PlaybackState.Stopped;

        public EmbyMediaSource? CurrentMediaSource { get; private set; }

        public PlaybackDescriptor? CurrentDescriptor { get; private set; }

        public long CurrentDurationTicks => PlaybackTimelineDurationPolicy.Resolve(
            _backend.DurationTicks,
            CurrentMediaSource?.RunTimeTicks ?? 0,
            0);

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public async Task StartAsync(
            string itemId,
            IReadOnlyList<EmbyMediaSource> sources,
            long resumeTicks,
            string preferredMediaSourceId = "")
        {
            if (itemId == null)
            {
                throw new ArgumentNullException(nameof(itemId));
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Playback requires an item id.", nameof(itemId));
            }

            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            if (sources.Count == 0)
            {
                throw new InvalidOperationException("Playback requires at least one media source.");
            }

            await StartBackendAsync(
                SelectInitialMediaSource(sources, preferredMediaSourceId),
                resumeTicks,
                itemId,
                sources,
                null,
                null,
                restoreOnFailure: false).ConfigureAwait(false);
        }

        private static EmbyMediaSource SelectInitialMediaSource(
            IReadOnlyList<EmbyMediaSource> sources,
            string preferredMediaSourceId)
        {
            if (!string.IsNullOrWhiteSpace(preferredMediaSourceId))
            {
                var preferredSource = sources.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, preferredMediaSourceId, StringComparison.Ordinal));
                if (preferredSource != null)
                {
                    return preferredSource;
                }
            }

            return sources[0];
        }

        public async Task SwitchMediaSourceAsync(string mediaSourceId)
        {
            if (mediaSourceId == null)
            {
                throw new ArgumentNullException(nameof(mediaSourceId));
            }

            EnsureStarted();

            var source = _availableSources.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, mediaSourceId, StringComparison.Ordinal));

            if (source == null)
            {
                throw new InvalidOperationException("The requested media source is not available.");
            }

            await StartBackendAsync(
                source,
                _backend.CurrentPositionTicks,
                _currentItemId,
                _availableSources,
                null,
                null,
                restoreOnFailure: true).ConfigureAwait(false);
        }

        public async Task SwitchAudioStreamAsync(int? audioStreamIndex)
        {
            EnsureStarted();
            ValidateStreamIndex(audioStreamIndex, EmbyStreamKind.Audio, nameof(audioStreamIndex));
            if (audioStreamIndex.HasValue && _backend is IPlaybackStreamSwitchingBackend streamSwitchingBackend)
            {
                await SwitchAudioStreamInPlaceAsync(streamSwitchingBackend, audioStreamIndex.Value).ConfigureAwait(false);
                return;
            }

            await StartBackendAsync(
                CurrentMediaSource!,
                _backend.CurrentPositionTicks,
                _currentItemId,
                _availableSources,
                audioStreamIndex,
                _subtitleStreamIndex,
                restoreOnFailure: true).ConfigureAwait(false);
        }

        public async Task SwitchSubtitleStreamAsync(int? subtitleStreamIndex)
        {
            EnsureStarted();
            ValidateStreamIndex(subtitleStreamIndex, EmbyStreamKind.Subtitle, nameof(subtitleStreamIndex));
            if (_backend is IPlaybackStreamSwitchingBackend streamSwitchingBackend)
            {
                await SwitchSubtitleStreamInPlaceAsync(streamSwitchingBackend, subtitleStreamIndex).ConfigureAwait(false);
                return;
            }

            await StartBackendAsync(
                CurrentMediaSource!,
                _backend.CurrentPositionTicks,
                _currentItemId,
                _availableSources,
                _audioStreamIndex,
                subtitleStreamIndex,
                restoreOnFailure: true).ConfigureAwait(false);
        }

        public async Task PauseAsync()
        {
            EnsureStarted();
            await _backend.PauseAsync().ConfigureAwait(false);
            SetState(PlaybackState.Paused);
        }

        public async Task ResumeAsync()
        {
            EnsureStarted();
            await _backend.ResumeAsync().ConfigureAwait(false);
            SetState(PlaybackState.Playing);
        }

        public Task SeekAsync(long positionTicks)
        {
            EnsureStarted();
            return _backend.SeekAsync(positionTicks);
        }

        public PlaybackProgressRequest CreateProgressRequest(PlaybackProgressEvent eventName)
        {
            var session = CreateSessionRequest();

            return new PlaybackProgressRequest
            {
                ItemId = session.ItemId,
                MediaSourceId = session.MediaSourceId,
                PlaySessionId = session.PlaySessionId,
                PositionTicks = session.PositionTicks,
                IsPaused = session.IsPaused,
                PlayMethod = session.PlayMethod,
                AudioStreamIndex = session.AudioStreamIndex,
                SubtitleStreamIndex = session.SubtitleStreamIndex,
                EventName = eventName,
            };
        }

        public PlaybackSessionRequest CreateSessionRequest()
        {
            EnsureStarted();
            var descriptor = CurrentDescriptor!;
            var source = descriptor.MediaSource;

            return new PlaybackSessionRequest
            {
                ItemId = descriptor.ItemId,
                MediaSourceId = source.Id,
                PlaySessionId = string.IsNullOrWhiteSpace(source.PlaySessionId) ? null : source.PlaySessionId,
                PositionTicks = Math.Max(0, _backend.CurrentPositionTicks),
                IsPaused = State == PlaybackState.Paused,
                PlayMethod = PlaybackPlayMethod.DirectPlay,
                AudioStreamIndex = descriptor.AudioStreamIndex,
                SubtitleStreamIndex = descriptor.SubtitleStreamIndex
            };
        }

        public async Task StopAsync()
        {
            await _backend.StopAsync().ConfigureAwait(false);
            SetState(PlaybackState.Stopped);
            ClearPlaybackContext();
        }

        private async Task SwitchAudioStreamInPlaceAsync(
            IPlaybackStreamSwitchingBackend streamSwitchingBackend,
            int audioStreamIndex)
        {
            _backendCommandInProgress = true;
            _terminalStateDuringBackendCommand = false;
            try
            {
                await streamSwitchingBackend.SwitchAudioStreamAsync(audioStreamIndex).ConfigureAwait(false);
            }
            finally
            {
                _backendCommandInProgress = false;
            }

            if (_terminalStateDuringBackendCommand)
            {
                _terminalStateDuringBackendCommand = false;
                return;
            }

            UpdateCurrentDescriptor(audioStreamIndex, _subtitleStreamIndex);
        }

        private async Task SwitchSubtitleStreamInPlaceAsync(
            IPlaybackStreamSwitchingBackend streamSwitchingBackend,
            int? subtitleStreamIndex)
        {
            _backendCommandInProgress = true;
            _terminalStateDuringBackendCommand = false;
            try
            {
                await streamSwitchingBackend.SwitchSubtitleStreamAsync(subtitleStreamIndex).ConfigureAwait(false);
            }
            finally
            {
                _backendCommandInProgress = false;
            }

            if (_terminalStateDuringBackendCommand)
            {
                _terminalStateDuringBackendCommand = false;
                return;
            }

            UpdateCurrentDescriptor(_audioStreamIndex, subtitleStreamIndex);
        }

        private async Task StartBackendAsync(
            EmbyMediaSource source,
            long startPositionTicks,
            string itemId,
            IReadOnlyList<EmbyMediaSource> availableSources,
            int? audioStreamIndex,
            int? subtitleStreamIndex,
            bool restoreOnFailure)
        {
            var descriptor = new PlaybackDescriptor(
                itemId,
                source,
                availableSources,
                startPositionTicks,
                audioStreamIndex,
                subtitleStreamIndex);
            var previousState = State;

            SetState(PlaybackState.Opening);

            try
            {
                _backendStartInProgress = true;
                _terminalStateDuringBackendStart = false;
                await _backend.StartAsync(descriptor).ConfigureAwait(false);
            }
            catch
            {
                if (_terminalStateDuringBackendStart)
                {
                    _terminalStateDuringBackendStart = false;
                    throw;
                }

                if (!restoreOnFailure)
                {
                    ClearPlaybackContext();
                }

                SetState(restoreOnFailure ? previousState : PlaybackState.Failed);
                throw;
            }
            finally
            {
                _backendStartInProgress = false;
            }

            if (_terminalStateDuringBackendStart)
            {
                _terminalStateDuringBackendStart = false;
                return;
            }

            _currentItemId = itemId;
            _availableSources = availableSources;
            _audioStreamIndex = audioStreamIndex;
            _subtitleStreamIndex = subtitleStreamIndex;
            CurrentMediaSource = source;
            CurrentDescriptor = descriptor;
            SetState(PlaybackState.Playing);
        }

        private void UpdateCurrentDescriptor(int? audioStreamIndex, int? subtitleStreamIndex)
        {
            _audioStreamIndex = audioStreamIndex;
            _subtitleStreamIndex = subtitleStreamIndex;
            CurrentDescriptor = new PlaybackDescriptor(
                _currentItemId,
                CurrentMediaSource!,
                _availableSources,
                CurrentDescriptor?.StartPositionTicks ?? 0,
                audioStreamIndex,
                subtitleStreamIndex);
        }

        private void EnsureStarted()
        {
            if (_availableSources.Count == 0 || string.IsNullOrEmpty(_currentItemId) || CurrentMediaSource == null)
            {
                throw new InvalidOperationException("Playback has not been started.");
            }
        }

        private void ValidateStreamIndex(int? streamIndex, EmbyStreamKind expectedKind, string parameterName)
        {
            if (!streamIndex.HasValue)
            {
                return;
            }

            if (streamIndex.Value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Stream index cannot be negative.");
            }

            var found = CurrentMediaSource!.Streams.Any(stream =>
                stream.Index == streamIndex.Value && stream.Kind == expectedKind);
            if (!found)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Stream index is not available on the current media source.");
            }
        }

        private void Backend_OnStateChanged(object? sender, PlaybackStateChangedEventArgs args)
        {
            if (args.State == PlaybackState.Failed || args.State == PlaybackState.Stopped)
            {
                if (_backendStartInProgress)
                {
                    _terminalStateDuringBackendStart = true;
                }

                if (_backendCommandInProgress)
                {
                    _terminalStateDuringBackendCommand = true;
                }

                ClearPlaybackContext();
            }

            SetState(args.State, args.Message, args.PositionTicks);
        }

        private void SetState(PlaybackState state, string message = "", long? positionTicks = null)
        {
            State = state;
            StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(state, message, positionTicks));
        }

        private void ClearPlaybackContext()
        {
            CurrentMediaSource = null;
            CurrentDescriptor = null;
            _currentItemId = "";
            _availableSources = Array.Empty<EmbyMediaSource>();
            _audioStreamIndex = null;
            _subtitleStreamIndex = null;
        }
    }
}
