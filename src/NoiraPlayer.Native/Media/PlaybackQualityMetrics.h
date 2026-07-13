#pragma once

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstddef>
#include <cmath>
#include <chrono>
#include <optional>
#include <string>

namespace winrt::NoiraPlayer::Native::implementation
{
    struct PlaybackTransportCallMetrics
    {
        uint64_t ReadCalls{0};
        uint64_t SeekCalls{0};
        double ReadWaitMs{0.0};
        double SeekWaitMs{0.0};
        uint64_t SeekDistanceBytes{0};
        uint64_t SizeQueryCalls{0};
        uint64_t DataSeekCalls{0};
        uint64_t ForwardDataSeekCalls{0};
        uint64_t BackwardDataSeekCalls{0};
        uint64_t NoOpDataSeekCalls{0};
        double SizeQueryWaitMs{0.0};
        double DataSeekWaitMs{0.0};
        double ForwardDataSeekWaitMs{0.0};
        double BackwardDataSeekWaitMs{0.0};
        double NoOpDataSeekWaitMs{0.0};
        uint64_t DataSeekDistanceBytes{0};
        uint64_t ForwardDataSeekDistanceBytes{0};
        uint64_t BackwardDataSeekDistanceBytes{0};
        std::string Provider{"ffmpeg-builtin"};
        bool EvidenceAvailable{false};
    };

    class PlaybackPresentationIntervalTracker
    {
    public:
        std::optional<double> Observe(std::chrono::steady_clock::time_point presentedAt) noexcept
        {
            if (!m_lastPresentedAt)
            {
                m_lastPresentedAt = presentedAt;
                return std::nullopt;
            }

            auto intervalMs = std::chrono::duration<double, std::milli>(
                presentedAt - *m_lastPresentedAt).count();
            m_lastPresentedAt = presentedAt;
            return intervalMs;
        }

        void BreakContinuity() noexcept
        {
            m_lastPresentedAt.reset();
        }

    private:
        std::optional<std::chrono::steady_clock::time_point> m_lastPresentedAt;
    };

