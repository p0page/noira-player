#pragma once

#include "NativePlaybackQualityMetrics.g.h"

namespace winrt::NoiraPlayer::Native::implementation
{
    struct NativePlaybackQualityMetrics : NativePlaybackQualityMetricsT<NativePlaybackQualityMetrics>
    {
        NativePlaybackQualityMetrics() = default;

        uint64_t RenderPasses() const noexcept { return m_renderPasses; }
        void RenderPasses(uint64_t value) noexcept { m_renderPasses = value; }

        uint64_t DecodedVideoFrames() const noexcept { return m_decodedVideoFrames; }
        void DecodedVideoFrames(uint64_t value) noexcept { m_decodedVideoFrames = value; }

        uint64_t HardwareDecodedVideoFrames() const noexcept { return m_hardwareDecodedVideoFrames; }
        void HardwareDecodedVideoFrames(uint64_t value) noexcept { m_hardwareDecodedVideoFrames = value; }

        uint64_t SoftwareDecodedVideoFrames() const noexcept { return m_softwareDecodedVideoFrames; }
        void SoftwareDecodedVideoFrames(uint64_t value) noexcept { m_softwareDecodedVideoFrames = value; }

        uint64_t RenderedVideoFrames() const noexcept { return m_renderedVideoFrames; }
        void RenderedVideoFrames(uint64_t value) noexcept { m_renderedVideoFrames = value; }

        uint64_t SubmittedAudioFrames() const noexcept { return m_submittedAudioFrames; }
        void SubmittedAudioFrames(uint64_t value) noexcept { m_submittedAudioFrames = value; }

        int32_t SelectedAudioStreamIndex() const noexcept { return m_selectedAudioStreamIndex; }
        void SelectedAudioStreamIndex(int32_t value) noexcept { m_selectedAudioStreamIndex = value; }

        uint64_t SubtitleDecodedCueCount() const noexcept { return m_subtitleDecodedCueCount; }
        void SubtitleDecodedCueCount(uint64_t value) noexcept { m_subtitleDecodedCueCount = value; }

        uint64_t SubtitleCueRenderCount() const noexcept { return m_subtitleCueRenderCount; }
        void SubtitleCueRenderCount(uint64_t value) noexcept { m_subtitleCueRenderCount = value; }

        int32_t SelectedSubtitleStreamIndex() const noexcept { return m_selectedSubtitleStreamIndex; }
        void SelectedSubtitleStreamIndex(int32_t value) noexcept { m_selectedSubtitleStreamIndex = value; }

        uint64_t DroppedVideoFrames() const noexcept { return m_droppedVideoFrames; }
        void DroppedVideoFrames(uint64_t value) noexcept { m_droppedVideoFrames = value; }

        uint64_t SeekPrerollDroppedFrames() const noexcept { return m_seekPrerollDroppedFrames; }
        void SeekPrerollDroppedFrames(uint64_t value) noexcept { m_seekPrerollDroppedFrames = value; }

        uint64_t VideoAheadWaitCount() const noexcept { return m_videoAheadWaitCount; }
        void VideoAheadWaitCount(uint64_t value) noexcept { m_videoAheadWaitCount = value; }

        uint64_t AudioAheadWaitCount() const noexcept { return m_audioAheadWaitCount; }
        void AudioAheadWaitCount(uint64_t value) noexcept { m_audioAheadWaitCount = value; }

        uint64_t VideoClockWaitCount() const noexcept { return m_videoClockWaitCount; }
        void VideoClockWaitCount(uint64_t value) noexcept { m_videoClockWaitCount = value; }

        uint64_t VideoStarvedPasses() const noexcept { return m_videoStarvedPasses; }
        void VideoStarvedPasses(uint64_t value) noexcept { m_videoStarvedPasses = value; }

        uint64_t AudioStarvedPasses() const noexcept { return m_audioStarvedPasses; }
        void AudioStarvedPasses(uint64_t value) noexcept { m_audioStarvedPasses = value; }

        uint64_t QueuedAudioBuffers() const noexcept { return m_queuedAudioBuffers; }
        void QueuedAudioBuffers(uint64_t value) noexcept { m_queuedAudioBuffers = value; }

        int64_t AudioClockTicks() const noexcept { return m_audioClockTicks; }
        void AudioClockTicks(int64_t value) noexcept { m_audioClockTicks = value; }

        int64_t VideoPositionTicks() const noexcept { return m_videoPositionTicks; }
        void VideoPositionTicks(int64_t value) noexcept { m_videoPositionTicks = value; }

        double NativeGraphOpenDurationMs() const noexcept { return m_nativeGraphOpenDurationMs; }
        void NativeGraphOpenDurationMs(double value) noexcept { m_nativeGraphOpenDurationMs = value; }

        double FfmpegOpenInputDurationMs() const noexcept { return m_ffmpegOpenInputDurationMs; }
        void FfmpegOpenInputDurationMs(double value) noexcept { m_ffmpegOpenInputDurationMs = value; }

        double FfmpegStreamInfoDurationMs() const noexcept { return m_ffmpegStreamInfoDurationMs; }
        void FfmpegStreamInfoDurationMs(double value) noexcept { m_ffmpegStreamInfoDurationMs = value; }

        double NativeStartupSeekDurationMs() const noexcept { return m_nativeStartupSeekDurationMs; }
        void NativeStartupSeekDurationMs(double value) noexcept { m_nativeStartupSeekDurationMs = value; }

        uint64_t FfmpegOpenInputBytesRead() const noexcept { return m_ffmpegOpenInputBytesRead; }
        void FfmpegOpenInputBytesRead(uint64_t value) noexcept { m_ffmpegOpenInputBytesRead = value; }

        uint64_t FfmpegStreamInfoBytesRead() const noexcept { return m_ffmpegStreamInfoBytesRead; }
        void FfmpegStreamInfoBytesRead(uint64_t value) noexcept { m_ffmpegStreamInfoBytesRead = value; }

        uint64_t NativeStartupSeekBytesRead() const noexcept { return m_nativeStartupSeekBytesRead; }
        void NativeStartupSeekBytesRead(uint64_t value) noexcept { m_nativeStartupSeekBytesRead = value; }

        uint64_t NativeFirstFrameTransportBytesRead() const noexcept { return m_nativeFirstFrameTransportBytesRead; }
        void NativeFirstFrameTransportBytesRead(uint64_t value) noexcept { m_nativeFirstFrameTransportBytesRead = value; }

        winrt::hstring StartupTransportProvider() const { return m_startupTransportProvider; }
        void StartupTransportProvider(winrt::hstring const& value) { m_startupTransportProvider = value; }
        bool StartupTransportCallEvidenceAvailable() const noexcept { return m_startupTransportCallEvidenceAvailable; }
        void StartupTransportCallEvidenceAvailable(bool value) noexcept { m_startupTransportCallEvidenceAvailable = value; }

