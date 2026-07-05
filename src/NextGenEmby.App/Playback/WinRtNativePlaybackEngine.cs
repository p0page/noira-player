using System;
using System.Threading.Tasks;
using NextGenEmby.Core.Playback;
using CoreNativePlaybackOpenRequest = NextGenEmby.Core.Playback.NativePlaybackOpenRequest;
using NativeHdrStatus = NextGenEmby.Native.NativeHdrStatus;
using NativePlaybackEngine = NextGenEmby.Native.NativePlaybackEngine;
using NativePlaybackOpenRequest = NextGenEmby.Native.NativePlaybackOpenRequest;
using NativePlaybackState = NextGenEmby.Native.NativePlaybackState;

namespace NextGenEmby.App.Playback
{
    public sealed class WinRtNativePlaybackEngine : INativePlaybackEngine
    {
        private readonly NativePlaybackEngine _engine;

        public WinRtNativePlaybackEngine(NativePlaybackEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _engine.StateChanged += Engine_OnStateChanged;
        }

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public long CurrentPositionTicks => _engine.CurrentPositionTicks();

        public PlaybackBackendCapabilities Capabilities { get; } =
            new PlaybackBackendCapabilities(
                PlaybackBackendFeature.DirectPlayHttp |
                PlaybackBackendFeature.Hevc |
                PlaybackBackendFeature.HevcMain10 |
                PlaybackBackendFeature.Hdr10 |
                PlaybackBackendFeature.AudioStreamSwitching |
                PlaybackBackendFeature.SubtitleStreamSwitching |
                PlaybackBackendFeature.MediaSourceSwitching);

        public PlaybackDisplayStatus DisplayStatus
        {
            get
            {
                var status = _engine.DisplayStatus();
                return new PlaybackDisplayStatus(
                    MapHdrStatus(status.HdrStatus),
                    status.IsHdrDisplayAvailable,
                    status.IsHdrOutputActive,
                    status.Message ?? "");
            }
        }

        public void AttachSurface(Windows.UI.Xaml.Controls.SwapChainPanel panel)
        {
            _engine.AttachSurface(panel);
        }

        public Task OpenAsync(CoreNativePlaybackOpenRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var nativeRequest = new NativePlaybackOpenRequest
            {
                ItemId = request.ItemId,
                MediaSourceId = request.MediaSourceId,
                DirectStreamUrl = request.DirectStreamUrl,
                StartPositionTicks = request.StartPositionTicks,
                HasAudioStreamIndex = request.AudioStreamIndex.HasValue,
                AudioStreamIndex = request.AudioStreamIndex.GetValueOrDefault(),
                HasSubtitleStreamIndex = request.SubtitleStreamIndex.HasValue,
                SubtitleStreamIndex = request.SubtitleStreamIndex.GetValueOrDefault()
            };

            return _engine.OpenAsync(nativeRequest).AsTask();
        }

        public Task PauseAsync()
        {
            return _engine.PauseAsync().AsTask();
        }

        public Task ResumeAsync()
        {
            return _engine.ResumeAsync().AsTask();
        }

        public Task SeekAsync(long positionTicks)
        {
            return _engine.SeekAsync(positionTicks).AsTask();
        }

        public Task StopAsync()
        {
            return _engine.StopAsync().AsTask();
        }

        public Task SwitchAudioStreamAsync(int audioStreamIndex)
        {
            return _engine.SwitchAudioStreamAsync(audioStreamIndex).AsTask();
        }

        public Task SwitchSubtitleStreamAsync(int? subtitleStreamIndex)
        {
            return subtitleStreamIndex.HasValue
                ? _engine.SwitchSubtitleStreamAsync(subtitleStreamIndex.Value).AsTask()
                : _engine.DisableSubtitlesAsync().AsTask();
        }

        private void Engine_OnStateChanged(NativePlaybackState state, string message)
        {
            StateChanged?.Invoke(
                this,
                new PlaybackStateChangedEventArgs(MapState(state), message ?? "", _engine.CurrentPositionTicks()));
        }

        private static PlaybackState MapState(NativePlaybackState state)
        {
            switch (state)
            {
                case NativePlaybackState.NativePlaybackState_Opening:
                    return PlaybackState.Opening;
                case NativePlaybackState.NativePlaybackState_Buffering:
                    return PlaybackState.Buffering;
                case NativePlaybackState.NativePlaybackState_Playing:
                    return PlaybackState.Playing;
                case NativePlaybackState.NativePlaybackState_Paused:
                    return PlaybackState.Paused;
                case NativePlaybackState.NativePlaybackState_Failed:
                    return PlaybackState.Failed;
                default:
                    return PlaybackState.Stopped;
            }
        }

        private static HdrOutputStatus MapHdrStatus(NativeHdrStatus status)
        {
            switch (status)
            {
                case NativeHdrStatus.NativeHdrStatus_Unsupported:
                    return HdrOutputStatus.Unsupported;
                case NativeHdrStatus.NativeHdrStatus_Off:
                    return HdrOutputStatus.Off;
                case NativeHdrStatus.NativeHdrStatus_On:
                    return HdrOutputStatus.On;
                case NativeHdrStatus.NativeHdrStatus_Failed:
                    return HdrOutputStatus.Failed;
                default:
                    return HdrOutputStatus.Unknown;
            }
        }
    }
}
