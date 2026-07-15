#include "pch.h"
#include "NativePlaybackEngine.h"
#include "NativePlaybackDiagnostics.h"
#include "NativePlaybackQualityMetrics.h"
#include "NativePlaybackStatus.h"
#include "NativePlaybackEngine.g.cpp"

#include <exception>
#include <optional>
#include <string>

namespace winrt::NoiraPlayer::Native::implementation
{
    namespace
    {
        winrt::hstring FormatDxgiFormat(DXGI_FORMAT format)
        {
            switch (format)
            {
            case DXGI_FORMAT_R10G10B10A2_UNORM:
                return L"R10G10B10A2_UNORM";
            case DXGI_FORMAT_B8G8R8A8_UNORM:
                return L"B8G8R8A8_UNORM";
            case DXGI_FORMAT_UNKNOWN:
                return L"UNKNOWN";
            default:
                return winrt::to_hstring(static_cast<int32_t>(format));
            }
        }

        winrt::hstring FormatDxgiColorSpace(DXGI_COLOR_SPACE_TYPE colorSpace)
        {
            switch (colorSpace)
            {
            case DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709:
                return L"RGB_FULL_G22_NONE_P709";
            case DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020:
                return L"RGB_FULL_G2084_NONE_P2020";
            case DXGI_COLOR_SPACE_RGB_STUDIO_G2084_NONE_P2020:
                return L"RGB_STUDIO_G2084_NONE_P2020";
            case DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P2020:
                return L"RGB_FULL_G22_NONE_P2020";
            case DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P2020:
                return L"RGB_STUDIO_G22_NONE_P2020";
            case DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P709:
                return L"RGB_STUDIO_G22_NONE_P709";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_LEFT_P2020:
                return L"YCBCR_STUDIO_G2084_LEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_TOPLEFT_P2020:
                return L"YCBCR_STUDIO_G2084_TOPLEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_GHLG_TOPLEFT_P2020:
                return L"YCBCR_STUDIO_GHLG_TOPLEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_FULL_GHLG_TOPLEFT_P2020:
                return L"YCBCR_FULL_GHLG_TOPLEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P2020:
                return L"YCBCR_STUDIO_G22_LEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P2020:
                return L"YCBCR_FULL_G22_LEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_TOPLEFT_P2020:
                return L"YCBCR_STUDIO_G22_TOPLEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P601:
                return L"YCBCR_STUDIO_G22_LEFT_P601";
            case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P601:
                return L"YCBCR_FULL_G22_LEFT_P601";
            case DXGI_COLOR_SPACE_YCBCR_FULL_G22_NONE_P709_X601:
                return L"YCBCR_FULL_G22_NONE_P709_X601";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P709:
                return L"YCBCR_STUDIO_G22_LEFT_P709";
            case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P709:
                return L"YCBCR_FULL_G22_LEFT_P709";
            case DXGI_COLOR_SPACE_CUSTOM:
                return L"CUSTOM";
            default:
                return winrt::to_hstring(static_cast<int32_t>(colorSpace));
            }
        }

        std::wstring FormatBool(bool value)
        {
            return value ? L"true" : L"false";
        }

        std::wstring FormatHdrSnapshot(HdrDisplaySnapshot const& snapshot)
        {
            return L"status=" + std::to_wstring(static_cast<int32_t>(snapshot.Status)) +
                L" display=" + FormatBool(snapshot.IsHdrDisplayAvailable) +
                L" active=" + FormatBool(snapshot.IsHdrOutputActive) +
                L" refresh=" + std::to_wstring(snapshot.RefreshRateHz) +
                L" message=" + std::wstring(snapshot.Message.c_str());
        }

        PlaybackGraphOpenRequest CreatePlaybackGraphOpenRequest(
            NoiraPlayer::Native::NativePlaybackOpenRequest const& request)
        {
            if (request == nullptr)
            {
                throw winrt::hresult_invalid_argument(L"Playback request is required.");
            }

            return PlaybackGraphOpenRequest
            {
                request.DirectStreamUrl(),
                request.StartPositionTicks(),
                request.AudioStreamIndex(),
                request.HasAudioStreamIndex(),
                request.SubtitleStreamIndex(),
                request.HasSubtitleStreamIndex(),
                request.VideoFrameRate()
            };
        }
    }

    NativePlaybackEngine::NativePlaybackEngine()
        : m_graph(std::make_unique<PlaybackGraph>(
              m_dx,
              [this](PlaybackGraphState state, winrt::hstring const& message)
              {
                  OnGraphStateChanged(state, message);
              },
              [this](bool desiredHdrOutput, double preferredRefreshRate)
              {
                  return OnGraphHdrOutputChanged(desiredHdrOutput, preferredRefreshRate);
              }))
    {
        UpdateDisplayStatus(m_hdr.Probe());
    }

    winrt::event_token NativePlaybackEngine::StateChanged(
        NoiraPlayer::Native::NativePlaybackStateChangedHandler const& handler)
    {
        return m_stateChanged.add(handler);
    }

    void NativePlaybackEngine::StateChanged(winrt::event_token const& token) noexcept
    {
        m_stateChanged.remove(token);
    }

    void NativePlaybackEngine::AttachSurface(winrt::Windows::UI::Xaml::Controls::SwapChainPanel const& panel)
    {
        m_dx.AttachSurface(panel);
    }

    int64_t NativePlaybackEngine::CurrentPositionTicks() const
    {
        return m_graph ? m_graph->CurrentPositionTicks() : m_positionTicks;
    }