        winrt::hstring FfmpegOpenInputTransportProvider() const { return m_ffmpegOpenInputTransportProvider; }
        void FfmpegOpenInputTransportProvider(winrt::hstring const& value) { m_ffmpegOpenInputTransportProvider = value; }
        bool FfmpegOpenInputTransportCallEvidenceAvailable() const noexcept { return m_ffmpegOpenInputTransportCallEvidenceAvailable; }
        void FfmpegOpenInputTransportCallEvidenceAvailable(bool value) noexcept { m_ffmpegOpenInputTransportCallEvidenceAvailable = value; }
        uint64_t FfmpegOpenInputTransportReadCalls() const noexcept { return m_ffmpegOpenInputTransportReadCalls; }
        void FfmpegOpenInputTransportReadCalls(uint64_t value) noexcept { m_ffmpegOpenInputTransportReadCalls = value; }
        uint64_t FfmpegOpenInputTransportSeekCalls() const noexcept { return m_ffmpegOpenInputTransportSeekCalls; }
        void FfmpegOpenInputTransportSeekCalls(uint64_t value) noexcept { m_ffmpegOpenInputTransportSeekCalls = value; }
        double FfmpegOpenInputTransportReadWaitMs() const noexcept { return m_ffmpegOpenInputTransportReadWaitMs; }
        void FfmpegOpenInputTransportReadWaitMs(double value) noexcept { m_ffmpegOpenInputTransportReadWaitMs = value; }
        double FfmpegOpenInputTransportSeekWaitMs() const noexcept { return m_ffmpegOpenInputTransportSeekWaitMs; }
        void FfmpegOpenInputTransportSeekWaitMs(double value) noexcept { m_ffmpegOpenInputTransportSeekWaitMs = value; }
        uint64_t FfmpegOpenInputTransportSeekDistanceBytes() const noexcept { return m_ffmpegOpenInputTransportSeekDistanceBytes; }
        void FfmpegOpenInputTransportSeekDistanceBytes(uint64_t value) noexcept { m_ffmpegOpenInputTransportSeekDistanceBytes = value; }

        winrt::hstring FfmpegStreamInfoTransportProvider() const { return m_ffmpegStreamInfoTransportProvider; }
        void FfmpegStreamInfoTransportProvider(winrt::hstring const& value) { m_ffmpegStreamInfoTransportProvider = value; }
        bool FfmpegStreamInfoTransportCallEvidenceAvailable() const noexcept { return m_ffmpegStreamInfoTransportCallEvidenceAvailable; }
        void FfmpegStreamInfoTransportCallEvidenceAvailable(bool value) noexcept { m_ffmpegStreamInfoTransportCallEvidenceAvailable = value; }
        uint64_t FfmpegStreamInfoTransportReadCalls() const noexcept { return m_ffmpegStreamInfoTransportReadCalls; }
        void FfmpegStreamInfoTransportReadCalls(uint64_t value) noexcept { m_ffmpegStreamInfoTransportReadCalls = value; }
        uint64_t FfmpegStreamInfoTransportSeekCalls() const noexcept { return m_ffmpegStreamInfoTransportSeekCalls; }
        void FfmpegStreamInfoTransportSeekCalls(uint64_t value) noexcept { m_ffmpegStreamInfoTransportSeekCalls = value; }
        double FfmpegStreamInfoTransportReadWaitMs() const noexcept { return m_ffmpegStreamInfoTransportReadWaitMs; }
        void FfmpegStreamInfoTransportReadWaitMs(double value) noexcept { m_ffmpegStreamInfoTransportReadWaitMs = value; }
        double FfmpegStreamInfoTransportSeekWaitMs() const noexcept { return m_ffmpegStreamInfoTransportSeekWaitMs; }
        void FfmpegStreamInfoTransportSeekWaitMs(double value) noexcept { m_ffmpegStreamInfoTransportSeekWaitMs = value; }
        uint64_t FfmpegStreamInfoTransportSeekDistanceBytes() const noexcept { return m_ffmpegStreamInfoTransportSeekDistanceBytes; }
        void FfmpegStreamInfoTransportSeekDistanceBytes(uint64_t value) noexcept { m_ffmpegStreamInfoTransportSeekDistanceBytes = value; }

        winrt::hstring NativeStartupSeekTransportProvider() const { return m_nativeStartupSeekTransportProvider; }
        void NativeStartupSeekTransportProvider(winrt::hstring const& value) { m_nativeStartupSeekTransportProvider = value; }
        bool NativeStartupSeekTransportCallEvidenceAvailable() const noexcept { return m_nativeStartupSeekTransportCallEvidenceAvailable; }
        void NativeStartupSeekTransportCallEvidenceAvailable(bool value) noexcept { m_nativeStartupSeekTransportCallEvidenceAvailable = value; }
        uint64_t NativeStartupSeekTransportReadCalls() const noexcept { return m_nativeStartupSeekTransportReadCalls; }
        void NativeStartupSeekTransportReadCalls(uint64_t value) noexcept { m_nativeStartupSeekTransportReadCalls = value; }
        uint64_t NativeStartupSeekTransportSeekCalls() const noexcept { return m_nativeStartupSeekTransportSeekCalls; }
        void NativeStartupSeekTransportSeekCalls(uint64_t value) noexcept { m_nativeStartupSeekTransportSeekCalls = value; }
        double NativeStartupSeekTransportReadWaitMs() const noexcept { return m_nativeStartupSeekTransportReadWaitMs; }
        void NativeStartupSeekTransportReadWaitMs(double value) noexcept { m_nativeStartupSeekTransportReadWaitMs = value; }
        double NativeStartupSeekTransportSeekWaitMs() const noexcept { return m_nativeStartupSeekTransportSeekWaitMs; }
        void NativeStartupSeekTransportSeekWaitMs(double value) noexcept { m_nativeStartupSeekTransportSeekWaitMs = value; }
        uint64_t NativeStartupSeekTransportSeekDistanceBytes() const noexcept { return m_nativeStartupSeekTransportSeekDistanceBytes; }
        void NativeStartupSeekTransportSeekDistanceBytes(uint64_t value) noexcept { m_nativeStartupSeekTransportSeekDistanceBytes = value; }

        winrt::hstring NativeFirstFrameTransportProvider() const { return m_nativeFirstFrameTransportProvider; }
        void NativeFirstFrameTransportProvider(winrt::hstring const& value) { m_nativeFirstFrameTransportProvider = value; }
        bool NativeFirstFrameTransportCallEvidenceAvailable() const noexcept { return m_nativeFirstFrameTransportCallEvidenceAvailable; }
        void NativeFirstFrameTransportCallEvidenceAvailable(bool value) noexcept { m_nativeFirstFrameTransportCallEvidenceAvailable = value; }
        uint64_t NativeFirstFrameTransportReadCalls() const noexcept { return m_nativeFirstFrameTransportReadCalls; }
        void NativeFirstFrameTransportReadCalls(uint64_t value) noexcept { m_nativeFirstFrameTransportReadCalls = value; }
        uint64_t NativeFirstFrameTransportSeekCalls() const noexcept { return m_nativeFirstFrameTransportSeekCalls; }
        void NativeFirstFrameTransportSeekCalls(uint64_t value) noexcept { m_nativeFirstFrameTransportSeekCalls = value; }
        double NativeFirstFrameTransportReadWaitMs() const noexcept { return m_nativeFirstFrameTransportReadWaitMs; }
        void NativeFirstFrameTransportReadWaitMs(double value) noexcept { m_nativeFirstFrameTransportReadWaitMs = value; }
        double NativeFirstFrameTransportSeekWaitMs() const noexcept { return m_nativeFirstFrameTransportSeekWaitMs; }
        void NativeFirstFrameTransportSeekWaitMs(double value) noexcept { m_nativeFirstFrameTransportSeekWaitMs = value; }
        uint64_t NativeFirstFrameTransportSeekDistanceBytes() const noexcept { return m_nativeFirstFrameTransportSeekDistanceBytes; }
        void NativeFirstFrameTransportSeekDistanceBytes(uint64_t value) noexcept { m_nativeFirstFrameTransportSeekDistanceBytes = value; }