    struct PlaybackQualityMetricsSnapshot
    {
        uint64_t RenderPasses{0};
        uint64_t DecodedVideoFrames{0};
        uint64_t HardwareDecodedVideoFrames{0};
        uint64_t SoftwareDecodedVideoFrames{0};
        uint64_t RenderedVideoFrames{0};
        uint64_t SubmittedAudioFrames{0};
        uint64_t DroppedVideoFrames{0};
        uint64_t SeekPrerollDroppedFrames{0};
        uint64_t VideoAheadWaitCount{0};
        uint64_t AudioAheadWaitCount{0};
        uint64_t VideoClockWaitCount{0};
        uint64_t VideoStarvedPasses{0};
        uint64_t AudioStarvedPasses{0};
        uint64_t QueuedAudioBuffers{0};
        int64_t AudioClockTicks{0};
        int64_t VideoPositionTicks{0};
        double NativeGraphOpenDurationMs{0.0};
        double FfmpegOpenInputDurationMs{0.0};
        double FfmpegStreamInfoDurationMs{0.0};
        double NativeStartupSeekDurationMs{0.0};
        uint64_t FfmpegOpenInputBytesRead{0};
        uint64_t FfmpegStreamInfoBytesRead{0};
        uint64_t NativeStartupSeekBytesRead{0};
        uint64_t NativeFirstFrameTransportBytesRead{0};
        std::string StartupTransportProvider{"ffmpeg-builtin"};
        bool StartupTransportCallEvidenceAvailable{false};
        PlaybackTransportCallMetrics FfmpegOpenInputTransportCalls;
        PlaybackTransportCallMetrics FfmpegStreamInfoTransportCalls;
        PlaybackTransportCallMetrics NativeStartupSeekTransportCalls;
        PlaybackTransportCallMetrics NativeFirstFrameTransportCalls;
        double NativeFirstFrameDurationMs{0.0};
        double NativeFirstFrameDemuxReadDurationMs{0.0};
        double NativeFirstFramePresentDurationMs{0.0};
        uint64_t NativeFirstFrameDemuxPacketCount{0};
        uint64_t NativeFirstFrameDemuxBytes{0};
        double PlaybackDemuxReadDurationMs{0.0};
        uint64_t PlaybackDemuxPacketCount{0};
        uint64_t PlaybackDemuxBytes{0};
        PlaybackTransportCallMetrics PlaybackTransportCalls;
        uint64_t ReadErrorCount{0};
        uint64_t ReadRetryCount{0};
        uint64_t ReadRecoveryCount{0};
        uint32_t MaxConsecutiveReadErrors{0};
        int32_t LastReadErrorCode{0};
        int32_t FatalReadErrorCode{0};
        double LastReadRecoveryDurationMs{0.0};
        int64_t ContainerStartTimeTicks{0};
        int64_t VideoStreamStartTimeTicks{0};
        int64_t SeekDemuxTargetTicks{-1};
        int64_t FirstPresentedPositionTicks{-1};
        double RenderIntervalMsP50{0.0};
        double RenderIntervalMsP05{0.0};
        double RenderIntervalMsP95{0.0};
        double RenderIntervalMsP99{0.0};
        double MinFrameGapMs{0.0};
        double MaxFrameGapMs{0.0};
        uint64_t RenderIntervalSampleCount{0};
        uint64_t RenderIntervalOverExpected2MsCount{0};
        uint64_t RenderIntervalOverExpected4MsCount{0};
        uint64_t RenderIntervalUnderExpected2MsCount{0};
        uint64_t RenderIntervalUnderExpected4MsCount{0};
        uint64_t RenderIntervalAfterAudioAheadWaitSampleCount{0};
        double RenderIntervalAfterAudioAheadWaitMsP95{0.0};
        double RenderIntervalAfterAudioAheadWaitMsP99{0.0};
        double RenderIntervalAfterAudioAheadWaitMsMax{0.0};
        uint64_t AudioAheadWaitEndToPresentSampleCount{0};
        double AudioAheadWaitEndToPresentMsP50{0.0};
        double AudioAheadWaitEndToPresentMsP95{0.0};
        double AudioAheadWaitEndToPresentMsP99{0.0};
        double AudioAheadWaitEndToPresentMsMax{0.0};
        uint64_t RenderIntervalAfterNonAudioWaitSampleCount{0};
        double RenderIntervalAfterNonAudioWaitMsP95{0.0};
        double RenderIntervalAfterNonAudioWaitMsP99{0.0};
        double RenderIntervalAfterNonAudioWaitMsMax{0.0};
        double PresentDurationMsP50{0.0};
        double PresentDurationMsP95{0.0};
        double PresentDurationMsP99{0.0};
        double PresentDurationMsMax{0.0};
        double VideoDecodeDurationMsP50{0.0};
        double VideoDecodeDurationMsP95{0.0};
        double VideoDecodeDurationMsP99{0.0};
        double VideoDecodeDurationMsMax{0.0};
        double VideoRenderDurationMsP50{0.0};
        double VideoRenderDurationMsP95{0.0};
        double VideoRenderDurationMsP99{0.0};
        double VideoRenderDurationMsMax{0.0};
        double AudioAheadWaitDurationMsP50{0.0};
        double AudioAheadWaitDurationMsP95{0.0};
        double AudioAheadWaitDurationMsP99{0.0};
        double AudioAheadWaitDurationMsMax{0.0};
        double AudioAheadWaitTargetMsP50{0.0};
        double AudioAheadWaitTargetMsP95{0.0};
        double AudioAheadWaitTargetMsP99{0.0};
        double AudioAheadWaitTargetMsMax{0.0};
        double AudioAheadWaitOversleepMsP50{0.0};
        double AudioAheadWaitOversleepMsP95{0.0};
        double AudioAheadWaitOversleepMsP99{0.0};
        double AudioAheadWaitOversleepMsMax{0.0};
        double AudioAheadWaitFinalDeltaAbsMsP50{0.0};
        double AudioAheadWaitFinalDeltaAbsMsP95{0.0};
        double AudioAheadWaitFinalDeltaAbsMsP99{0.0};
        double AudioAheadWaitFinalDeltaAbsMsMax{0.0};
        uint64_t AudioAheadWaitEpisodeCount{0};
        double AudioAheadWaitPassesPerEpisodeP50{0.0};
        double AudioAheadWaitPassesPerEpisodeP95{0.0};
        double AudioAheadWaitPassesPerEpisodeP99{0.0};
        double AudioAheadWaitPassesPerEpisodeMax{0.0};
        double AudioAheadWaitPassDurationMsP50{0.0};
        double AudioAheadWaitPassDurationMsP95{0.0};
        double AudioAheadWaitPassDurationMsP99{0.0};
        double AudioAheadWaitPassDurationMsMax{0.0};
        double AudioAheadWaitPassTargetMsP50{0.0};
        double AudioAheadWaitPassTargetMsP95{0.0};
        double AudioAheadWaitPassTargetMsP99{0.0};
        double AudioAheadWaitPassTargetMsMax{0.0};
        double AudioAheadWaitPassOversleepMsP50{0.0};
        double AudioAheadWaitPassOversleepMsP95{0.0};
        double AudioAheadWaitPassOversleepMsP99{0.0};
        double AudioAheadWaitPassOversleepMsMax{0.0};
        double FramePacingSourceFrameRate{0.0};
        double LateFrameDropToleranceMs{0.0};
        double AudioVideoDriftMsP50{0.0};
        double AudioVideoDriftMsP95{0.0};
        double AudioVideoDriftMsP99{0.0};
        double AudioVideoDriftMsMax{0.0};
    };

