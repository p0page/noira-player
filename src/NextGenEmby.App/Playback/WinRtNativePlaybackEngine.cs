using System;
using System.Threading.Tasks;
using NextGenEmby.Core.Playback;
using NextGenEmby.Core.PlaybackQuality;
using NextGenEmby.App.Services;
using CoreNativePlaybackOpenRequest = NextGenEmby.Core.Playback.NativePlaybackOpenRequest;
using NativeHdrStatus = NextGenEmby.Native.NativeHdrStatus;
using NativePlaybackEngine = NextGenEmby.Native.NativePlaybackEngine;
using NativePlaybackOpenRequest = NextGenEmby.Native.NativePlaybackOpenRequest;
using NativePlaybackState = NextGenEmby.Native.NativePlaybackState;

namespace NextGenEmby.App.Playback
{
    public sealed class WinRtNativePlaybackEngine :
        INativePlaybackEngine,
        IPlaybackQualityMetricsProvider,
        IPlaybackQualityMetricsProviderIdentity
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
                PlaybackBackendFeature.MediaSourceSwitching |
                PlaybackBackendFeature.NativeAudioOutput);

        public PlaybackDisplayStatus DisplayStatus
        {
            get
            {
                var status = _engine.DisplayStatus();
                return new PlaybackDisplayStatus(
                    MapHdrStatus(status.HdrStatus),
                    status.IsHdrDisplayAvailable,
                    status.IsHdrOutputActive,
                    status.Message ?? "",
                    status.SwapChainFormat ?? "",
                    status.SwapChainColorSpace ?? "",
                    status.IsTenBitSwapChain,
                    status.IsVideoProcessorColorSpaceValidated,
                    status.VideoProcessorInputColorSpace ?? "",
                    status.VideoProcessorOutputColorSpace ?? "",
                    status.VideoProcessorConversionStatus ?? "");
            }
        }

        public string PlaybackQualityMetricsProviderId => "native-winrt";

        public bool TryGetQualityMetrics(out PlaybackQualityMetricsSnapshot metrics)
        {
            try
            {
                var nativeMetrics = _engine.QualityMetrics();
                if (nativeMetrics == null)
                {
                    metrics = new PlaybackQualityMetricsSnapshot();
                    return false;
                }

                metrics = new PlaybackQualityMetricsSnapshot
                {
                    RenderPasses = nativeMetrics.RenderPasses,
                    DecodedVideoFrames = nativeMetrics.DecodedVideoFrames,
                    RenderedVideoFrames = nativeMetrics.RenderedVideoFrames,
                    SubmittedAudioFrames = nativeMetrics.SubmittedAudioFrames,
                    DroppedVideoFrames = nativeMetrics.DroppedVideoFrames,
                    SeekPrerollDroppedFrames = nativeMetrics.SeekPrerollDroppedFrames,
                    VideoAheadWaitCount = nativeMetrics.VideoAheadWaitCount,
                    VideoStarvedPasses = nativeMetrics.VideoStarvedPasses,
                    AudioStarvedPasses = nativeMetrics.AudioStarvedPasses,
                    QueuedAudioBuffers = nativeMetrics.QueuedAudioBuffers,
                    AudioClockTicks = nativeMetrics.AudioClockTicks,
                    VideoPositionTicks = nativeMetrics.VideoPositionTicks,
                    RenderIntervalMsP50 = nativeMetrics.RenderIntervalMsP50,
                    RenderIntervalMsP95 = nativeMetrics.RenderIntervalMsP95,
                    RenderIntervalMsP99 = nativeMetrics.RenderIntervalMsP99,
                    MaxFrameGapMs = nativeMetrics.MaxFrameGapMs,
                    FramePacingSourceFrameRate = nativeMetrics.FramePacingSourceFrameRate,
                    LateFrameDropToleranceMs = nativeMetrics.LateFrameDropToleranceMs,
                    AudioVideoDriftMsP50 = nativeMetrics.AudioVideoDriftMsP50,
                    AudioVideoDriftMsP95 = nativeMetrics.AudioVideoDriftMsP95,
                    AudioVideoDriftMsP99 = nativeMetrics.AudioVideoDriftMsP99,
                    AudioVideoDriftMsMax = nativeMetrics.AudioVideoDriftMsMax
                };
                return true;
            }
            catch (Exception ex)
            {
                PlaybackDiagnosticsLog.WriteLine(
                    "Native quality metrics unavailable " + ex.GetType().FullName + " " + ex.Message);
                metrics = new PlaybackQualityMetricsSnapshot();
                return false;
            }
        }

        public void AttachSurface(Windows.UI.Xaml.Controls.SwapChainPanel panel)
        {
            PlaybackDiagnosticsLog.WriteLine("Native attach surface begin");
            _engine.AttachSurface(panel);
            PlaybackDiagnosticsLog.WriteLine("Native attach surface end " + FormatDisplayStatus(DisplayStatus));
        }

        public async Task OpenAsync(CoreNativePlaybackOpenRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            await PlaybackDiagnosticsLog.WriteLineAsync(
                "Native open enter item=" + request.ItemId +
                " source=" + request.MediaSourceId +
                " fps=" + request.VideoFrameRate +
                " urlLength=" + (request.DirectStreamUrl?.Length ?? 0));

            await PlaybackDiagnosticsLog.WriteLineAsync("Native request create begin");
            var nativeRequest = new NativePlaybackOpenRequest();
            await PlaybackDiagnosticsLog.WriteLineAsync("Native request create end");

            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set ItemId begin");
            nativeRequest.ItemId = request.ItemId;
            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set ItemId end");

            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set MediaSourceId begin");
            nativeRequest.MediaSourceId = request.MediaSourceId;
            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set MediaSourceId end");

            await PlaybackDiagnosticsLog.WriteLineAsync(
                "Native request set DirectStreamUrl begin length=" + (request.DirectStreamUrl?.Length ?? 0));
            nativeRequest.DirectStreamUrl = request.DirectStreamUrl;
            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set DirectStreamUrl end");

            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set StartPositionTicks begin");
            nativeRequest.StartPositionTicks = request.StartPositionTicks;
            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set StartPositionTicks end");

            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set audio fields begin");
            nativeRequest.HasAudioStreamIndex = request.AudioStreamIndex.HasValue;
            nativeRequest.AudioStreamIndex = request.AudioStreamIndex.GetValueOrDefault();
            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set audio fields end");

            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set subtitle fields begin");
            nativeRequest.HasSubtitleStreamIndex = request.SubtitleStreamIndex.HasValue;
            nativeRequest.SubtitleStreamIndex = request.SubtitleStreamIndex.GetValueOrDefault();
            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set subtitle fields end");

            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set VideoFrameRate begin");
            nativeRequest.VideoFrameRate = request.VideoFrameRate;
            await PlaybackDiagnosticsLog.WriteLineAsync("Native request set VideoFrameRate end");

            await PlaybackDiagnosticsLog.WriteLineAsync(
                "Native open begin item=" + request.ItemId +
                " source=" + request.MediaSourceId +
                " startTicks=" + request.StartPositionTicks +
                " fps=" + request.VideoFrameRate +
                " audio=" + (request.AudioStreamIndex.HasValue ? request.AudioStreamIndex.Value.ToString() : "default") +
                " subtitle=" + (request.SubtitleStreamIndex.HasValue ? request.SubtitleStreamIndex.Value.ToString() : "off"));
            await PlaybackDiagnosticsLog.WriteLineAsync("Native open status before " + FormatDisplayStatus(DisplayStatus));
            try
            {
                await _engine.OpenAsync(nativeRequest).AsTask();
                PlaybackDiagnosticsLog.WriteLine("Native open end " + FormatDisplayStatus(DisplayStatus));
            }
            catch (Exception ex)
            {
                PlaybackDiagnosticsLog.WriteLine("Native open exception " + ex.GetType().FullName + " " + ex.Message);
                throw;
            }
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
            PlaybackDiagnosticsLog.WriteLine(
                "Native state " + MapState(state) +
                " position=" + _engine.CurrentPositionTicks() +
                " message=" + (message ?? "") +
                " " + FormatDisplayStatus(DisplayStatus));
            StateChanged?.Invoke(
                this,
                new PlaybackStateChangedEventArgs(MapState(state), message ?? "", _engine.CurrentPositionTicks()));
        }

        private static string FormatDisplayStatus(PlaybackDisplayStatus status)
        {
            return
                "hdr=" + status.HdrStatus +
                " active=" + status.IsHdrOutputActive +
                " display=" + status.IsHdrDisplayAvailable +
                " swap=" + status.SwapChainFormat +
                " color=" + status.SwapChainColorSpace +
                " tenBit=" + status.IsTenBitSwapChain +
                " vp=" + status.IsVideoProcessorColorSpaceValidated +
                " vpIn=" + status.VideoProcessorInputColorSpace +
                " vpOut=" + status.VideoProcessorOutputColorSpace +
                " vpStatus=" + status.VideoProcessorConversionStatus +
                " msg=" + status.Message;
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