        double NativeFirstFrameDurationMs() const noexcept { return m_nativeFirstFrameDurationMs; }
        void NativeFirstFrameDurationMs(double value) noexcept { m_nativeFirstFrameDurationMs = value; }

        double NativeFirstFrameDemuxReadDurationMs() const noexcept { return m_nativeFirstFrameDemuxReadDurationMs; }
        void NativeFirstFrameDemuxReadDurationMs(double value) noexcept { m_nativeFirstFrameDemuxReadDurationMs = value; }

        double NativeFirstFramePresentDurationMs() const noexcept { return m_nativeFirstFramePresentDurationMs; }
        void NativeFirstFramePresentDurationMs(double value) noexcept { m_nativeFirstFramePresentDurationMs = value; }

        uint64_t NativeFirstFrameDemuxPacketCount() const noexcept { return m_nativeFirstFrameDemuxPacketCount; }
        void NativeFirstFrameDemuxPacketCount(uint64_t value) noexcept { m_nativeFirstFrameDemuxPacketCount = value; }

        uint64_t NativeFirstFrameDemuxBytes() const noexcept { return m_nativeFirstFrameDemuxBytes; }
        void NativeFirstFrameDemuxBytes(uint64_t value) noexcept { m_nativeFirstFrameDemuxBytes = value; }

        double PlaybackDemuxReadDurationMs() const noexcept { return m_playbackDemuxReadDurationMs; }
        void PlaybackDemuxReadDurationMs(double value) noexcept { m_playbackDemuxReadDurationMs = value; }
        uint64_t PlaybackDemuxPacketCount() const noexcept { return m_playbackDemuxPacketCount; }
        void PlaybackDemuxPacketCount(uint64_t value) noexcept { m_playbackDemuxPacketCount = value; }
        uint64_t PlaybackDemuxBytes() const noexcept { return m_playbackDemuxBytes; }
        void PlaybackDemuxBytes(uint64_t value) noexcept { m_playbackDemuxBytes = value; }
        winrt::hstring PlaybackTransportProvider() const { return m_playbackTransportProvider; }
        void PlaybackTransportProvider(winrt::hstring const& value) { m_playbackTransportProvider = value; }
        bool PlaybackTransportCallEvidenceAvailable() const noexcept { return m_playbackTransportCallEvidenceAvailable; }
        void PlaybackTransportCallEvidenceAvailable(bool value) noexcept { m_playbackTransportCallEvidenceAvailable = value; }
        uint64_t PlaybackTransportReadCalls() const noexcept { return m_playbackTransportReadCalls; }
        void PlaybackTransportReadCalls(uint64_t value) noexcept { m_playbackTransportReadCalls = value; }
        uint64_t PlaybackTransportSeekCalls() const noexcept { return m_playbackTransportSeekCalls; }
        void PlaybackTransportSeekCalls(uint64_t value) noexcept { m_playbackTransportSeekCalls = value; }
        double PlaybackTransportReadWaitMs() const noexcept { return m_playbackTransportReadWaitMs; }
        void PlaybackTransportReadWaitMs(double value) noexcept { m_playbackTransportReadWaitMs = value; }
        double PlaybackTransportSeekWaitMs() const noexcept { return m_playbackTransportSeekWaitMs; }
        void PlaybackTransportSeekWaitMs(double value) noexcept { m_playbackTransportSeekWaitMs = value; }
        uint64_t PlaybackTransportSeekDistanceBytes() const noexcept { return m_playbackTransportSeekDistanceBytes; }
        void PlaybackTransportSeekDistanceBytes(uint64_t value) noexcept { m_playbackTransportSeekDistanceBytes = value; }

        uint64_t ReadErrorCount() const noexcept { return m_readErrorCount; }
        void ReadErrorCount(uint64_t value) noexcept { m_readErrorCount = value; }
        uint64_t ReadRetryCount() const noexcept { return m_readRetryCount; }
        void ReadRetryCount(uint64_t value) noexcept { m_readRetryCount = value; }
        uint64_t ReadRecoveryCount() const noexcept { return m_readRecoveryCount; }
        void ReadRecoveryCount(uint64_t value) noexcept { m_readRecoveryCount = value; }
        uint32_t MaxConsecutiveReadErrors() const noexcept { return m_maxConsecutiveReadErrors; }
        void MaxConsecutiveReadErrors(uint32_t value) noexcept { m_maxConsecutiveReadErrors = value; }
        int32_t LastReadErrorCode() const noexcept { return m_lastReadErrorCode; }
        void LastReadErrorCode(int32_t value) noexcept { m_lastReadErrorCode = value; }
        int32_t FatalReadErrorCode() const noexcept { return m_fatalReadErrorCode; }
        void FatalReadErrorCode(int32_t value) noexcept { m_fatalReadErrorCode = value; }
        double LastReadRecoveryDurationMs() const noexcept { return m_lastReadRecoveryDurationMs; }
        void LastReadRecoveryDurationMs(double value) noexcept { m_lastReadRecoveryDurationMs = value; }

        int64_t ContainerStartTimeTicks() const noexcept { return m_containerStartTimeTicks; }
        void ContainerStartTimeTicks(int64_t value) noexcept { m_containerStartTimeTicks = value; }
        int64_t VideoStreamStartTimeTicks() const noexcept { return m_videoStreamStartTimeTicks; }
        void VideoStreamStartTimeTicks(int64_t value) noexcept { m_videoStreamStartTimeTicks = value; }
        int64_t SeekDemuxTargetTicks() const noexcept { return m_seekDemuxTargetTicks; }
        void SeekDemuxTargetTicks(int64_t value) noexcept { m_seekDemuxTargetTicks = value; }
        int64_t FirstPresentedPositionTicks() const noexcept { return m_firstPresentedPositionTicks; }
        void FirstPresentedPositionTicks(int64_t value) noexcept { m_firstPresentedPositionTicks = value; }
        bool SeekPacketCacheEnabled() const noexcept { return m_seekPacketCacheEnabled; }
        void SeekPacketCacheEnabled(bool value) noexcept { m_seekPacketCacheEnabled = value; }
        bool SeekPacketCacheHit() const noexcept { return m_seekPacketCacheHit; }
        void SeekPacketCacheHit(bool value) noexcept { m_seekPacketCacheHit = value; }
        uint64_t SeekPacketCachePacketCount() const noexcept { return m_seekPacketCachePacketCount; }
        void SeekPacketCachePacketCount(uint64_t value) noexcept { m_seekPacketCachePacketCount = value; }
        uint64_t SeekPacketCacheBytes() const noexcept { return m_seekPacketCacheBytes; }
        void SeekPacketCacheBytes(uint64_t value) noexcept { m_seekPacketCacheBytes = value; }
        int64_t SeekPacketCacheWindowDurationTicks() const noexcept { return m_seekPacketCacheWindowDurationTicks; }
        void SeekPacketCacheWindowDurationTicks(int64_t value) noexcept { m_seekPacketCacheWindowDurationTicks = value; }
        winrt::hstring SeekFallbackReason() const { return m_seekFallbackReason; }
        void SeekFallbackReason(winrt::hstring const& value) { m_seekFallbackReason = value; }

        double RenderIntervalMsP05() const noexcept { return m_renderIntervalMsP05; }
        void RenderIntervalMsP05(double value) noexcept { m_renderIntervalMsP05 = value; }

        double RenderIntervalMsP50() const noexcept { return m_renderIntervalMsP50; }
        void RenderIntervalMsP50(double value) noexcept { m_renderIntervalMsP50 = value; }

        double RenderIntervalMsP95() const noexcept { return m_renderIntervalMsP95; }
        void RenderIntervalMsP95(double value) noexcept { m_renderIntervalMsP95 = value; }