    class PlaybackQualityHistogram
    {
    public:
        void Reset() noexcept
        {
            m_count = 0;
            m_replaceIndex = 0;
            m_max = 0.0;
            m_min = 0.0;
            m_values.fill(0.0);
        }

        void Add(double value) noexcept
        {
            if (value < 0.0)
            {
                value = -value;
            }

            if (m_count == 0)
            {
                m_min = value;
                m_max = value;
            }
            else
            {
                m_min = (std::min)(m_min, value);
                m_max = (std::max)(m_max, value);
            }

            if (m_count < m_values.size())
            {
                m_values[m_count] = value;
                ++m_count;
                return;
            }

            const auto index = m_replaceIndex % m_values.size();
            m_values[index] = value;
            ++m_replaceIndex;
        }

        double Percentile(double percentile) const noexcept
        {
            if (m_count == 0)
            {
                return 0.0;
            }

            auto copy = m_values;
            const auto count = m_count;
            std::sort(copy.begin(), copy.begin() + static_cast<std::ptrdiff_t>(count));
            const auto clamped = (std::min)((std::max)(percentile, 0.0), 100.0);
            auto position = static_cast<size_t>(std::ceil((clamped / 100.0) * count));
            if (position == 0)
            {
                position = 1;
            }

            return copy[position - 1];
        }

        double Max() const noexcept
        {
            return m_max;
        }

        double Min() const noexcept
        {
            return m_min;
        }

        size_t Count() const noexcept
        {
            return m_count;
        }

        uint64_t CountGreaterThan(double threshold) const noexcept
        {
            auto count = uint64_t{0};
            for (auto index = size_t{0}; index < m_count; ++index)
            {
                if (m_values[index] > threshold)
                {
                    ++count;
                }
            }

            return count;
        }

        uint64_t CountLessThan(double threshold) const noexcept
        {
            auto count = uint64_t{0};
            for (auto index = size_t{0}; index < m_count; ++index)
            {
                if (m_values[index] < threshold)
                {
                    ++count;
                }
            }

            return count;
        }

    private:
        std::array<double, 512> m_values{};
        size_t m_count{0};
        size_t m_replaceIndex{0};
        double m_max{0.0};
        double m_min{0.0};
    };

    class PlaybackQualityMetrics
    {
    public:
        uint64_t RenderPasses{0};
        uint64_t DecodedVideoFrames{0};
        uint64_t HardwareDecodedVideoFrames{0};
        uint64_t SoftwareDecodedVideoFrames{0};
        uint64_t RenderedVideoFrames{0};
        uint64_t SubmittedAudioFrames{0};
        uint64_t DroppedVideoFrames{0};
        uint64_t SeekPrerollDroppedFrames{0};
        uint64_t VideoAheadWaitCount{0};
        uint64_t AudioAheadWaitCount{0};
        uint64_t VideoClockWaitCount{0};
        uint64_t VideoStarvedPasses{0};
        uint64_t AudioStarvedPasses{0};
        uint64_t QueuedAudioBuffers{0};
        int64_t AudioClockTicks{0};
        int64_t VideoPositionTicks{0};
        double NativeGraphOpenDurationMs{0.0};
        double FfmpegOpenInputDurationMs{0.0};
        double FfmpegStreamInfoDurationMs{0.0};
        double NativeStartupSeekDurationMs{0.0};
        uint64_t FfmpegOpenInputBytesRead{0};
        uint64_t FfmpegStreamInfoBytesRead{0};
        uint64_t NativeStartupSeekBytesRead{0};
        uint64_t NativeFirstFrameTransportBytesRead{0};
        std::string StartupTransportProvider{"ffmpeg-builtin"};
        bool StartupTransportCallEvidenceAvailable{false};
        PlaybackTransportCallMetrics FfmpegOpenInputTransportCalls;
        PlaybackTransportCallMetrics FfmpegStreamInfoTransportCalls;
        PlaybackTransportCallMetrics NativeStartupSeekTransportCalls;
        PlaybackTransportCallMetrics NativeFirstFrameTransportCalls;
        double NativeFirstFrameDurationMs{0.0};
        double NativeFirstFrameDemuxReadDurationMs{0.0};
        double NativeFirstFramePresentDurationMs{0.0};
        uint64_t NativeFirstFrameDemuxPacketCount{0};
        uint64_t NativeFirstFrameDemuxBytes{0};
        uint64_t ReadErrorCount{0};
        uint64_t ReadRetryCount{0};
        uint64_t ReadRecoveryCount{0};
        uint32_t MaxConsecutiveReadErrors{0};
        int32_t LastReadErrorCode{0};
        int32_t FatalReadErrorCode{0};
        double LastReadRecoveryDurationMs{0.0};
        int64_t ContainerStartTimeTicks{0};
        int64_t VideoStreamStartTimeTicks{0};
        int64_t SeekDemuxTargetTicks{-1};
        int64_t FirstPresentedPositionTicks{-1};
        double FramePacingSourceFrameRate{0.0};
        double LateFrameDropToleranceMs{0.0};