    NoiraPlayer::Native::NativePlaybackStatus NativePlaybackEngine::DisplayStatus() const
    {
        auto status = winrt::make<NativePlaybackStatus>();
        if (m_displayStatus == nullptr)
        {
            status.HdrStatus(NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Unknown);
            status.IsHdrDisplayAvailable(false);
            status.IsHdrOutputActive(false);
            status.Message(L"Native engine has not probed the display yet.");
        }
        else
        {
            status.HdrStatus(m_displayStatus.HdrStatus());
            status.IsHdrDisplayAvailable(m_displayStatus.IsHdrDisplayAvailable());
            status.IsHdrOutputActive(m_displayStatus.IsHdrOutputActive());
            status.RefreshRateHz(m_displayStatus.RefreshRateHz());
            status.Message(m_displayStatus.Message());
        }

        status.SwapChainFormat(FormatDxgiFormat(m_dx.SwapChainFormat()));
        status.SwapChainColorSpace(FormatDxgiColorSpace(m_dx.SwapChainColorSpace()));
        status.IsTenBitSwapChain(m_dx.IsTenBitSwapChain());
        status.IsVideoProcessorColorSpaceValidated(m_dx.LastVideoProcessorConversionWasValidated());
        status.VideoProcessorInputColorSpace(FormatDxgiColorSpace(m_dx.LastVideoProcessorInputColorSpace()));
        status.VideoProcessorOutputColorSpace(FormatDxgiColorSpace(m_dx.LastVideoProcessorOutputColorSpace()));
        status.VideoProcessorConversionStatus(winrt::hstring(m_dx.LastVideoProcessorConversionStatus()));
        return status;
    }