        double RenderIntervalMsP99() const noexcept { return m_renderIntervalMsP99; }
        void RenderIntervalMsP99(double value) noexcept { m_renderIntervalMsP99 = value; }

        double MinFrameGapMs() const noexcept { return m_minFrameGapMs; }
        void MinFrameGapMs(double value) noexcept { m_minFrameGapMs = value; }

        double MaxFrameGapMs() const noexcept { return m_maxFrameGapMs; }
        void MaxFrameGapMs(double value) noexcept { m_maxFrameGapMs = value; }

        uint64_t RenderIntervalSampleCount() const noexcept { return m_renderIntervalSampleCount; }
        void RenderIntervalSampleCount(uint64_t value) noexcept { m_renderIntervalSampleCount = value; }

        uint64_t RenderIntervalOverExpected2MsCount() const noexcept { return m_renderIntervalOverExpected2MsCount; }
        void RenderIntervalOverExpected2MsCount(uint64_t value) noexcept { m_renderIntervalOverExpected2MsCount = value; }

        uint64_t RenderIntervalOverExpected4MsCount() const noexcept { return m_renderIntervalOverExpected4MsCount; }
        void RenderIntervalOverExpected4MsCount(uint64_t value) noexcept { m_renderIntervalOverExpected4MsCount = value; }

        uint64_t RenderIntervalUnderExpected2MsCount() const noexcept { return m_renderIntervalUnderExpected2MsCount; }
        void RenderIntervalUnderExpected2MsCount(uint64_t value) noexcept { m_renderIntervalUnderExpected2MsCount = value; }

        uint64_t RenderIntervalUnderExpected4MsCount() const noexcept { return m_renderIntervalUnderExpected4MsCount; }
        void RenderIntervalUnderExpected4MsCount(uint64_t value) noexcept { m_renderIntervalUnderExpected4MsCount = value; }

        uint64_t RenderIntervalAfterAudioAheadWaitSampleCount() const noexcept { return m_renderIntervalAfterAudioAheadWaitSampleCount; }
        void RenderIntervalAfterAudioAheadWaitSampleCount(uint64_t value) noexcept { m_renderIntervalAfterAudioAheadWaitSampleCount = value; }

        double RenderIntervalAfterAudioAheadWaitMsP95() const noexcept { return m_renderIntervalAfterAudioAheadWaitMsP95; }
        void RenderIntervalAfterAudioAheadWaitMsP95(double value) noexcept { m_renderIntervalAfterAudioAheadWaitMsP95 = value; }

        double RenderIntervalAfterAudioAheadWaitMsP99() const noexcept { return m_renderIntervalAfterAudioAheadWaitMsP99; }
        void RenderIntervalAfterAudioAheadWaitMsP99(double value) noexcept { m_renderIntervalAfterAudioAheadWaitMsP99 = value; }

        double RenderIntervalAfterAudioAheadWaitMsMax() const noexcept { return m_renderIntervalAfterAudioAheadWaitMsMax; }
        void RenderIntervalAfterAudioAheadWaitMsMax(double value) noexcept { m_renderIntervalAfterAudioAheadWaitMsMax = value; }

        uint64_t AudioAheadWaitEndToPresentSampleCount() const noexcept { return m_audioAheadWaitEndToPresentSampleCount; }
        void AudioAheadWaitEndToPresentSampleCount(uint64_t value) noexcept { m_audioAheadWaitEndToPresentSampleCount = value; }

        double AudioAheadWaitEndToPresentMsP50() const noexcept { return m_audioAheadWaitEndToPresentMsP50; }
        void AudioAheadWaitEndToPresentMsP50(double value) noexcept { m_audioAheadWaitEndToPresentMsP50 = value; }

        double AudioAheadWaitEndToPresentMsP95() const noexcept { return m_audioAheadWaitEndToPresentMsP95; }
        void AudioAheadWaitEndToPresentMsP95(double value) noexcept { m_audioAheadWaitEndToPresentMsP95 = value; }

        double AudioAheadWaitEndToPresentMsP99() const noexcept { return m_audioAheadWaitEndToPresentMsP99; }
        void AudioAheadWaitEndToPresentMsP99(double value) noexcept { m_audioAheadWaitEndToPresentMsP99 = value; }

        double AudioAheadWaitEndToPresentMsMax() const noexcept { return m_audioAheadWaitEndToPresentMsMax; }
        void AudioAheadWaitEndToPresentMsMax(double value) noexcept { m_audioAheadWaitEndToPresentMsMax = value; }

        uint64_t RenderIntervalAfterNonAudioWaitSampleCount() const noexcept { return m_renderIntervalAfterNonAudioWaitSampleCount; }
        void RenderIntervalAfterNonAudioWaitSampleCount(uint64_t value) noexcept { m_renderIntervalAfterNonAudioWaitSampleCount = value; }

        double RenderIntervalAfterNonAudioWaitMsP95() const noexcept { return m_renderIntervalAfterNonAudioWaitMsP95; }
        void RenderIntervalAfterNonAudioWaitMsP95(double value) noexcept { m_renderIntervalAfterNonAudioWaitMsP95 = value; }

        double RenderIntervalAfterNonAudioWaitMsP99() const noexcept { return m_renderIntervalAfterNonAudioWaitMsP99; }
        void RenderIntervalAfterNonAudioWaitMsP99(double value) noexcept { m_renderIntervalAfterNonAudioWaitMsP99 = value; }

        double RenderIntervalAfterNonAudioWaitMsMax() const noexcept { return m_renderIntervalAfterNonAudioWaitMsMax; }
        void RenderIntervalAfterNonAudioWaitMsMax(double value) noexcept { m_renderIntervalAfterNonAudioWaitMsMax = value; }

        double PresentDurationMsP50() const noexcept { return m_presentDurationMsP50; }
        void PresentDurationMsP50(double value) noexcept { m_presentDurationMsP50 = value; }

        double PresentDurationMsP95() const noexcept { return m_presentDurationMsP95; }
        void PresentDurationMsP95(double value) noexcept { m_presentDurationMsP95 = value; }

        double PresentDurationMsP99() const noexcept { return m_presentDurationMsP99; }
        void PresentDurationMsP99(double value) noexcept { m_presentDurationMsP99 = value; }

        double PresentDurationMsMax() const noexcept { return m_presentDurationMsMax; }
        void PresentDurationMsMax(double value) noexcept { m_presentDurationMsMax = value; }

        double VideoDecodeDurationMsP50() const noexcept { return m_videoDecodeDurationMsP50; }
        void VideoDecodeDurationMsP50(double value) noexcept { m_videoDecodeDurationMsP50 = value; }

        double VideoDecodeDurationMsP95() const noexcept { return m_videoDecodeDurationMsP95; }
        void VideoDecodeDurationMsP95(double value) noexcept { m_videoDecodeDurationMsP95 = value; }

        double VideoDecodeDurationMsP99() const noexcept { return m_videoDecodeDurationMsP99; }
        void VideoDecodeDurationMsP99(double value) noexcept { m_videoDecodeDurationMsP99 = value; }

        double VideoDecodeDurationMsMax() const noexcept { return m_videoDecodeDurationMsMax; }
        void VideoDecodeDurationMsMax(double value) noexcept { m_videoDecodeDurationMsMax = value; }

