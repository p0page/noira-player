using System;
using System.Threading.Tasks;
using NoiraPlayer.Core.Playback;
using NoiraPlayer.Core.PlaybackQuality;
using NoiraPlayer.App.Services;
using CoreNativePlaybackOpenRequest = NoiraPlayer.Core.Playback.NativePlaybackOpenRequest;
using NativeHdrStatus = NoiraPlayer.Native.NativeHdrStatus;
using NativePlaybackEngine = NoiraPlayer.Native.NativePlaybackEngine;
using NativePlaybackOpenRequest = NoiraPlayer.Native.NativePlaybackOpenRequest;
using NativePlaybackState = NoiraPlayer.Native.NativePlaybackState;

namespace NoiraPlayer.App.Playback
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
                    status.VideoProcessorConversionStatus ?? "",
                    status.RefreshRateHz);
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
                    HardwareDecodedVideoFrames = nativeMetrics.HardwareDecodedVideoFrames,
                    SoftwareDecodedVideoFrames = nativeMetrics.SoftwareDecodedVideoFrames,
                    RenderedVideoFrames = nativeMetrics.RenderedVideoFrames,
                    SubmittedAudioFrames = nativeMetrics.SubmittedAudioFrames,
                    SelectedAudioStreamIndex = nativeMetrics.SelectedAudioStreamIndex,
                    SubtitleDecodedCueCount = nativeMetrics.SubtitleDecodedCueCount,
                    SubtitleCueRenderCount = nativeMetrics.SubtitleCueRenderCount,
                    SelectedSubtitleStreamIndex = nativeMetrics.SelectedSubtitleStreamIndex,
                    DroppedVideoFrames = nativeMetrics.DroppedVideoFrames,
                    SeekPrerollDroppedFrames = nativeMetrics.SeekPrerollDroppedFrames,
                    VideoAheadWaitCount = nativeMetrics.VideoAheadWaitCount,
                    AudioAheadWaitCount = nativeMetrics.AudioAheadWaitCount,
                    VideoClockWaitCount = nativeMetrics.VideoClockWaitCount,
                    VideoStarvedPasses = nativeMetrics.VideoStarvedPasses,
                    AudioStarvedPasses = nativeMetrics.AudioStarvedPasses,
                    QueuedAudioBuffers = nativeMetrics.QueuedAudioBuffers,
                    AudioClockTicks = nativeMetrics.AudioClockTicks,
                    VideoPositionTicks = nativeMetrics.VideoPositionTicks,
                    NativeGraphOpenDurationMs = nativeMetrics.NativeGraphOpenDurationMs,
                    FfmpegOpenInputDurationMs = nativeMetrics.FfmpegOpenInputDurationMs,
                    FfmpegStreamInfoDurationMs = nativeMetrics.FfmpegStreamInfoDurationMs,
                    NativeStartupSeekDurationMs = nativeMetrics.NativeStartupSeekDurationMs,
                    FfmpegOpenInputBytesRead = nativeMetrics.FfmpegOpenInputBytesRead,
                    FfmpegStreamInfoBytesRead = nativeMetrics.FfmpegStreamInfoBytesRead,
                    NativeStartupSeekBytesRead = nativeMetrics.NativeStartupSeekBytesRead,
                    NativeFirstFrameTransportBytesRead = nativeMetrics.NativeFirstFrameTransportBytesRead,
                    StartupTransportProvider = nativeMetrics.StartupTransportProvider,
                    StartupTransportCallEvidenceAvailable = nativeMetrics.StartupTransportCallEvidenceAvailable,
                    FfmpegOpenInputTransportCalls = new PlaybackQualityTransportCallSnapshot
                    {
                        Provider = nativeMetrics.FfmpegOpenInputTransportProvider,
                        EvidenceAvailable = nativeMetrics.FfmpegOpenInputTransportCallEvidenceAvailable,
                        ReadCalls = nativeMetrics.FfmpegOpenInputTransportReadCalls,
                        SeekCalls = nativeMetrics.FfmpegOpenInputTransportSeekCalls,
                        ReadWaitMs = nativeMetrics.FfmpegOpenInputTransportReadWaitMs,
                        SeekWaitMs = nativeMetrics.FfmpegOpenInputTransportSeekWaitMs,
                        SeekDistanceBytes = nativeMetrics.FfmpegOpenInputTransportSeekDistanceBytes
                    },
                    FfmpegStreamInfoTransportCalls = new PlaybackQualityTransportCallSnapshot
                    {
                        Provider = nativeMetrics.FfmpegStreamInfoTransportProvider,
                        EvidenceAvailable = nativeMetrics.FfmpegStreamInfoTransportCallEvidenceAvailable,
                        ReadCalls = nativeMetrics.FfmpegStreamInfoTransportReadCalls,
                        SeekCalls = nativeMetrics.FfmpegStreamInfoTransportSeekCalls,
                        ReadWaitMs = nativeMetrics.FfmpegStreamInfoTransportReadWaitMs,
                        SeekWaitMs = nativeMetrics.FfmpegStreamInfoTransportSeekWaitMs,
                        SeekDistanceBytes = nativeMetrics.FfmpegStreamInfoTransportSeekDistanceBytes
                    },
                    NativeStartupSeekTransportCalls = new PlaybackQualityTransportCallSnapshot
                    {
                        Provider = nativeMetrics.NativeStartupSeekTransportProvider,
                        EvidenceAvailable = nativeMetrics.NativeStartupSeekTransportCallEvidenceAvailable,
                        ReadCalls = nativeMetrics.NativeStartupSeekTransportReadCalls,
                        SeekCalls = nativeMetrics.NativeStartupSeekTransportSeekCalls,
                        ReadWaitMs = nativeMetrics.NativeStartupSeekTransportReadWaitMs,
                        SeekWaitMs = nativeMetrics.NativeStartupSeekTransportSeekWaitMs,
                        SeekDistanceBytes = nativeMetrics.NativeStartupSeekTransportSeekDistanceBytes
                    },
                    NativeFirstFrameTransportCalls = new PlaybackQualityTransportCallSnapshot
                    {
                        Provider = nativeMetrics.NativeFirstFrameTransportProvider,
                        EvidenceAvailable = nativeMetrics.NativeFirstFrameTransportCallEvidenceAvailable,
                        ReadCalls = nativeMetrics.NativeFirstFrameTransportReadCalls,
                        SeekCalls = nativeMetrics.NativeFirstFrameTransportSeekCalls,
                        ReadWaitMs = nativeMetrics.NativeFirstFrameTransportReadWaitMs,
                        SeekWaitMs = nativeMetrics.NativeFirstFrameTransportSeekWaitMs,
                        SeekDistanceBytes = nativeMetrics.NativeFirstFrameTransportSeekDistanceBytes
                    },
                    NativeFirstFrameDurationMs = nativeMetrics.NativeFirstFrameDurationMs,
                    NativeFirstFrameDemuxReadDurationMs = nativeMetrics.NativeFirstFrameDemuxReadDurationMs,
                    NativeFirstFramePresentDurationMs = nativeMetrics.NativeFirstFramePresentDurationMs,
                    NativeFirstFrameDemuxPacketCount = nativeMetrics.NativeFirstFrameDemuxPacketCount,
                    NativeFirstFrameDemuxBytes = nativeMetrics.NativeFirstFrameDemuxBytes,
                    ReadErrorCount = nativeMetrics.ReadErrorCount,
                    ReadRetryCount = nativeMetrics.ReadRetryCount,
                    ReadRecoveryCount = nativeMetrics.ReadRecoveryCount,
                    MaxConsecutiveReadErrors = nativeMetrics.MaxConsecutiveReadErrors,
                    LastReadErrorCode = nativeMetrics.LastReadErrorCode,
                    FatalReadErrorCode = nativeMetrics.FatalReadErrorCode,
                    LastReadRecoveryDurationMs = nativeMetrics.LastReadRecoveryDurationMs,
                    ContainerStartTimeTicks = nativeMetrics.ContainerStartTimeTicks,
                    VideoStreamStartTimeTicks = nativeMetrics.VideoStreamStartTimeTicks,
                    SeekDemuxTargetTicks = nativeMetrics.SeekDemuxTargetTicks,
                    FirstPresentedPositionTicks = nativeMetrics.FirstPresentedPositionTicks >= 0 ? nativeMetrics.FirstPresentedPositionTicks : (long?)null,
                    SeekPacketCacheEnabled = nativeMetrics.SeekPacketCacheEnabled,
                    SeekPacketCacheHit = nativeMetrics.SeekPacketCacheHit,
                    SeekPacketCachePacketCount = nativeMetrics.SeekPacketCachePacketCount,
                    SeekPacketCacheBytes = nativeMetrics.SeekPacketCacheBytes,
                    SeekPacketCacheWindowDurationTicks = nativeMetrics.SeekPacketCacheWindowDurationTicks,
                    SeekFallbackReason = nativeMetrics.SeekFallbackReason ?? "",
                    RenderIntervalMsP05 = nativeMetrics.RenderIntervalMsP05,
                    RenderIntervalMsP50 = nativeMetrics.RenderIntervalMsP50,
                    RenderIntervalMsP95 = nativeMetrics.RenderIntervalMsP95,
                    RenderIntervalMsP99 = nativeMetrics.RenderIntervalMsP99,
                    MinFrameGapMs = nativeMetrics.MinFrameGapMs,
                    MaxFrameGapMs = nativeMetrics.MaxFrameGapMs,
                    RenderIntervalSampleCount = nativeMetrics.RenderIntervalSampleCount,
                    RenderIntervalOverExpected2MsCount = nativeMetrics.RenderIntervalOverExpected2MsCount,
                    RenderIntervalOverExpected4MsCount = nativeMetrics.RenderIntervalOverExpected4MsCount,
                    RenderIntervalUnderExpected2MsCount = nativeMetrics.RenderIntervalUnderExpected2MsCount,
                    RenderIntervalUnderExpected4MsCount = nativeMetrics.RenderIntervalUnderExpected4MsCount,
                    RenderIntervalAfterAudioAheadWaitSampleCount = nativeMetrics.RenderIntervalAfterAudioAheadWaitSampleCount,
                    RenderIntervalAfterAudioAheadWaitMsP95 = nativeMetrics.RenderIntervalAfterAudioAheadWaitMsP95,
                    RenderIntervalAfterAudioAheadWaitMsP99 = nativeMetrics.RenderIntervalAfterAudioAheadWaitMsP99,
                    RenderIntervalAfterAudioAheadWaitMsMax = nativeMetrics.RenderIntervalAfterAudioAheadWaitMsMax,
                    AudioAheadWaitEndToPresentSampleCount = nativeMetrics.AudioAheadWaitEndToPresentSampleCount,
                    AudioAheadWaitEndToPresentMsP50 = nativeMetrics.AudioAheadWaitEndToPresentMsP50,
                    AudioAheadWaitEndToPresentMsP95 = nativeMetrics.AudioAheadWaitEndToPresentMsP95,
                    AudioAheadWaitEndToPresentMsP99 = nativeMetrics.AudioAheadWaitEndToPresentMsP99,
                    AudioAheadWaitEndToPresentMsMax = nativeMetrics.AudioAheadWaitEndToPresentMsMax,
                    RenderIntervalAfterNonAudioWaitSampleCount = nativeMetrics.RenderIntervalAfterNonAudioWaitSampleCount,
                    RenderIntervalAfterNonAudioWaitMsP95 = nativeMetrics.RenderIntervalAfterNonAudioWaitMsP95,
                    RenderIntervalAfterNonAudioWaitMsP99 = nativeMetrics.RenderIntervalAfterNonAudioWaitMsP99,
                    RenderIntervalAfterNonAudioWaitMsMax = nativeMetrics.RenderIntervalAfterNonAudioWaitMsMax,
                    PresentDurationMsP50 = nativeMetrics.PresentDurationMsP50,
                    PresentDurationMsP95 = nativeMetrics.PresentDurationMsP95,
                    PresentDurationMsP99 = nativeMetrics.PresentDurationMsP99,
                    PresentDurationMsMax = nativeMetrics.PresentDurationMsMax,
                    AudioAheadWaitDurationMsP50 = nativeMetrics.AudioAheadWaitDurationMsP50,
                    AudioAheadWaitDurationMsP95 = nativeMetrics.AudioAheadWaitDurationMsP95,
                    AudioAheadWaitDurationMsP99 = nativeMetrics.AudioAheadWaitDurationMsP99,
                    AudioAheadWaitDurationMsMax = nativeMetrics.AudioAheadWaitDurationMsMax,
                    AudioAheadWaitTargetMsP50 = nativeMetrics.AudioAheadWaitTargetMsP50,
                    AudioAheadWaitTargetMsP95 = nativeMetrics.AudioAheadWaitTargetMsP95,
                    AudioAheadWaitTargetMsP99 = nativeMetrics.AudioAheadWaitTargetMsP99,
                    AudioAheadWaitTargetMsMax = nativeMetrics.AudioAheadWaitTargetMsMax,
                    AudioAheadWaitOversleepMsP50 = nativeMetrics.AudioAheadWaitOversleepMsP50,
                    AudioAheadWaitOversleepMsP95 = nativeMetrics.AudioAheadWaitOversleepMsP95,
                    AudioAheadWaitOversleepMsP99 = nativeMetrics.AudioAheadWaitOversleepMsP99,
                    AudioAheadWaitOversleepMsMax = nativeMetrics.AudioAheadWaitOversleepMsMax,
                    AudioAheadWaitFinalDeltaAbsMsP50 = nativeMetrics.AudioAheadWaitFinalDeltaAbsMsP50,
                    AudioAheadWaitFinalDeltaAbsMsP95 = nativeMetrics.AudioAheadWaitFinalDeltaAbsMsP95,
                    AudioAheadWaitFinalDeltaAbsMsP99 = nativeMetrics.AudioAheadWaitFinalDeltaAbsMsP99,
                    AudioAheadWaitFinalDeltaAbsMsMax = nativeMetrics.AudioAheadWaitFinalDeltaAbsMsMax,
                    AudioAheadWaitEpisodeCount = nativeMetrics.AudioAheadWaitEpisodeCount,
                    AudioAheadWaitPassesPerEpisodeP50 = nativeMetrics.AudioAheadWaitPassesPerEpisodeP50,
                    AudioAheadWaitPassesPerEpisodeP95 = nativeMetrics.AudioAheadWaitPassesPerEpisodeP95,
                    AudioAheadWaitPassesPerEpisodeP99 = nativeMetrics.AudioAheadWaitPassesPerEpisodeP99,
                    AudioAheadWaitPassesPerEpisodeMax = nativeMetrics.AudioAheadWaitPassesPerEpisodeMax,
                    AudioAheadWaitPassDurationMsP50 = nativeMetrics.AudioAheadWaitPassDurationMsP50,
                    AudioAheadWaitPassDurationMsP95 = nativeMetrics.AudioAheadWaitPassDurationMsP95,
                    AudioAheadWaitPassDurationMsP99 = nativeMetrics.AudioAheadWaitPassDurationMsP99,
                    AudioAheadWaitPassDurationMsMax = nativeMetrics.AudioAheadWaitPassDurationMsMax,
                    AudioAheadWaitPassTargetMsP50 = nativeMetrics.AudioAheadWaitPassTargetMsP50,
                    AudioAheadWaitPassTargetMsP95 = nativeMetrics.AudioAheadWaitPassTargetMsP95,
                    AudioAheadWaitPassTargetMsP99 = nativeMetrics.AudioAheadWaitPassTargetMsP99,
                    AudioAheadWaitPassTargetMsMax = nativeMetrics.AudioAheadWaitPassTargetMsMax,
                    AudioAheadWaitPassOversleepMsP50 = nativeMetrics.AudioAheadWaitPassOversleepMsP50,
                    AudioAheadWaitPassOversleepMsP95 = nativeMetrics.AudioAheadWaitPassOversleepMsP95,
                    AudioAheadWaitPassOversleepMsP99 = nativeMetrics.AudioAheadWaitPassOversleepMsP99,
                    AudioAheadWaitPassOversleepMsMax = nativeMetrics.AudioAheadWaitPassOversleepMsMax,
                    FramePacingSourceFrameRate = nativeMetrics.FramePacingSourceFrameRate,
                    LateFrameDropToleranceMs = nativeMetrics.LateFrameDropToleranceMs,
                    AudioVideoDriftMsP50 = nativeMetrics.AudioVideoDriftMsP50,
                    AudioVideoDriftMsP95 = nativeMetrics.AudioVideoDriftMsP95,
                    AudioVideoDriftMsP99 = nativeMetrics.AudioVideoDriftMsP99,
                    AudioVideoDriftMsMax = nativeMetrics.AudioVideoDriftMsMax,
                    LastInteractionScenario = nativeMetrics.LastInteractionScenario ?? "",
                    LastInteractionSequence = nativeMetrics.LastInteractionSequence,
                    LastInteractionLockWaitDurationMs = nativeMetrics.LastInteractionLockWaitDurationMs,
                    LastInteractionExecutionDurationMs = nativeMetrics.LastInteractionExecutionDurationMs,
                    LastInteractionQuiesceDurationMs = nativeMetrics.LastInteractionQuiesceDurationMs,
                    LastInteractionSeekDurationMs = nativeMetrics.LastInteractionSeekDurationMs,
                    LastInteractionDecoderOpenDurationMs = nativeMetrics.LastInteractionDecoderOpenDurationMs,
                    LastInteractionRendererOpenDurationMs = nativeMetrics.LastInteractionRendererOpenDurationMs,
                    LastInteractionPacketCacheHit = nativeMetrics.LastInteractionPacketCacheHit,
                    LastInteractionPacketCacheEnabled = nativeMetrics.LastInteractionPacketCacheEnabled,
                    LastInteractionPacketCachePacketCount = nativeMetrics.LastInteractionPacketCachePacketCount,
                    LastInteractionPacketCacheBytes = nativeMetrics.LastInteractionPacketCacheBytes,
                    LastInteractionPacketCacheWindowDurationTicks = nativeMetrics.LastInteractionPacketCacheWindowDurationTicks
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
                " refresh=" + status.RefreshRateHz +
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