        void Reset() noexcept
        {
            *this = PlaybackQualityMetrics{};
        }

        void ResetRuntimeSamplesPreservingOpenTiming() noexcept
        {
            auto const nativeGraphOpenDurationMs = NativeGraphOpenDurationMs;
            auto const ffmpegOpenInputDurationMs = FfmpegOpenInputDurationMs;
            auto const ffmpegStreamInfoDurationMs = FfmpegStreamInfoDurationMs;
            auto const nativeStartupSeekDurationMs = NativeStartupSeekDurationMs;
            auto const ffmpegOpenInputBytesRead = FfmpegOpenInputBytesRead;
            auto const ffmpegStreamInfoBytesRead = FfmpegStreamInfoBytesRead;
            auto const nativeStartupSeekBytesRead = NativeStartupSeekBytesRead;
            auto const nativeFirstFrameTransportBytesRead = NativeFirstFrameTransportBytesRead;
            auto const startupTransportProvider = StartupTransportProvider;
            auto const startupTransportCallEvidenceAvailable = StartupTransportCallEvidenceAvailable;
            auto const ffmpegOpenInputTransportCalls = FfmpegOpenInputTransportCalls;
            auto const ffmpegStreamInfoTransportCalls = FfmpegStreamInfoTransportCalls;
            auto const nativeStartupSeekTransportCalls = NativeStartupSeekTransportCalls;
            auto const nativeFirstFrameTransportCalls = NativeFirstFrameTransportCalls;
            auto const nativeFirstFrameDurationMs = NativeFirstFrameDurationMs;
            auto const nativeFirstFrameDemuxReadDurationMs = NativeFirstFrameDemuxReadDurationMs;
            auto const nativeFirstFramePresentDurationMs = NativeFirstFramePresentDurationMs;
            auto const nativeFirstFrameDemuxPacketCount = NativeFirstFrameDemuxPacketCount;
            auto const nativeFirstFrameDemuxBytes = NativeFirstFrameDemuxBytes;
            auto const containerStartTimeTicks = ContainerStartTimeTicks;
            auto const videoStreamStartTimeTicks = VideoStreamStartTimeTicks;
            Reset();
            NativeGraphOpenDurationMs = nativeGraphOpenDurationMs;
            FfmpegOpenInputDurationMs = ffmpegOpenInputDurationMs;
            FfmpegStreamInfoDurationMs = ffmpegStreamInfoDurationMs;
            NativeStartupSeekDurationMs = nativeStartupSeekDurationMs;
            FfmpegOpenInputBytesRead = ffmpegOpenInputBytesRead;
            FfmpegStreamInfoBytesRead = ffmpegStreamInfoBytesRead;
            NativeStartupSeekBytesRead = nativeStartupSeekBytesRead;
            NativeFirstFrameTransportBytesRead = nativeFirstFrameTransportBytesRead;
            StartupTransportProvider = startupTransportProvider;
            StartupTransportCallEvidenceAvailable = startupTransportCallEvidenceAvailable;
            FfmpegOpenInputTransportCalls = ffmpegOpenInputTransportCalls;
            FfmpegStreamInfoTransportCalls = ffmpegStreamInfoTransportCalls;
            NativeStartupSeekTransportCalls = nativeStartupSeekTransportCalls;
            NativeFirstFrameTransportCalls = nativeFirstFrameTransportCalls;
            NativeFirstFrameDurationMs = nativeFirstFrameDurationMs;
            NativeFirstFrameDemuxReadDurationMs = nativeFirstFrameDemuxReadDurationMs;
            NativeFirstFramePresentDurationMs = nativeFirstFramePresentDurationMs;
            NativeFirstFrameDemuxPacketCount = nativeFirstFrameDemuxPacketCount;
            NativeFirstFrameDemuxBytes = nativeFirstFrameDemuxBytes;
            ContainerStartTimeTicks = containerStartTimeTicks;
            VideoStreamStartTimeTicks = videoStreamStartTimeTicks;
        }