        winrt::hstring VideoDecodeDeviceMode() const { return m_videoDecodeDeviceMode; }
        void VideoDecodeDeviceMode(winrt::hstring const& value) { m_videoDecodeDeviceMode = value; }
        winrt::hstring VideoDecodeSynchronizationMode() const { return m_videoDecodeSynchronizationMode; }
        void VideoDecodeSynchronizationMode(winrt::hstring const& value) { m_videoDecodeSynchronizationMode = value; }
        bool VideoDecodeWorkerActive() const noexcept { return m_videoDecodeWorkerActive; }
        void VideoDecodeWorkerActive(bool value) noexcept { m_videoDecodeWorkerActive = value; }
        uint64_t VideoDecodeQueueCapacity() const noexcept { return m_videoDecodeQueueCapacity; }
        void VideoDecodeQueueCapacity(uint64_t value) noexcept { m_videoDecodeQueueCapacity = value; }
        uint64_t VideoDecodeQueueMaxDepth() const noexcept { return m_videoDecodeQueueMaxDepth; }
        void VideoDecodeQueueMaxDepth(uint64_t value) noexcept { m_videoDecodeQueueMaxDepth = value; }
        uint64_t VideoDecodeQueueProducerWaitCount() const noexcept { return m_videoDecodeQueueProducerWaitCount; }
        void VideoDecodeQueueProducerWaitCount(uint64_t value) noexcept { m_videoDecodeQueueProducerWaitCount = value; }

        double VideoDecodePacketReadDurationMsP50() const noexcept { return m_videoDecodePacketReadDurationMsP50; }
        void VideoDecodePacketReadDurationMsP50(double value) noexcept { m_videoDecodePacketReadDurationMsP50 = value; }
        double VideoDecodePacketReadDurationMsP95() const noexcept { return m_videoDecodePacketReadDurationMsP95; }
        void VideoDecodePacketReadDurationMsP95(double value) noexcept { m_videoDecodePacketReadDurationMsP95 = value; }
        double VideoDecodeSendPacketDurationMsP50() const noexcept { return m_videoDecodeSendPacketDurationMsP50; }
        void VideoDecodeSendPacketDurationMsP50(double value) noexcept { m_videoDecodeSendPacketDurationMsP50 = value; }
        double VideoDecodeSendPacketDurationMsP95() const noexcept { return m_videoDecodeSendPacketDurationMsP95; }
        void VideoDecodeSendPacketDurationMsP95(double value) noexcept { m_videoDecodeSendPacketDurationMsP95 = value; }
        double VideoDecodeReceiveFrameDurationMsP50() const noexcept { return m_videoDecodeReceiveFrameDurationMsP50; }
        void VideoDecodeReceiveFrameDurationMsP50(double value) noexcept { m_videoDecodeReceiveFrameDurationMsP50 = value; }
        double VideoDecodeReceiveFrameDurationMsP95() const noexcept { return m_videoDecodeReceiveFrameDurationMsP95; }
        void VideoDecodeReceiveFrameDurationMsP95(double value) noexcept { m_videoDecodeReceiveFrameDurationMsP95 = value; }
        double VideoDecodeFrameMaterializeDurationMsP50() const noexcept { return m_videoDecodeFrameMaterializeDurationMsP50; }
        void VideoDecodeFrameMaterializeDurationMsP50(double value) noexcept { m_videoDecodeFrameMaterializeDurationMsP50 = value; }
        double VideoDecodeFrameMaterializeDurationMsP95() const noexcept { return m_videoDecodeFrameMaterializeDurationMsP95; }
        void VideoDecodeFrameMaterializeDurationMsP95(double value) noexcept { m_videoDecodeFrameMaterializeDurationMsP95 = value; }

        double VideoRenderDurationMsP50() const noexcept { return m_videoRenderDurationMsP50; }
        void VideoRenderDurationMsP50(double value) noexcept { m_videoRenderDurationMsP50 = value; }

        double VideoRenderDurationMsP95() const noexcept { return m_videoRenderDurationMsP95; }
        void VideoRenderDurationMsP95(double value) noexcept { m_videoRenderDurationMsP95 = value; }

        double VideoRenderDurationMsP99() const noexcept { return m_videoRenderDurationMsP99; }
        void VideoRenderDurationMsP99(double value) noexcept { m_videoRenderDurationMsP99 = value; }

        double VideoRenderDurationMsMax() const noexcept { return m_videoRenderDurationMsMax; }
        void VideoRenderDurationMsMax(double value) noexcept { m_videoRenderDurationMsMax = value; }

        double AudioAheadWaitDurationMsP50() const noexcept { return m_audioAheadWaitDurationMsP50; }
        void AudioAheadWaitDurationMsP50(double value) noexcept { m_audioAheadWaitDurationMsP50 = value; }

        double AudioAheadWaitDurationMsP95() const noexcept { return m_audioAheadWaitDurationMsP95; }
        void AudioAheadWaitDurationMsP95(double value) noexcept { m_audioAheadWaitDurationMsP95 = value; }

        double AudioAheadWaitDurationMsP99() const noexcept { return m_audioAheadWaitDurationMsP99; }
        void AudioAheadWaitDurationMsP99(double value) noexcept { m_audioAheadWaitDurationMsP99 = value; }

        double AudioAheadWaitDurationMsMax() const noexcept { return m_audioAheadWaitDurationMsMax; }
        void AudioAheadWaitDurationMsMax(double value) noexcept { m_audioAheadWaitDurationMsMax = value; }

        double AudioAheadWaitTargetMsP50() const noexcept { return m_audioAheadWaitTargetMsP50; }
        void AudioAheadWaitTargetMsP50(double value) noexcept { m_audioAheadWaitTargetMsP50 = value; }

        double AudioAheadWaitTargetMsP95() const noexcept { return m_audioAheadWaitTargetMsP95; }
        void AudioAheadWaitTargetMsP95(double value) noexcept { m_audioAheadWaitTargetMsP95 = value; }

        double AudioAheadWaitTargetMsP99() const noexcept { return m_audioAheadWaitTargetMsP99; }
        void AudioAheadWaitTargetMsP99(double value) noexcept { m_audioAheadWaitTargetMsP99 = value; }

        double AudioAheadWaitTargetMsMax() const noexcept { return m_audioAheadWaitTargetMsMax; }
        void AudioAheadWaitTargetMsMax(double value) noexcept { m_audioAheadWaitTargetMsMax = value; }

        double AudioAheadWaitOversleepMsP50() const noexcept { return m_audioAheadWaitOversleepMsP50; }
        void AudioAheadWaitOversleepMsP50(double value) noexcept { m_audioAheadWaitOversleepMsP50 = value; }

        double AudioAheadWaitOversleepMsP95() const noexcept { return m_audioAheadWaitOversleepMsP95; }
        void AudioAheadWaitOversleepMsP95(double value) noexcept { m_audioAheadWaitOversleepMsP95 = value; }

        double AudioAheadWaitOversleepMsP99() const noexcept { return m_audioAheadWaitOversleepMsP99; }
        void AudioAheadWaitOversleepMsP99(double value) noexcept { m_audioAheadWaitOversleepMsP99 = value; }

        double AudioAheadWaitOversleepMsMax() const noexcept { return m_audioAheadWaitOversleepMsMax; }
        void AudioAheadWaitOversleepMsMax(double value) noexcept { m_audioAheadWaitOversleepMsMax = value; }

        double AudioAheadWaitFinalDeltaAbsMsP50() const noexcept { return m_audioAheadWaitFinalDeltaAbsMsP50; }
        void AudioAheadWaitFinalDeltaAbsMsP50(double value) noexcept { m_audioAheadWaitFinalDeltaAbsMsP50 = value; }