    NoiraPlayer::Native::NativePlaybackQualityMetrics NativePlaybackEngine::QualityMetrics() const
    {
        auto snapshot = m_graph->QualityMetricsSnapshot();
        auto const observedSource = m_graph->VideoSourceSnapshot();
        auto metrics = winrt::make<NativePlaybackQualityMetrics>();
        metrics.ObservedVideoSourceAvailable(observedSource.has_value());
        if (observedSource)
        {
            metrics.ObservedVideoCodec(winrt::to_hstring(observedSource->Codec));
            metrics.ObservedVideoWidth(observedSource->Width);
            metrics.ObservedVideoHeight(observedSource->Height);
            metrics.ObservedVideoFrameRate(observedSource->FrameRate);
            metrics.ObservedVideoRange(winrt::to_hstring(observedSource->VideoRange));
            metrics.ObservedColorPrimaries(winrt::to_hstring(observedSource->ColorPrimaries));
            metrics.ObservedColorTransfer(winrt::to_hstring(observedSource->ColorTransfer));
            metrics.ObservedColorSpace(winrt::to_hstring(observedSource->ColorSpace));
            metrics.ObservedHdrKind(winrt::to_hstring(observedSource->HdrKind));
            metrics.ObservedIsDolbyVision(observedSource->IsDolbyVision);
            metrics.ObservedDolbyVisionProfile(observedSource->DolbyVisionProfile);
            metrics.ObservedDolbyVisionCompatibilityId(observedSource->DolbyVisionCompatibilityId);
            metrics.ObservedHasHdr10BaseLayer(observedSource->HasHdr10BaseLayer);
            metrics.ObservedHasHlgBaseLayer(observedSource->HasHlgBaseLayer);
        }
        metrics.RenderPasses(snapshot.RenderPasses);
        metrics.DecodedVideoFrames(snapshot.DecodedVideoFrames);
        metrics.HardwareDecodedVideoFrames(snapshot.HardwareDecodedVideoFrames);
        metrics.SoftwareDecodedVideoFrames(snapshot.SoftwareDecodedVideoFrames);
        metrics.RenderedVideoFrames(snapshot.RenderedVideoFrames);
        metrics.SubmittedAudioFrames(snapshot.SubmittedAudioFrames);
        metrics.SelectedAudioStreamIndex(m_graph->SelectedAudioStreamIndex().value_or(-1));
        metrics.SubtitleDecodedCueCount(m_graph->SubtitleDecodedCueCount());
        metrics.SubtitleCueRenderCount(m_graph->SubtitleCueRenderCount());
        metrics.SelectedSubtitleStreamIndex(
            m_graph->SelectedSubtitleStreamIndex().value_or(-1));
        metrics.DroppedVideoFrames(snapshot.DroppedVideoFrames);
        metrics.SeekPrerollDroppedFrames(snapshot.SeekPrerollDroppedFrames);
        metrics.VideoAheadWaitCount(snapshot.VideoAheadWaitCount);
        metrics.AudioAheadWaitCount(snapshot.AudioAheadWaitCount);
        metrics.VideoClockWaitCount(snapshot.VideoClockWaitCount);
        metrics.VideoStarvedPasses(snapshot.VideoStarvedPasses);
        metrics.AudioStarvedPasses(snapshot.AudioStarvedPasses);
        metrics.QueuedAudioBuffers(snapshot.QueuedAudioBuffers);
        metrics.AudioClockTicks(snapshot.AudioClockTicks);
        metrics.VideoPositionTicks(snapshot.VideoPositionTicks);
        metrics.NativeGraphOpenDurationMs(snapshot.NativeGraphOpenDurationMs);
        metrics.FfmpegOpenInputDurationMs(snapshot.FfmpegOpenInputDurationMs);
        metrics.FfmpegStreamInfoDurationMs(snapshot.FfmpegStreamInfoDurationMs);
        metrics.NativeStartupSeekDurationMs(snapshot.NativeStartupSeekDurationMs);
        metrics.FfmpegOpenInputBytesRead(snapshot.FfmpegOpenInputBytesRead);
        metrics.FfmpegStreamInfoBytesRead(snapshot.FfmpegStreamInfoBytesRead);
        metrics.NativeStartupSeekBytesRead(snapshot.NativeStartupSeekBytesRead);
        metrics.NativeFirstFrameTransportBytesRead(snapshot.NativeFirstFrameTransportBytesRead);
        metrics.StartupTransportProvider(winrt::to_hstring(snapshot.StartupTransportProvider));
        metrics.StartupTransportCallEvidenceAvailable(snapshot.StartupTransportCallEvidenceAvailable);
        metrics.FfmpegOpenInputTransportProvider(winrt::to_hstring(snapshot.FfmpegOpenInputTransportCalls.Provider));
        metrics.FfmpegOpenInputTransportCallEvidenceAvailable(snapshot.FfmpegOpenInputTransportCalls.EvidenceAvailable);
        metrics.FfmpegOpenInputTransportReadCalls(snapshot.FfmpegOpenInputTransportCalls.ReadCalls);
        metrics.FfmpegOpenInputTransportSeekCalls(snapshot.FfmpegOpenInputTransportCalls.SeekCalls);
        metrics.FfmpegOpenInputTransportReadWaitMs(snapshot.FfmpegOpenInputTransportCalls.ReadWaitMs);
        metrics.FfmpegOpenInputTransportSeekWaitMs(snapshot.FfmpegOpenInputTransportCalls.SeekWaitMs);
        metrics.FfmpegOpenInputTransportSeekDistanceBytes(snapshot.FfmpegOpenInputTransportCalls.SeekDistanceBytes);
        metrics.FfmpegStreamInfoTransportProvider(winrt::to_hstring(snapshot.FfmpegStreamInfoTransportCalls.Provider));
        metrics.FfmpegStreamInfoTransportCallEvidenceAvailable(snapshot.FfmpegStreamInfoTransportCalls.EvidenceAvailable);
        metrics.FfmpegStreamInfoTransportReadCalls(snapshot.FfmpegStreamInfoTransportCalls.ReadCalls);
        metrics.FfmpegStreamInfoTransportSeekCalls(snapshot.FfmpegStreamInfoTransportCalls.SeekCalls);
        metrics.FfmpegStreamInfoTransportReadWaitMs(snapshot.FfmpegStreamInfoTransportCalls.ReadWaitMs);
        metrics.FfmpegStreamInfoTransportSeekWaitMs(snapshot.FfmpegStreamInfoTransportCalls.SeekWaitMs);
        metrics.FfmpegStreamInfoTransportSeekDistanceBytes(snapshot.FfmpegStreamInfoTransportCalls.SeekDistanceBytes);
        metrics.NativeStartupSeekTransportProvider(winrt::to_hstring(snapshot.NativeStartupSeekTransportCalls.Provider));
        metrics.NativeStartupSeekTransportCallEvidenceAvailable(snapshot.NativeStartupSeekTransportCalls.EvidenceAvailable);
        metrics.NativeStartupSeekTransportReadCalls(snapshot.NativeStartupSeekTransportCalls.ReadCalls);
        metrics.NativeStartupSeekTransportSeekCalls(snapshot.NativeStartupSeekTransportCalls.SeekCalls);
        metrics.NativeStartupSeekTransportReadWaitMs(snapshot.NativeStartupSeekTransportCalls.ReadWaitMs);
        metrics.NativeStartupSeekTransportSeekWaitMs(snapshot.NativeStartupSeekTransportCalls.SeekWaitMs);
        metrics.NativeStartupSeekTransportSeekDistanceBytes(snapshot.NativeStartupSeekTransportCalls.SeekDistanceBytes);
        metrics.NativeFirstFrameTransportProvider(winrt::to_hstring(snapshot.NativeFirstFrameTransportCalls.Provider));
        metrics.NativeFirstFrameTransportCallEvidenceAvailable(snapshot.NativeFirstFrameTransportCalls.EvidenceAvailable);
        metrics.NativeFirstFrameTransportReadCalls(snapshot.NativeFirstFrameTransportCalls.ReadCalls);
        metrics.NativeFirstFrameTransportSeekCalls(snapshot.NativeFirstFrameTransportCalls.SeekCalls);
        metrics.NativeFirstFrameTransportReadWaitMs(snapshot.NativeFirstFrameTransportCalls.ReadWaitMs);
        metrics.NativeFirstFrameTransportSeekWaitMs(snapshot.NativeFirstFrameTransportCalls.SeekWaitMs);
        metrics.NativeFirstFrameTransportSeekDistanceBytes(snapshot.NativeFirstFrameTransportCalls.SeekDistanceBytes);
        metrics.NativeFirstFrameDurationMs(snapshot.NativeFirstFrameDurationMs);
        metrics.NativeFirstFrameDemuxReadDurationMs(snapshot.NativeFirstFrameDemuxReadDurationMs);
        metrics.NativeFirstFramePresentDurationMs(snapshot.NativeFirstFramePresentDurationMs);
        metrics.NativeFirstFrameDemuxPacketCount(snapshot.NativeFirstFrameDemuxPacketCount);
        metrics.NativeFirstFrameDemuxBytes(snapshot.NativeFirstFrameDemuxBytes);
        metrics.PlaybackDemuxReadDurationMs(snapshot.PlaybackDemuxReadDurationMs);
        metrics.PlaybackDemuxPacketCount(snapshot.PlaybackDemuxPacketCount);
        metrics.PlaybackDemuxBytes(snapshot.PlaybackDemuxBytes);
        metrics.PlaybackTransportProvider(winrt::to_hstring(snapshot.PlaybackTransportCalls.Provider));
        metrics.PlaybackTransportCallEvidenceAvailable(snapshot.PlaybackTransportCalls.EvidenceAvailable);
        metrics.PlaybackTransportReadCalls(snapshot.PlaybackTransportCalls.ReadCalls);
        metrics.PlaybackTransportSeekCalls(snapshot.PlaybackTransportCalls.SeekCalls);
        metrics.PlaybackTransportReadWaitMs(snapshot.PlaybackTransportCalls.ReadWaitMs);
        metrics.PlaybackTransportSeekWaitMs(snapshot.PlaybackTransportCalls.SeekWaitMs);
        metrics.PlaybackTransportSeekDistanceBytes(snapshot.PlaybackTransportCalls.SeekDistanceBytes);
        metrics.ReadErrorCount(snapshot.ReadErrorCount);
        metrics.ReadRetryCount(snapshot.ReadRetryCount);
        metrics.ReadRecoveryCount(snapshot.ReadRecoveryCount);
        metrics.MaxConsecutiveReadErrors(snapshot.MaxConsecutiveReadErrors);
        metrics.LastReadErrorCode(snapshot.LastReadErrorCode);
        metrics.FatalReadErrorCode(snapshot.FatalReadErrorCode);
        metrics.LastReadRecoveryDurationMs(snapshot.LastReadRecoveryDurationMs);
        metrics.ContainerStartTimeTicks(snapshot.ContainerStartTimeTicks);
        metrics.VideoStreamStartTimeTicks(snapshot.VideoStreamStartTimeTicks);
        metrics.SeekDemuxTargetTicks(snapshot.SeekDemuxTargetTicks);
        metrics.FirstPresentedPositionTicks(snapshot.FirstPresentedPositionTicks);
        auto seekTiming = m_graph->LastSeekTimingSnapshot();
        metrics.SeekLockWaitDurationMs(seekTiming.LockWaitDurationMs);
        metrics.SeekExecutionDurationMs(seekTiming.ExecutionDurationMs);
        metrics.SeekQuiesceDurationMs(seekTiming.QuiesceDurationMs);
        metrics.SeekReplayPreparationDurationMs(seekTiming.ReplayPreparationDurationMs);
        metrics.SeekStateResetDurationMs(seekTiming.StateResetDurationMs);
        metrics.SeekMediaRepositionDurationMs(seekTiming.MediaRepositionDurationMs);
        metrics.SeekDependentDecoderFlushDurationMs(seekTiming.DependentDecoderFlushDurationMs);
        metrics.SeekPrerollRenderDurationMs(seekTiming.PrerollRenderDurationMs);
        metrics.SeekWorkerRestartDurationMs(seekTiming.WorkerRestartDurationMs);
        auto seekReplay = m_graph->LastSeekReplaySnapshot();
        metrics.SeekPacketCacheEnabled(seekReplay.Enabled);
        metrics.SeekPacketCacheHit(seekReplay.Hit);
        metrics.SeekPacketCachePacketCount(seekReplay.PacketCount);
        metrics.SeekPacketCacheBytes(seekReplay.Bytes);
        metrics.SeekPacketCacheWindowDurationTicks(seekReplay.WindowDurationTicks);
        metrics.SeekFallbackReason(winrt::to_hstring(
            seekReplay.FallbackReason.empty() ? "none" : seekReplay.FallbackReason));
        metrics.RenderIntervalMsP05(snapshot.RenderIntervalMsP05);
        metrics.RenderIntervalMsP50(snapshot.RenderIntervalMsP50);
        metrics.RenderIntervalMsP95(snapshot.RenderIntervalMsP95);
        metrics.RenderIntervalMsP99(snapshot.RenderIntervalMsP99);
        metrics.MinFrameGapMs(snapshot.MinFrameGapMs);
        metrics.MaxFrameGapMs(snapshot.MaxFrameGapMs);
        metrics.RenderIntervalSampleCount(snapshot.RenderIntervalSampleCount);
        metrics.RenderIntervalOverExpected2MsCount(snapshot.RenderIntervalOverExpected2MsCount);
        metrics.RenderIntervalOverExpected4MsCount(snapshot.RenderIntervalOverExpected4MsCount);
        metrics.RenderIntervalUnderExpected2MsCount(snapshot.RenderIntervalUnderExpected2MsCount);
        metrics.RenderIntervalUnderExpected4MsCount(snapshot.RenderIntervalUnderExpected4MsCount);
        metrics.RenderIntervalAfterAudioAheadWaitSampleCount(snapshot.RenderIntervalAfterAudioAheadWaitSampleCount);
        metrics.RenderIntervalAfterAudioAheadWaitMsP95(snapshot.RenderIntervalAfterAudioAheadWaitMsP95);
        metrics.RenderIntervalAfterAudioAheadWaitMsP99(snapshot.RenderIntervalAfterAudioAheadWaitMsP99);
        metrics.RenderIntervalAfterAudioAheadWaitMsMax(snapshot.RenderIntervalAfterAudioAheadWaitMsMax);
        metrics.AudioAheadWaitEndToPresentSampleCount(snapshot.AudioAheadWaitEndToPresentSampleCount);
        metrics.AudioAheadWaitEndToPresentMsP50(snapshot.AudioAheadWaitEndToPresentMsP50);
        metrics.AudioAheadWaitEndToPresentMsP95(snapshot.AudioAheadWaitEndToPresentMsP95);
        metrics.AudioAheadWaitEndToPresentMsP99(snapshot.AudioAheadWaitEndToPresentMsP99);
        metrics.AudioAheadWaitEndToPresentMsMax(snapshot.AudioAheadWaitEndToPresentMsMax);
        metrics.RenderIntervalAfterNonAudioWaitSampleCount(snapshot.RenderIntervalAfterNonAudioWaitSampleCount);
        metrics.RenderIntervalAfterNonAudioWaitMsP95(snapshot.RenderIntervalAfterNonAudioWaitMsP95);
        metrics.RenderIntervalAfterNonAudioWaitMsP99(snapshot.RenderIntervalAfterNonAudioWaitMsP99);
        metrics.RenderIntervalAfterNonAudioWaitMsMax(snapshot.RenderIntervalAfterNonAudioWaitMsMax);
        metrics.PresentDurationMsP50(snapshot.PresentDurationMsP50);
        metrics.PresentDurationMsP95(snapshot.PresentDurationMsP95);
        metrics.PresentDurationMsP99(snapshot.PresentDurationMsP99);
        metrics.PresentDurationMsMax(snapshot.PresentDurationMsMax);
        metrics.VideoDecodeDurationMsP50(snapshot.VideoDecodeDurationMsP50);
        metrics.VideoDecodeDurationMsP95(snapshot.VideoDecodeDurationMsP95);
        metrics.VideoDecodeDurationMsP99(snapshot.VideoDecodeDurationMsP99);
        metrics.VideoDecodeDurationMsMax(snapshot.VideoDecodeDurationMsMax);
        metrics.VideoDecodeDeviceMode(winrt::to_hstring(snapshot.VideoDecodeDeviceMode));
        metrics.VideoDecodeSynchronizationMode(winrt::to_hstring(snapshot.VideoDecodeSynchronizationMode));
        metrics.VideoDecodeWorkerActive(snapshot.VideoDecodeWorkerActive);
        metrics.VideoDecodeQueueCapacity(snapshot.VideoDecodeQueueCapacity);
        metrics.VideoDecodeQueueMaxDepth(snapshot.VideoDecodeQueueMaxDepth);
        metrics.VideoDecodeQueueProducerWaitCount(snapshot.VideoDecodeQueueProducerWaitCount);
        metrics.VideoDecoderSendPacketEagainCount(snapshot.VideoDecoderSendPacketEagainCount);
        metrics.VideoDecoderDoubleEagainRetryCount(snapshot.VideoDecoderDoubleEagainRetryCount);
        metrics.VideoDecoderDoubleEagainRecoveryCount(snapshot.VideoDecoderDoubleEagainRecoveryCount);
        metrics.VideoDecoderDoubleEagainExhaustedCount(snapshot.VideoDecoderDoubleEagainExhaustedCount);
        metrics.VideoDecodePacketReadDurationMsP50(snapshot.VideoDecodePacketReadDurationMsP50);
        metrics.VideoDecodePacketReadDurationMsP95(snapshot.VideoDecodePacketReadDurationMsP95);
        metrics.VideoDecodeSendPacketDurationMsP50(snapshot.VideoDecodeSendPacketDurationMsP50);
        metrics.VideoDecodeSendPacketDurationMsP95(snapshot.VideoDecodeSendPacketDurationMsP95);
        metrics.VideoDecodeReceiveFrameDurationMsP50(snapshot.VideoDecodeReceiveFrameDurationMsP50);
        metrics.VideoDecodeReceiveFrameDurationMsP95(snapshot.VideoDecodeReceiveFrameDurationMsP95);
        metrics.VideoDecodeFrameMaterializeDurationMsP50(snapshot.VideoDecodeFrameMaterializeDurationMsP50);
        metrics.VideoDecodeFrameMaterializeDurationMsP95(snapshot.VideoDecodeFrameMaterializeDurationMsP95);
        metrics.VideoRenderDurationMsP50(snapshot.VideoRenderDurationMsP50);
        metrics.VideoRenderDurationMsP95(snapshot.VideoRenderDurationMsP95);
        metrics.VideoRenderDurationMsP99(snapshot.VideoRenderDurationMsP99);
        metrics.VideoRenderDurationMsMax(snapshot.VideoRenderDurationMsMax);
        metrics.VideoRenderDirectCopyFrameCount(snapshot.VideoRenderDirectCopyFrameCount);
        metrics.VideoRenderVideoProcessorFrameCount(snapshot.VideoRenderVideoProcessorFrameCount);
        metrics.VideoRenderBgraFrameCount(snapshot.VideoRenderBgraFrameCount);
        metrics.VideoRenderPostProcessFrameCount(snapshot.VideoRenderPostProcessFrameCount);
        metrics.VideoProcessorSetupCpuSampleCount(snapshot.VideoProcessorSetupCpuSampleCount);
        metrics.VideoProcessorSetupCpuDurationMsP50(snapshot.VideoProcessorSetupCpuDurationMsP50);
        metrics.VideoProcessorSetupCpuDurationMsP95(snapshot.VideoProcessorSetupCpuDurationMsP95);
        metrics.VideoProcessorSetupCpuDurationMsP99(snapshot.VideoProcessorSetupCpuDurationMsP99);
        metrics.VideoProcessorSetupCpuDurationMsMax(snapshot.VideoProcessorSetupCpuDurationMsMax);
        metrics.VideoProcessorViewTargetCpuSampleCount(snapshot.VideoProcessorViewTargetCpuSampleCount);
        metrics.VideoProcessorViewTargetCpuDurationMsP50(snapshot.VideoProcessorViewTargetCpuDurationMsP50);
        metrics.VideoProcessorViewTargetCpuDurationMsP95(snapshot.VideoProcessorViewTargetCpuDurationMsP95);
        metrics.VideoProcessorViewTargetCpuDurationMsP99(snapshot.VideoProcessorViewTargetCpuDurationMsP99);
        metrics.VideoProcessorViewTargetCpuDurationMsMax(snapshot.VideoProcessorViewTargetCpuDurationMsMax);
        metrics.VideoProcessorClearCpuSampleCount(snapshot.VideoProcessorClearCpuSampleCount);
        metrics.VideoProcessorClearCpuDurationMsP50(snapshot.VideoProcessorClearCpuDurationMsP50);
        metrics.VideoProcessorClearCpuDurationMsP95(snapshot.VideoProcessorClearCpuDurationMsP95);
        metrics.VideoProcessorClearCpuDurationMsP99(snapshot.VideoProcessorClearCpuDurationMsP99);
        metrics.VideoProcessorClearCpuDurationMsMax(snapshot.VideoProcessorClearCpuDurationMsMax);
        metrics.VideoProcessorBltCpuSampleCount(snapshot.VideoProcessorBltCpuSampleCount);
        metrics.VideoProcessorBltCpuDurationMsP50(snapshot.VideoProcessorBltCpuDurationMsP50);
        metrics.VideoProcessorBltCpuDurationMsP95(snapshot.VideoProcessorBltCpuDurationMsP95);
        metrics.VideoProcessorBltCpuDurationMsP99(snapshot.VideoProcessorBltCpuDurationMsP99);
        metrics.VideoProcessorBltCpuDurationMsMax(snapshot.VideoProcessorBltCpuDurationMsMax);
        metrics.VideoProcessorPostProcessCpuSampleCount(snapshot.VideoProcessorPostProcessCpuSampleCount);
        metrics.VideoProcessorPostProcessCpuDurationMsP50(snapshot.VideoProcessorPostProcessCpuDurationMsP50);
        metrics.VideoProcessorPostProcessCpuDurationMsP95(snapshot.VideoProcessorPostProcessCpuDurationMsP95);
        metrics.VideoProcessorPostProcessCpuDurationMsP99(snapshot.VideoProcessorPostProcessCpuDurationMsP99);
        metrics.VideoProcessorPostProcessCpuDurationMsMax(snapshot.VideoProcessorPostProcessCpuDurationMsMax);
        metrics.AudioAheadWaitDurationMsP50(snapshot.AudioAheadWaitDurationMsP50);
        metrics.AudioAheadWaitDurationMsP95(snapshot.AudioAheadWaitDurationMsP95);
        metrics.AudioAheadWaitDurationMsP99(snapshot.AudioAheadWaitDurationMsP99);
        metrics.AudioAheadWaitDurationMsMax(snapshot.AudioAheadWaitDurationMsMax);
        metrics.AudioAheadWaitTargetMsP50(snapshot.AudioAheadWaitTargetMsP50);
        metrics.AudioAheadWaitTargetMsP95(snapshot.AudioAheadWaitTargetMsP95);
        metrics.AudioAheadWaitTargetMsP99(snapshot.AudioAheadWaitTargetMsP99);
        metrics.AudioAheadWaitTargetMsMax(snapshot.AudioAheadWaitTargetMsMax);
        metrics.AudioAheadWaitOversleepMsP50(snapshot.AudioAheadWaitOversleepMsP50);
        metrics.AudioAheadWaitOversleepMsP95(snapshot.AudioAheadWaitOversleepMsP95);
        metrics.AudioAheadWaitOversleepMsP99(snapshot.AudioAheadWaitOversleepMsP99);
        metrics.AudioAheadWaitOversleepMsMax(snapshot.AudioAheadWaitOversleepMsMax);
        metrics.AudioAheadWaitFinalDeltaAbsMsP50(snapshot.AudioAheadWaitFinalDeltaAbsMsP50);
        metrics.AudioAheadWaitFinalDeltaAbsMsP95(snapshot.AudioAheadWaitFinalDeltaAbsMsP95);
        metrics.AudioAheadWaitFinalDeltaAbsMsP99(snapshot.AudioAheadWaitFinalDeltaAbsMsP99);
        metrics.AudioAheadWaitFinalDeltaAbsMsMax(snapshot.AudioAheadWaitFinalDeltaAbsMsMax);
        metrics.AudioAheadWaitEpisodeCount(snapshot.AudioAheadWaitEpisodeCount);
        metrics.AudioAheadWaitPassesPerEpisodeP50(snapshot.AudioAheadWaitPassesPerEpisodeP50);
        metrics.AudioAheadWaitPassesPerEpisodeP95(snapshot.AudioAheadWaitPassesPerEpisodeP95);
        metrics.AudioAheadWaitPassesPerEpisodeP99(snapshot.AudioAheadWaitPassesPerEpisodeP99);
        metrics.AudioAheadWaitPassesPerEpisodeMax(snapshot.AudioAheadWaitPassesPerEpisodeMax);
        metrics.AudioAheadWaitPassDurationMsP50(snapshot.AudioAheadWaitPassDurationMsP50);
        metrics.AudioAheadWaitPassDurationMsP95(snapshot.AudioAheadWaitPassDurationMsP95);
        metrics.AudioAheadWaitPassDurationMsP99(snapshot.AudioAheadWaitPassDurationMsP99);
        metrics.AudioAheadWaitPassDurationMsMax(snapshot.AudioAheadWaitPassDurationMsMax);
        metrics.AudioAheadWaitPassTargetMsP50(snapshot.AudioAheadWaitPassTargetMsP50);
        metrics.AudioAheadWaitPassTargetMsP95(snapshot.AudioAheadWaitPassTargetMsP95);
        metrics.AudioAheadWaitPassTargetMsP99(snapshot.AudioAheadWaitPassTargetMsP99);
        metrics.AudioAheadWaitPassTargetMsMax(snapshot.AudioAheadWaitPassTargetMsMax);
        metrics.AudioAheadWaitPassOversleepMsP50(snapshot.AudioAheadWaitPassOversleepMsP50);
        metrics.AudioAheadWaitPassOversleepMsP95(snapshot.AudioAheadWaitPassOversleepMsP95);
        metrics.AudioAheadWaitPassOversleepMsP99(snapshot.AudioAheadWaitPassOversleepMsP99);
        metrics.AudioAheadWaitPassOversleepMsMax(snapshot.AudioAheadWaitPassOversleepMsMax);
        metrics.FramePacingSourceFrameRate(snapshot.FramePacingSourceFrameRate);
        metrics.LateFrameDropToleranceMs(snapshot.LateFrameDropToleranceMs);
        metrics.AudioVideoDriftMsP50(snapshot.AudioVideoDriftMsP50);
        metrics.AudioVideoDriftMsP95(snapshot.AudioVideoDriftMsP95);
        metrics.AudioVideoDriftMsP99(snapshot.AudioVideoDriftMsP99);
        metrics.AudioVideoDriftMsMax(snapshot.AudioVideoDriftMsMax);
        std::scoped_lock interactionLock(m_interactionTimingMutex);
        auto const& interaction = m_lastInteractionTiming;
        metrics.LastInteractionScenario(m_lastInteractionScenario);
        metrics.LastInteractionSequence(m_lastInteractionSequence);
        metrics.LastInteractionLockWaitDurationMs(interaction.LockWaitDurationMs);
        metrics.LastInteractionExecutionDurationMs(interaction.ExecutionDurationMs);
        metrics.LastInteractionQuiesceDurationMs(interaction.QuiesceDurationMs);
        metrics.LastInteractionSeekDurationMs(interaction.SeekDurationMs);
        metrics.LastInteractionDecoderOpenDurationMs(interaction.DecoderOpenDurationMs);
        metrics.LastInteractionRendererOpenDurationMs(interaction.RendererOpenDurationMs);
        metrics.LastInteractionPacketCacheHit(interaction.PacketCacheHit);
        metrics.LastInteractionPacketCacheEnabled(interaction.PacketCacheEnabled);
        metrics.LastInteractionPacketCachePacketCount(interaction.PacketCachePacketCount);
        metrics.LastInteractionPacketCacheBytes(interaction.PacketCacheBytes);
        metrics.LastInteractionPacketCacheWindowDurationTicks(interaction.PacketCacheWindowDurationTicks);
        return metrics;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::OpenAsync(
        NoiraPlayer::Native::NativePlaybackOpenRequest request)
    {
        auto lifetime = get_strong();
        try
        {
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync enter");
            auto graphRequest = CreatePlaybackGraphOpenRequest(request);
            auto videoFrameRate = graphRequest.VideoFrameRate;
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync request videoFrameRate=" + std::to_wstring(videoFrameRate));
            UpdateDisplayStatus(m_hdr.Probe());
            co_await winrt::resume_background();

            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync graph Open begin");
            ResetLastInteractionTiming();
            m_graph->Open(graphRequest);
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync graph Open end");
            m_positionTicks = m_graph->CurrentPositionTicks();
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync CurrentPositionTicks read");
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync Raise Opening begin");
            Raise(NoiraPlayer::Native::NativePlaybackState::NativePlaybackState_Opening);
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync Raise Playing begin");
            Raise(NoiraPlayer::Native::NativePlaybackState::NativePlaybackState_Playing);
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync success end");
        }
        catch (winrt::hresult_error const& error)
        {
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync hresult exception " + std::wstring(error.message().c_str()));
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            AppendNativePlaybackDiagnostic(
                L"NativePlaybackEngine.OpenAsync std exception " +
                std::wstring(winrt::to_hstring(error.what()).c_str()));
            RaiseFailed(error);
        }

        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::PauseAsync()
    {
        auto lifetime = get_strong();
        co_await winrt::resume_background();
        try
        {
            m_graph->Pause();
            Raise(NoiraPlayer::Native::NativePlaybackState::NativePlaybackState_Paused);
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::ResumeAsync()
    {
        auto lifetime = get_strong();
        co_await winrt::resume_background();
        try
        {
            m_graph->Resume();
            Raise(NoiraPlayer::Native::NativePlaybackState::NativePlaybackState_Playing);
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::SeekAsync(int64_t positionTicks)
    {
        auto lifetime = get_strong();
        co_await winrt::resume_background();
        try
        {
            m_graph->Seek(positionTicks);
            m_positionTicks = m_graph->CurrentPositionTicks();
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::StopAsync()
    {
        auto lifetime = get_strong();
        co_await winrt::resume_background();
        try
        {
            m_graph->Stop();
            ResetLastInteractionTiming();
            m_positionTicks = m_graph->CurrentPositionTicks();

            auto display = m_hdr.RestoreInitialState();
            UpdateDisplayStatus(display);
            ApplySwapChainColorSpace(display);
            Raise(NoiraPlayer::Native::NativePlaybackState::NativePlaybackState_Stopped);
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::SwitchAudioStreamAsync(int32_t audioStreamIndex)
    {
        auto lifetime = get_strong();
        co_await winrt::resume_background();
        try
        {
            auto const timing = m_graph->SwitchAudioStream(audioStreamIndex);
            StoreLastInteractionTiming(L"audio-switch", timing);
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::SwitchSubtitleStreamAsync(int32_t subtitleStreamIndex)
    {
        auto lifetime = get_strong();
        co_await winrt::resume_background();
        try
        {
            auto const timing = m_graph->SwitchSubtitleStream(subtitleStreamIndex);
            StoreLastInteractionTiming(L"subtitle-switch", timing);
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::DisableSubtitlesAsync()
    {
        auto lifetime = get_strong();
        co_await winrt::resume_background();
        try
        {
            auto const timing = m_graph->SwitchSubtitleStream(std::nullopt);
            StoreLastInteractionTiming(L"subtitle-switch", timing);
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    void NativePlaybackEngine::ResetLastInteractionTiming() noexcept
    {
        std::scoped_lock lock(m_interactionTimingMutex);
        m_lastInteractionTiming = {};
        m_lastInteractionScenario = L"";
        m_lastInteractionSequence = 0;
    }

    void NativePlaybackEngine::StoreLastInteractionTiming(
        winrt::hstring const& scenario,
        PlaybackGraphSwitchTiming const& timing) noexcept
    {
        std::scoped_lock lock(m_interactionTimingMutex);
        m_lastInteractionTiming = timing;
        m_lastInteractionScenario = scenario;
        ++m_lastInteractionSequence;
    }

    void NativePlaybackEngine::ApplySwapChainColorSpace(HdrDisplaySnapshot const& snapshot)
    {
        if (snapshot.IsHdrOutputActive)
        {
            m_dx.SetHdr10ColorSpace();
            return;
        }

        m_dx.SetSdrColorSpace();
    }

    bool NativePlaybackEngine::OnGraphHdrOutputChanged(
        bool desiredHdrOutput,
        double preferredRefreshRate)
    {
        AppendNativePlaybackDiagnostic(desiredHdrOutput
            ? L"NativePlaybackEngine.OnGraphHdrOutputChanged EnterHdr10 begin"
            : L"NativePlaybackEngine.OnGraphHdrOutputChanged LeaveHdr10 begin");
        auto display = desiredHdrOutput
            ? m_hdr.EnterHdr10(preferredRefreshRate)
            : m_hdr.LeaveHdr10();
        AppendNativePlaybackDiagnostic(
            L"NativePlaybackEngine.OnGraphHdrOutputChanged display end " + FormatHdrSnapshot(display));
        UpdateDisplayStatus(display);

        if (!display.IsHdrOutputActive)
        {
            m_dx.SetSdrColorSpace();
        }

        return desiredHdrOutput && display.IsHdrOutputActive;
    }

    void NativePlaybackEngine::OnGraphStateChanged(
        PlaybackGraphState state,
        winrt::hstring const& message)
    {
        switch (state)
        {
        case PlaybackGraphState::Stopped:
            Raise(NoiraPlayer::Native::NativePlaybackState::NativePlaybackState_Stopped, message);
            break;
        case PlaybackGraphState::Failed:
            Raise(NoiraPlayer::Native::NativePlaybackState::NativePlaybackState_Failed, message);
            break;
        }
    }

    void NativePlaybackEngine::RaiseFailed(std::exception const& error)
    {
        Raise(NoiraPlayer::Native::NativePlaybackState::NativePlaybackState_Failed, winrt::to_hstring(error.what()));
    }

    void NativePlaybackEngine::RaiseFailed(winrt::hresult_error const& error)
    {
        Raise(NoiraPlayer::Native::NativePlaybackState::NativePlaybackState_Failed, error.message());
    }

    void NativePlaybackEngine::Raise(NoiraPlayer::Native::NativePlaybackState state, winrt::hstring const& message)
    {
        m_stateChanged(state, message);
    }

    void NativePlaybackEngine::UpdateDisplayStatus(HdrDisplaySnapshot const& snapshot)
    {
        auto status = winrt::make<NativePlaybackStatus>();
        status.HdrStatus(snapshot.Status);
        status.IsHdrDisplayAvailable(snapshot.IsHdrDisplayAvailable);
        status.IsHdrOutputActive(snapshot.IsHdrOutputActive);
        status.RefreshRateHz(snapshot.RefreshRateHz);
        status.Message(snapshot.Message);
        status.SwapChainFormat(FormatDxgiFormat(m_dx.SwapChainFormat()));
        status.SwapChainColorSpace(FormatDxgiColorSpace(m_dx.SwapChainColorSpace()));
        status.IsTenBitSwapChain(m_dx.IsTenBitSwapChain());
        status.IsVideoProcessorColorSpaceValidated(m_dx.LastVideoProcessorConversionWasValidated());
        status.VideoProcessorInputColorSpace(FormatDxgiColorSpace(m_dx.LastVideoProcessorInputColorSpace()));
        status.VideoProcessorOutputColorSpace(FormatDxgiColorSpace(m_dx.LastVideoProcessorOutputColorSpace()));
        status.VideoProcessorConversionStatus(winrt::hstring(m_dx.LastVideoProcessorConversionStatus()));
        m_displayStatus = status;
    }
}