        void RecordRenderIntervalMs(double value) noexcept
        {
            m_renderIntervals.Add(value);
        }

        void RecordRenderIntervalAfterAudioAheadWaitMs(double value) noexcept
        {
            m_renderIntervalsAfterAudioAheadWait.Add(value);
        }

        void RecordAudioAheadWaitEndToPresentMs(double value) noexcept
        {
            m_audioAheadWaitEndToPresent.Add(value);
        }

        void RecordRenderIntervalAfterNonAudioWaitMs(double value) noexcept
        {
            m_renderIntervalsAfterNonAudioWait.Add(value);
        }

        void RecordPresentDurationMs(double value) noexcept
        {
            m_presentDurations.Add(value);
        }

        void RecordVideoDecodeDurationMs(double value) noexcept
        {
            m_videoDecodeDurations.Add(value);
        }

        void RecordVideoRenderDurationMs(double value) noexcept
        {
            m_videoRenderDurations.Add(value);
        }

        void RecordAudioAheadWaitDurationMs(double value) noexcept
        {
            RecordAudioAheadWaitMs(value, 0.0, value, 0.0, 1);
        }

        void RecordAudioAheadWaitMs(
            double durationMs,
            double targetMs,
            double oversleepMs,
            double finalDeltaMs,
            uint64_t passCount) noexcept
        {
            if (targetMs < 0.0)
            {
                targetMs = 0.0;
            }

            if (oversleepMs < 0.0)
            {
                oversleepMs = 0.0;
            }

            if (finalDeltaMs < 0.0)
            {
                finalDeltaMs = -finalDeltaMs;
            }

            if (passCount == 0)
            {
                passCount = 1;
            }

            m_audioAheadWaitDurations.Add(durationMs);
            m_audioAheadWaitTargets.Add(targetMs);
            m_audioAheadWaitOversleeps.Add(oversleepMs);
            m_audioAheadWaitFinalDeltaAbsMs.Add(finalDeltaMs);
            m_audioAheadWaitPassesPerEpisode.Add(static_cast<double>(passCount));
        }

        double RecordAudioAheadWaitPassMs(double durationMs, double targetMs) noexcept
        {
            if (durationMs < 0.0)
            {
                durationMs = 0.0;
            }

            if (targetMs < 0.0)
            {
                targetMs = 0.0;
            }

            auto oversleepMs = durationMs > targetMs ? durationMs - targetMs : 0.0;
            m_audioAheadWaitPassDurations.Add(durationMs);
            m_audioAheadWaitPassTargets.Add(targetMs);
            m_audioAheadWaitPassOversleeps.Add(oversleepMs);
            return oversleepMs;
        }

        void RecordAudioVideoDriftTicks(int64_t driftTicks) noexcept
        {
            m_audioVideoDriftMs.Add(static_cast<double>(driftTicks) / 10000.0);
        }