        double AudioAheadWaitFinalDeltaAbsMsP95() const noexcept { return m_audioAheadWaitFinalDeltaAbsMsP95; }
        void AudioAheadWaitFinalDeltaAbsMsP95(double value) noexcept { m_audioAheadWaitFinalDeltaAbsMsP95 = value; }

        double AudioAheadWaitFinalDeltaAbsMsP99() const noexcept { return m_audioAheadWaitFinalDeltaAbsMsP99; }
        void AudioAheadWaitFinalDeltaAbsMsP99(double value) noexcept { m_audioAheadWaitFinalDeltaAbsMsP99 = value; }

        double AudioAheadWaitFinalDeltaAbsMsMax() const noexcept { return m_audioAheadWaitFinalDeltaAbsMsMax; }
        void AudioAheadWaitFinalDeltaAbsMsMax(double value) noexcept { m_audioAheadWaitFinalDeltaAbsMsMax = value; }

        uint64_t AudioAheadWaitEpisodeCount() const noexcept { return m_audioAheadWaitEpisodeCount; }
        void AudioAheadWaitEpisodeCount(uint64_t value) noexcept { m_audioAheadWaitEpisodeCount = value; }

        double AudioAheadWaitPassesPerEpisodeP50() const noexcept { return m_audioAheadWaitPassesPerEpisodeP50; }
        void AudioAheadWaitPassesPerEpisodeP50(double value) noexcept { m_audioAheadWaitPassesPerEpisodeP50 = value; }

        double AudioAheadWaitPassesPerEpisodeP95() const noexcept { return m_audioAheadWaitPassesPerEpisodeP95; }
        void AudioAheadWaitPassesPerEpisodeP95(double value) noexcept { m_audioAheadWaitPassesPerEpisodeP95 = value; }

        double AudioAheadWaitPassesPerEpisodeP99() const noexcept { return m_audioAheadWaitPassesPerEpisodeP99; }
        void AudioAheadWaitPassesPerEpisodeP99(double value) noexcept { m_audioAheadWaitPassesPerEpisodeP99 = value; }

        double AudioAheadWaitPassesPerEpisodeMax() const noexcept { return m_audioAheadWaitPassesPerEpisodeMax; }
        void AudioAheadWaitPassesPerEpisodeMax(double value) noexcept { m_audioAheadWaitPassesPerEpisodeMax = value; }

        double AudioAheadWaitPassDurationMsP50() const noexcept { return m_audioAheadWaitPassDurationMsP50; }
        void AudioAheadWaitPassDurationMsP50(double value) noexcept { m_audioAheadWaitPassDurationMsP50 = value; }

        double AudioAheadWaitPassDurationMsP95() const noexcept { return m_audioAheadWaitPassDurationMsP95; }
        void AudioAheadWaitPassDurationMsP95(double value) noexcept { m_audioAheadWaitPassDurationMsP95 = value; }

        double AudioAheadWaitPassDurationMsP99() const noexcept { return m_audioAheadWaitPassDurationMsP99; }
        void AudioAheadWaitPassDurationMsP99(double value) noexcept { m_audioAheadWaitPassDurationMsP99 = value; }

        double AudioAheadWaitPassDurationMsMax() const noexcept { return m_audioAheadWaitPassDurationMsMax; }
        void AudioAheadWaitPassDurationMsMax(double value) noexcept { m_audioAheadWaitPassDurationMsMax = value; }

        double AudioAheadWaitPassTargetMsP50() const noexcept { return m_audioAheadWaitPassTargetMsP50; }
        void AudioAheadWaitPassTargetMsP50(double value) noexcept { m_audioAheadWaitPassTargetMsP50 = value; }

        double AudioAheadWaitPassTargetMsP95() const noexcept { return m_audioAheadWaitPassTargetMsP95; }
        void AudioAheadWaitPassTargetMsP95(double value) noexcept { m_audioAheadWaitPassTargetMsP95 = value; }

        double AudioAheadWaitPassTargetMsP99() const noexcept { return m_audioAheadWaitPassTargetMsP99; }
        void AudioAheadWaitPassTargetMsP99(double value) noexcept { m_audioAheadWaitPassTargetMsP99 = value; }

        double AudioAheadWaitPassTargetMsMax() const noexcept { return m_audioAheadWaitPassTargetMsMax; }
        void AudioAheadWaitPassTargetMsMax(double value) noexcept { m_audioAheadWaitPassTargetMsMax = value; }

        double AudioAheadWaitPassOversleepMsP50() const noexcept { return m_audioAheadWaitPassOversleepMsP50; }
        void AudioAheadWaitPassOversleepMsP50(double value) noexcept { m_audioAheadWaitPassOversleepMsP50 = value; }

        double AudioAheadWaitPassOversleepMsP95() const noexcept { return m_audioAheadWaitPassOversleepMsP95; }
        void AudioAheadWaitPassOversleepMsP95(double value) noexcept { m_audioAheadWaitPassOversleepMsP95 = value; }

        double AudioAheadWaitPassOversleepMsP99() const noexcept { return m_audioAheadWaitPassOversleepMsP99; }
        void AudioAheadWaitPassOversleepMsP99(double value) noexcept { m_audioAheadWaitPassOversleepMsP99 = value; }

        double AudioAheadWaitPassOversleepMsMax() const noexcept { return m_audioAheadWaitPassOversleepMsMax; }
        void AudioAheadWaitPassOversleepMsMax(double value) noexcept { m_audioAheadWaitPassOversleepMsMax = value; }

        double FramePacingSourceFrameRate() const noexcept { return m_framePacingSourceFrameRate; }
        void FramePacingSourceFrameRate(double value) noexcept { m_framePacingSourceFrameRate = value; }

        double LateFrameDropToleranceMs() const noexcept { return m_lateFrameDropToleranceMs; }
        void LateFrameDropToleranceMs(double value) noexcept { m_lateFrameDropToleranceMs = value; }

        double AudioVideoDriftMsP50() const noexcept { return m_audioVideoDriftMsP50; }
        void AudioVideoDriftMsP50(double value) noexcept { m_audioVideoDriftMsP50 = value; }

        double AudioVideoDriftMsP95() const noexcept { return m_audioVideoDriftMsP95; }
        void AudioVideoDriftMsP95(double value) noexcept { m_audioVideoDriftMsP95 = value; }

        double AudioVideoDriftMsP99() const noexcept { return m_audioVideoDriftMsP99; }
        void AudioVideoDriftMsP99(double value) noexcept { m_audioVideoDriftMsP99 = value; }

        double AudioVideoDriftMsMax() const noexcept { return m_audioVideoDriftMsMax; }
        void AudioVideoDriftMsMax(double value) noexcept { m_audioVideoDriftMsMax = value; }

        winrt::hstring LastInteractionScenario() const { return m_lastInteractionScenario; }
        void LastInteractionScenario(winrt::hstring const& value) { m_lastInteractionScenario = value; }
        uint64_t LastInteractionSequence() const noexcept { return m_lastInteractionSequence; }
        void LastInteractionSequence(uint64_t value) noexcept { m_lastInteractionSequence = value; }
        double LastInteractionLockWaitDurationMs() const noexcept { return m_lastInteractionLockWaitDurationMs; }
        void LastInteractionLockWaitDurationMs(double value) noexcept { m_lastInteractionLockWaitDurationMs = value; }
        double LastInteractionExecutionDurationMs() const noexcept { return m_lastInteractionExecutionDurationMs; }
        void LastInteractionExecutionDurationMs(double value) noexcept { m_lastInteractionExecutionDurationMs = value; }
        double LastInteractionQuiesceDurationMs() const noexcept { return m_lastInteractionQuiesceDurationMs; }
        void LastInteractionQuiesceDurationMs(double value) noexcept { m_lastInteractionQuiesceDurationMs = value; }
        double LastInteractionSeekDurationMs() const noexcept { return m_lastInteractionSeekDurationMs; }
        void LastInteractionSeekDurationMs(double value) noexcept { m_lastInteractionSeekDurationMs = value; }
        double LastInteractionDecoderOpenDurationMs() const noexcept { return m_lastInteractionDecoderOpenDurationMs; }
        void LastInteractionDecoderOpenDurationMs(double value) noexcept { m_lastInteractionDecoderOpenDurationMs = value; }
        double LastInteractionRendererOpenDurationMs() const noexcept { return m_lastInteractionRendererOpenDurationMs; }
        void LastInteractionRendererOpenDurationMs(double value) noexcept { m_lastInteractionRendererOpenDurationMs = value; }
        bool LastInteractionPacketCacheHit() const noexcept { return m_lastInteractionPacketCacheHit; }
        void LastInteractionPacketCacheHit(bool value) noexcept { m_lastInteractionPacketCacheHit = value; }
        bool LastInteractionPacketCacheEnabled() const noexcept { return m_lastInteractionPacketCacheEnabled; }
        void LastInteractionPacketCacheEnabled(bool value) noexcept { m_lastInteractionPacketCacheEnabled = value; }
        uint64_t LastInteractionPacketCachePacketCount() const noexcept { return m_lastInteractionPacketCachePacketCount; }
        void LastInteractionPacketCachePacketCount(uint64_t value) noexcept { m_lastInteractionPacketCachePacketCount = value; }
        uint64_t LastInteractionPacketCacheBytes() const noexcept { return m_lastInteractionPacketCacheBytes; }
        void LastInteractionPacketCacheBytes(uint64_t value) noexcept { m_lastInteractionPacketCacheBytes = value; }
        int64_t LastInteractionPacketCacheWindowDurationTicks() const noexcept { return m_lastInteractionPacketCacheWindowDurationTicks; }
        void LastInteractionPacketCacheWindowDurationTicks(int64_t value) noexcept { m_lastInteractionPacketCacheWindowDurationTicks = value; }