        PlaybackQualityMetricsSnapshot Snapshot() const noexcept
        {
            PlaybackQualityMetricsSnapshot snapshot{};
            snapshot.RenderPasses = RenderPasses;
            snapshot.DecodedVideoFrames = DecodedVideoFrames;
            snapshot.HardwareDecodedVideoFrames = HardwareDecodedVideoFrames;
            snapshot.SoftwareDecodedVideoFrames = SoftwareDecodedVideoFrames;
            snapshot.RenderedVideoFrames = RenderedVideoFrames;
            snapshot.SubmittedAudioFrames = SubmittedAudioFrames;
            snapshot.DroppedVideoFrames = DroppedVideoFrames;
            snapshot.SeekPrerollDroppedFrames = SeekPrerollDroppedFrames;
            snapshot.VideoAheadWaitCount = VideoAheadWaitCount;
            snapshot.AudioAheadWaitCount = AudioAheadWaitCount;
            snapshot.VideoClockWaitCount = VideoClockWaitCount;
            snapshot.VideoStarvedPasses = VideoStarvedPasses;
            snapshot.AudioStarvedPasses = AudioStarvedPasses;
            snapshot.QueuedAudioBuffers = QueuedAudioBuffers;
            snapshot.AudioClockTicks = AudioClockTicks;
            snapshot.VideoPositionTicks = VideoPositionTicks;
            snapshot.NativeGraphOpenDurationMs = NativeGraphOpenDurationMs;
            snapshot.FfmpegOpenInputDurationMs = FfmpegOpenInputDurationMs;
            snapshot.FfmpegStreamInfoDurationMs = FfmpegStreamInfoDurationMs;
            snapshot.NativeStartupSeekDurationMs = NativeStartupSeekDurationMs;
            snapshot.FfmpegOpenInputBytesRead = FfmpegOpenInputBytesRead;
            snapshot.FfmpegStreamInfoBytesRead = FfmpegStreamInfoBytesRead;
            snapshot.NativeStartupSeekBytesRead = NativeStartupSeekBytesRead;
            snapshot.NativeFirstFrameTransportBytesRead = NativeFirstFrameTransportBytesRead;
            snapshot.StartupTransportProvider = StartupTransportProvider;
            snapshot.StartupTransportCallEvidenceAvailable = StartupTransportCallEvidenceAvailable;
            snapshot.FfmpegOpenInputTransportCalls = FfmpegOpenInputTransportCalls;
            snapshot.FfmpegStreamInfoTransportCalls = FfmpegStreamInfoTransportCalls;
            snapshot.NativeStartupSeekTransportCalls = NativeStartupSeekTransportCalls;
            snapshot.NativeFirstFrameTransportCalls = NativeFirstFrameTransportCalls;
            snapshot.NativeFirstFrameDurationMs = NativeFirstFrameDurationMs;
            snapshot.NativeFirstFrameDemuxReadDurationMs = NativeFirstFrameDemuxReadDurationMs;
            snapshot.NativeFirstFramePresentDurationMs = NativeFirstFramePresentDurationMs;
            snapshot.NativeFirstFrameDemuxPacketCount = NativeFirstFrameDemuxPacketCount;
            snapshot.NativeFirstFrameDemuxBytes = NativeFirstFrameDemuxBytes;
            snapshot.ReadErrorCount = ReadErrorCount;
            snapshot.ReadRetryCount = ReadRetryCount;
            snapshot.ReadRecoveryCount = ReadRecoveryCount;
            snapshot.MaxConsecutiveReadErrors = MaxConsecutiveReadErrors;
            snapshot.LastReadErrorCode = LastReadErrorCode;
            snapshot.FatalReadErrorCode = FatalReadErrorCode;
            snapshot.LastReadRecoveryDurationMs = LastReadRecoveryDurationMs;
            snapshot.ContainerStartTimeTicks = ContainerStartTimeTicks;
            snapshot.VideoStreamStartTimeTicks = VideoStreamStartTimeTicks;
            snapshot.SeekDemuxTargetTicks = SeekDemuxTargetTicks;
            snapshot.FirstPresentedPositionTicks = FirstPresentedPositionTicks;
            snapshot.RenderIntervalMsP05 = m_renderIntervals.Percentile(5);
            snapshot.RenderIntervalMsP50 = m_renderIntervals.Percentile(50);
            snapshot.RenderIntervalMsP95 = m_renderIntervals.Percentile(95);
            snapshot.RenderIntervalMsP99 = m_renderIntervals.Percentile(99);
            snapshot.MinFrameGapMs = m_renderIntervals.Min();
            snapshot.MaxFrameGapMs = m_renderIntervals.Max();
            snapshot.RenderIntervalSampleCount = static_cast<uint64_t>(m_renderIntervals.Count());
            if (FramePacingSourceFrameRate > 0.0)
            {
                const auto expectedFrameDurationMs = 1000.0 / FramePacingSourceFrameRate;
                snapshot.RenderIntervalOverExpected2MsCount =
                    m_renderIntervals.CountGreaterThan(expectedFrameDurationMs + 2.0);
                snapshot.RenderIntervalOverExpected4MsCount =
                    m_renderIntervals.CountGreaterThan(expectedFrameDurationMs + 4.0);
                snapshot.RenderIntervalUnderExpected2MsCount =
                    m_renderIntervals.CountLessThan(expectedFrameDurationMs - 2.0);
                snapshot.RenderIntervalUnderExpected4MsCount =
                    m_renderIntervals.CountLessThan(expectedFrameDurationMs - 4.0);
            }
            snapshot.RenderIntervalAfterAudioAheadWaitSampleCount =
                static_cast<uint64_t>(m_renderIntervalsAfterAudioAheadWait.Count());
            snapshot.RenderIntervalAfterAudioAheadWaitMsP95 =
                m_renderIntervalsAfterAudioAheadWait.Percentile(95);
            snapshot.RenderIntervalAfterAudioAheadWaitMsP99 =
                m_renderIntervalsAfterAudioAheadWait.Percentile(99);
            snapshot.RenderIntervalAfterAudioAheadWaitMsMax =
                m_renderIntervalsAfterAudioAheadWait.Max();
            snapshot.AudioAheadWaitEndToPresentSampleCount =
                static_cast<uint64_t>(m_audioAheadWaitEndToPresent.Count());
            snapshot.AudioAheadWaitEndToPresentMsP50 =
                m_audioAheadWaitEndToPresent.Percentile(50);
            snapshot.AudioAheadWaitEndToPresentMsP95 =
                m_audioAheadWaitEndToPresent.Percentile(95);
            snapshot.AudioAheadWaitEndToPresentMsP99 =
                m_audioAheadWaitEndToPresent.Percentile(99);
            snapshot.AudioAheadWaitEndToPresentMsMax =
                m_audioAheadWaitEndToPresent.Max();
            snapshot.RenderIntervalAfterNonAudioWaitSampleCount =
                static_cast<uint64_t>(m_renderIntervalsAfterNonAudioWait.Count());
            snapshot.RenderIntervalAfterNonAudioWaitMsP95 =
                m_renderIntervalsAfterNonAudioWait.Percentile(95);
            snapshot.RenderIntervalAfterNonAudioWaitMsP99 =
                m_renderIntervalsAfterNonAudioWait.Percentile(99);
            snapshot.RenderIntervalAfterNonAudioWaitMsMax =
                m_renderIntervalsAfterNonAudioWait.Max();
            snapshot.PresentDurationMsP50 = m_presentDurations.Percentile(50);
            snapshot.PresentDurationMsP95 = m_presentDurations.Percentile(95);
            snapshot.PresentDurationMsP99 = m_presentDurations.Percentile(99);
            snapshot.PresentDurationMsMax = m_presentDurations.Max();
            snapshot.VideoDecodeDurationMsP50 = m_videoDecodeDurations.Percentile(50);
            snapshot.VideoDecodeDurationMsP95 = m_videoDecodeDurations.Percentile(95);
            snapshot.VideoDecodeDurationMsP99 = m_videoDecodeDurations.Percentile(99);
            snapshot.VideoDecodeDurationMsMax = m_videoDecodeDurations.Max();
            snapshot.VideoRenderDurationMsP50 = m_videoRenderDurations.Percentile(50);
            snapshot.VideoRenderDurationMsP95 = m_videoRenderDurations.Percentile(95);
            snapshot.VideoRenderDurationMsP99 = m_videoRenderDurations.Percentile(99);
            snapshot.VideoRenderDurationMsMax = m_videoRenderDurations.Max();
            snapshot.AudioAheadWaitDurationMsP50 = m_audioAheadWaitDurations.Percentile(50);
            snapshot.AudioAheadWaitDurationMsP95 = m_audioAheadWaitDurations.Percentile(95);
            snapshot.AudioAheadWaitDurationMsP99 = m_audioAheadWaitDurations.Percentile(99);
            snapshot.AudioAheadWaitDurationMsMax = m_audioAheadWaitDurations.Max();
            snapshot.AudioAheadWaitTargetMsP50 = m_audioAheadWaitTargets.Percentile(50);
            snapshot.AudioAheadWaitTargetMsP95 = m_audioAheadWaitTargets.Percentile(95);
            snapshot.AudioAheadWaitTargetMsP99 = m_audioAheadWaitTargets.Percentile(99);
            snapshot.AudioAheadWaitTargetMsMax = m_audioAheadWaitTargets.Max();
            snapshot.AudioAheadWaitOversleepMsP50 = m_audioAheadWaitOversleeps.Percentile(50);
            snapshot.AudioAheadWaitOversleepMsP95 = m_audioAheadWaitOversleeps.Percentile(95);
            snapshot.AudioAheadWaitOversleepMsP99 = m_audioAheadWaitOversleeps.Percentile(99);
            snapshot.AudioAheadWaitOversleepMsMax = m_audioAheadWaitOversleeps.Max();
            snapshot.AudioAheadWaitFinalDeltaAbsMsP50 = m_audioAheadWaitFinalDeltaAbsMs.Percentile(50);
            snapshot.AudioAheadWaitFinalDeltaAbsMsP95 = m_audioAheadWaitFinalDeltaAbsMs.Percentile(95);
            snapshot.AudioAheadWaitFinalDeltaAbsMsP99 = m_audioAheadWaitFinalDeltaAbsMs.Percentile(99);
            snapshot.AudioAheadWaitFinalDeltaAbsMsMax = m_audioAheadWaitFinalDeltaAbsMs.Max();
            snapshot.AudioAheadWaitEpisodeCount = static_cast<uint64_t>(m_audioAheadWaitPassesPerEpisode.Count());
            snapshot.AudioAheadWaitPassesPerEpisodeP50 = m_audioAheadWaitPassesPerEpisode.Percentile(50);
            snapshot.AudioAheadWaitPassesPerEpisodeP95 = m_audioAheadWaitPassesPerEpisode.Percentile(95);
            snapshot.AudioAheadWaitPassesPerEpisodeP99 = m_audioAheadWaitPassesPerEpisode.Percentile(99);
            snapshot.AudioAheadWaitPassesPerEpisodeMax = m_audioAheadWaitPassesPerEpisode.Max();
            snapshot.AudioAheadWaitPassDurationMsP50 = m_audioAheadWaitPassDurations.Percentile(50);
            snapshot.AudioAheadWaitPassDurationMsP95 = m_audioAheadWaitPassDurations.Percentile(95);
            snapshot.AudioAheadWaitPassDurationMsP99 = m_audioAheadWaitPassDurations.Percentile(99);
            snapshot.AudioAheadWaitPassDurationMsMax = m_audioAheadWaitPassDurations.Max();
            snapshot.AudioAheadWaitPassTargetMsP50 = m_audioAheadWaitPassTargets.Percentile(50);
            snapshot.AudioAheadWaitPassTargetMsP95 = m_audioAheadWaitPassTargets.Percentile(95);
            snapshot.AudioAheadWaitPassTargetMsP99 = m_audioAheadWaitPassTargets.Percentile(99);
            snapshot.AudioAheadWaitPassTargetMsMax = m_audioAheadWaitPassTargets.Max();
            snapshot.AudioAheadWaitPassOversleepMsP50 = m_audioAheadWaitPassOversleeps.Percentile(50);
            snapshot.AudioAheadWaitPassOversleepMsP95 = m_audioAheadWaitPassOversleeps.Percentile(95);
            snapshot.AudioAheadWaitPassOversleepMsP99 = m_audioAheadWaitPassOversleeps.Percentile(99);
            snapshot.AudioAheadWaitPassOversleepMsMax = m_audioAheadWaitPassOversleeps.Max();
            snapshot.FramePacingSourceFrameRate = FramePacingSourceFrameRate;
            snapshot.LateFrameDropToleranceMs = LateFrameDropToleranceMs;
            snapshot.AudioVideoDriftMsP50 = m_audioVideoDriftMs.Percentile(50);
            snapshot.AudioVideoDriftMsP95 = m_audioVideoDriftMs.Percentile(95);
            snapshot.AudioVideoDriftMsP99 = m_audioVideoDriftMs.Percentile(99);
            snapshot.AudioVideoDriftMsMax = m_audioVideoDriftMs.Max();
            return snapshot;
        }

    private:
        PlaybackQualityHistogram m_renderIntervals;
        PlaybackQualityHistogram m_renderIntervalsAfterAudioAheadWait;
        PlaybackQualityHistogram m_audioAheadWaitEndToPresent;
        PlaybackQualityHistogram m_renderIntervalsAfterNonAudioWait;
        PlaybackQualityHistogram m_presentDurations;
        PlaybackQualityHistogram m_videoDecodeDurations;
        PlaybackQualityHistogram m_videoRenderDurations;
        PlaybackQualityHistogram m_audioAheadWaitDurations;
        PlaybackQualityHistogram m_audioAheadWaitTargets;
        PlaybackQualityHistogram m_audioAheadWaitOversleeps;
        PlaybackQualityHistogram m_audioAheadWaitFinalDeltaAbsMs;
        PlaybackQualityHistogram m_audioAheadWaitPassesPerEpisode;
        PlaybackQualityHistogram m_audioAheadWaitPassDurations;
        PlaybackQualityHistogram m_audioAheadWaitPassTargets;
        PlaybackQualityHistogram m_audioAheadWaitPassOversleeps;
        PlaybackQualityHistogram m_audioVideoDriftMs;
    };
}