    private:
        uint64_t m_renderPasses{0};
        uint64_t m_decodedVideoFrames{0};
        uint64_t m_hardwareDecodedVideoFrames{0};
        uint64_t m_softwareDecodedVideoFrames{0};
        uint64_t m_renderedVideoFrames{0};
        uint64_t m_submittedAudioFrames{0};
        int32_t m_selectedAudioStreamIndex{-1};
        uint64_t m_subtitleDecodedCueCount{0};
        uint64_t m_subtitleCueRenderCount{0};
        int32_t m_selectedSubtitleStreamIndex{-1};
        uint64_t m_droppedVideoFrames{0};
        uint64_t m_seekPrerollDroppedFrames{0};
        uint64_t m_videoAheadWaitCount{0};
        uint64_t m_audioAheadWaitCount{0};
        uint64_t m_videoClockWaitCount{0};
        uint64_t m_videoStarvedPasses{0};
        uint64_t m_audioStarvedPasses{0};
        uint64_t m_queuedAudioBuffers{0};
        int64_t m_audioClockTicks{0};
        int64_t m_videoPositionTicks{0};
        double m_nativeGraphOpenDurationMs{0.0};
        double m_ffmpegOpenInputDurationMs{0.0};
        double m_ffmpegStreamInfoDurationMs{0.0};
        double m_nativeStartupSeekDurationMs{0.0};
        uint64_t m_ffmpegOpenInputBytesRead{0};
        uint64_t m_ffmpegStreamInfoBytesRead{0};
        uint64_t m_nativeStartupSeekBytesRead{0};
        uint64_t m_nativeFirstFrameTransportBytesRead{0};
        winrt::hstring m_startupTransportProvider{L"ffmpeg-builtin"};
        bool m_startupTransportCallEvidenceAvailable{false};
        winrt::hstring m_ffmpegOpenInputTransportProvider{L"ffmpeg-builtin"};
        bool m_ffmpegOpenInputTransportCallEvidenceAvailable{false};
        uint64_t m_ffmpegOpenInputTransportReadCalls{0};
        uint64_t m_ffmpegOpenInputTransportSeekCalls{0};
        double m_ffmpegOpenInputTransportReadWaitMs{0.0};
        double m_ffmpegOpenInputTransportSeekWaitMs{0.0};
        uint64_t m_ffmpegOpenInputTransportSeekDistanceBytes{0};
        winrt::hstring m_ffmpegStreamInfoTransportProvider{L"ffmpeg-builtin"};
        bool m_ffmpegStreamInfoTransportCallEvidenceAvailable{false};
        uint64_t m_ffmpegStreamInfoTransportReadCalls{0};
        uint64_t m_ffmpegStreamInfoTransportSeekCalls{0};
        double m_ffmpegStreamInfoTransportReadWaitMs{0.0};
        double m_ffmpegStreamInfoTransportSeekWaitMs{0.0};
        uint64_t m_ffmpegStreamInfoTransportSeekDistanceBytes{0};
        winrt::hstring m_nativeStartupSeekTransportProvider{L"ffmpeg-builtin"};
        bool m_nativeStartupSeekTransportCallEvidenceAvailable{false};
        uint64_t m_nativeStartupSeekTransportReadCalls{0};
        uint64_t m_nativeStartupSeekTransportSeekCalls{0};
        double m_nativeStartupSeekTransportReadWaitMs{0.0};
        double m_nativeStartupSeekTransportSeekWaitMs{0.0};
        uint64_t m_nativeStartupSeekTransportSeekDistanceBytes{0};
        winrt::hstring m_nativeFirstFrameTransportProvider{L"ffmpeg-builtin"};
        bool m_nativeFirstFrameTransportCallEvidenceAvailable{false};
        uint64_t m_nativeFirstFrameTransportReadCalls{0};
        uint64_t m_nativeFirstFrameTransportSeekCalls{0};
        double m_nativeFirstFrameTransportReadWaitMs{0.0};
        double m_nativeFirstFrameTransportSeekWaitMs{0.0};
        uint64_t m_nativeFirstFrameTransportSeekDistanceBytes{0};
        double m_nativeFirstFrameDurationMs{0.0};
        double m_nativeFirstFrameDemuxReadDurationMs{0.0};
        double m_nativeFirstFramePresentDurationMs{0.0};
        uint64_t m_nativeFirstFrameDemuxPacketCount{0};
        uint64_t m_nativeFirstFrameDemuxBytes{0};
        double m_playbackDemuxReadDurationMs{0.0};
        uint64_t m_playbackDemuxPacketCount{0};
        uint64_t m_playbackDemuxBytes{0};
        winrt::hstring m_playbackTransportProvider{L"ffmpeg-builtin"};
        bool m_playbackTransportCallEvidenceAvailable{false};
        uint64_t m_playbackTransportReadCalls{0};
        uint64_t m_playbackTransportSeekCalls{0};
        double m_playbackTransportReadWaitMs{0.0};
        double m_playbackTransportSeekWaitMs{0.0};
        uint64_t m_playbackTransportSeekDistanceBytes{0};
        uint64_t m_readErrorCount{0};
        uint64_t m_readRetryCount{0};
        uint64_t m_readRecoveryCount{0};
        uint32_t m_maxConsecutiveReadErrors{0};
        int32_t m_lastReadErrorCode{0};
        int32_t m_fatalReadErrorCode{0};
        double m_lastReadRecoveryDurationMs{0.0};
        int64_t m_containerStartTimeTicks{0};
        int64_t m_videoStreamStartTimeTicks{0};
        int64_t m_seekDemuxTargetTicks{-1};
        int64_t m_firstPresentedPositionTicks{-1};
        bool m_seekPacketCacheEnabled{false};
        bool m_seekPacketCacheHit{false};
        uint64_t m_seekPacketCachePacketCount{0};
        uint64_t m_seekPacketCacheBytes{0};
        int64_t m_seekPacketCacheWindowDurationTicks{0};
        winrt::hstring m_seekFallbackReason;
        double m_renderIntervalMsP05{0.0};
        double m_renderIntervalMsP50{0.0};
        double m_renderIntervalMsP95{0.0};
        double m_renderIntervalMsP99{0.0};
        double m_minFrameGapMs{0.0};
        double m_maxFrameGapMs{0.0};
        uint64_t m_renderIntervalSampleCount{0};
        uint64_t m_renderIntervalOverExpected2MsCount{0};
        uint64_t m_renderIntervalOverExpected4MsCount{0};
        uint64_t m_renderIntervalUnderExpected2MsCount{0};
        uint64_t m_renderIntervalUnderExpected4MsCount{0};
        uint64_t m_renderIntervalAfterAudioAheadWaitSampleCount{0};
        double m_renderIntervalAfterAudioAheadWaitMsP95{0.0};
        double m_renderIntervalAfterAudioAheadWaitMsP99{0.0};
        double m_renderIntervalAfterAudioAheadWaitMsMax{0.0};
        uint64_t m_audioAheadWaitEndToPresentSampleCount{0};
        double m_audioAheadWaitEndToPresentMsP50{0.0};
        double m_audioAheadWaitEndToPresentMsP95{0.0};
        double m_audioAheadWaitEndToPresentMsP99{0.0};
        double m_audioAheadWaitEndToPresentMsMax{0.0};
        uint64_t m_renderIntervalAfterNonAudioWaitSampleCount{0};
        double m_renderIntervalAfterNonAudioWaitMsP95{0.0};
        double m_renderIntervalAfterNonAudioWaitMsP99{0.0};
        double m_renderIntervalAfterNonAudioWaitMsMax{0.0};
        double m_presentDurationMsP50{0.0};
        double m_presentDurationMsP95{0.0};
        double m_presentDurationMsP99{0.0};
        double m_presentDurationMsMax{0.0};
        double m_videoDecodeDurationMsP50{0.0};
        double m_videoDecodeDurationMsP95{0.0};
        double m_videoDecodeDurationMsP99{0.0};
        double m_videoDecodeDurationMsMax{0.0};
        winrt::hstring m_videoDecodeDeviceMode{L"unknown"};
        winrt::hstring m_videoDecodeSynchronizationMode{L"none"};
        bool m_videoDecodeWorkerActive{false};
        uint64_t m_videoDecodeQueueCapacity{0};
        uint64_t m_videoDecodeQueueMaxDepth{0};
        uint64_t m_videoDecodeQueueProducerWaitCount{0};
        double m_videoDecodePacketReadDurationMsP50{0.0};
        double m_videoDecodePacketReadDurationMsP95{0.0};
        double m_videoDecodeSendPacketDurationMsP50{0.0};
        double m_videoDecodeSendPacketDurationMsP95{0.0};
        double m_videoDecodeReceiveFrameDurationMsP50{0.0};
        double m_videoDecodeReceiveFrameDurationMsP95{0.0};
        double m_videoDecodeFrameMaterializeDurationMsP50{0.0};
        double m_videoDecodeFrameMaterializeDurationMsP95{0.0};
        double m_videoRenderDurationMsP50{0.0};
        double m_videoRenderDurationMsP95{0.0};
        double m_videoRenderDurationMsP99{0.0};
        double m_videoRenderDurationMsMax{0.0};
        double m_audioAheadWaitDurationMsP50{0.0};
        double m_audioAheadWaitDurationMsP95{0.0};
        double m_audioAheadWaitDurationMsP99{0.0};
        double m_audioAheadWaitDurationMsMax{0.0};
        double m_audioAheadWaitTargetMsP50{0.0};
        double m_audioAheadWaitTargetMsP95{0.0};
        double m_audioAheadWaitTargetMsP99{0.0};
        double m_audioAheadWaitTargetMsMax{0.0};
        double m_audioAheadWaitOversleepMsP50{0.0};
        double m_audioAheadWaitOversleepMsP95{0.0};
        double m_audioAheadWaitOversleepMsP99{0.0};
        double m_audioAheadWaitOversleepMsMax{0.0};
        double m_audioAheadWaitFinalDeltaAbsMsP50{0.0};
        double m_audioAheadWaitFinalDeltaAbsMsP95{0.0};
        double m_audioAheadWaitFinalDeltaAbsMsP99{0.0};
        double m_audioAheadWaitFinalDeltaAbsMsMax{0.0};
        uint64_t m_audioAheadWaitEpisodeCount{0};
        double m_audioAheadWaitPassesPerEpisodeP50{0.0};
        double m_audioAheadWaitPassesPerEpisodeP95{0.0};
        double m_audioAheadWaitPassesPerEpisodeP99{0.0};
        double m_audioAheadWaitPassesPerEpisodeMax{0.0};
        double m_audioAheadWaitPassDurationMsP50{0.0};
        double m_audioAheadWaitPassDurationMsP95{0.0};
        double m_audioAheadWaitPassDurationMsP99{0.0};
        double m_audioAheadWaitPassDurationMsMax{0.0};
        double m_audioAheadWaitPassTargetMsP50{0.0};
        double m_audioAheadWaitPassTargetMsP95{0.0};
        double m_audioAheadWaitPassTargetMsP99{0.0};
        double m_audioAheadWaitPassTargetMsMax{0.0};
        double m_audioAheadWaitPassOversleepMsP50{0.0};
        double m_audioAheadWaitPassOversleepMsP95{0.0};
        double m_audioAheadWaitPassOversleepMsP99{0.0};
        double m_audioAheadWaitPassOversleepMsMax{0.0};
        double m_framePacingSourceFrameRate{0.0};
        double m_lateFrameDropToleranceMs{0.0};
        double m_audioVideoDriftMsP50{0.0};
        double m_audioVideoDriftMsP95{0.0};
        double m_audioVideoDriftMsP99{0.0};
        double m_audioVideoDriftMsMax{0.0};
        winrt::hstring m_lastInteractionScenario;
        uint64_t m_lastInteractionSequence{0};
        double m_lastInteractionLockWaitDurationMs{0.0};
        double m_lastInteractionExecutionDurationMs{0.0};
        double m_lastInteractionQuiesceDurationMs{0.0};
        double m_lastInteractionSeekDurationMs{0.0};
        double m_lastInteractionDecoderOpenDurationMs{0.0};
        double m_lastInteractionRendererOpenDurationMs{0.0};
        bool m_lastInteractionPacketCacheHit{false};
        bool m_lastInteractionPacketCacheEnabled{false};
        uint64_t m_lastInteractionPacketCachePacketCount{0};
        uint64_t m_lastInteractionPacketCacheBytes{0};
        int64_t m_lastInteractionPacketCacheWindowDurationTicks{0};
    };
}

namespace winrt::NoiraPlayer::Native::factory_implementation
{
    struct NativePlaybackQualityMetrics :
        NativePlaybackQualityMetricsT<NativePlaybackQualityMetrics, implementation::NativePlaybackQualityMetrics>
    {
    };
}
