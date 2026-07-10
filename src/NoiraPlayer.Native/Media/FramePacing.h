#pragma once

#include <chrono>
#include <cstdint>

namespace winrt::NoiraPlayer::Native::implementation
{
    class PlaybackFramePacing
    {
    public:
        static constexpr int64_t VideoAheadToleranceTicks = 100000;
        static constexpr int64_t AudioAheadRenderStartLeadTicks = 30000;
        static constexpr int64_t VideoDropToleranceTicks = 1000000;
        static constexpr int64_t MinimumFrameRateAdaptiveDropToleranceTicks = 400000;
        static constexpr double LateFrameDropFrameTolerance = 2.5;
        static constexpr std::chrono::milliseconds MaxVideoClockWait() noexcept
        {
            return std::chrono::milliseconds(10);
        }

        static constexpr std::chrono::milliseconds RenderLoopWait() noexcept
        {
            return std::chrono::milliseconds(5);
        }

        static constexpr bool ShouldUseRenderLoopTimer(std::chrono::steady_clock::duration delay) noexcept
        {
            return delay > std::chrono::steady_clock::duration::zero();
        }

        static constexpr bool ShouldWaitForAudio(
            int64_t framePositionTicks,
            int64_t audioPositionTicks,
            bool hasQueuedAudio) noexcept
        {
            return hasQueuedAudio &&
                framePositionTicks > audioPositionTicks + VideoAheadToleranceTicks + AudioAheadRenderStartLeadTicks;
        }

        static constexpr std::chrono::microseconds AudioAheadWaitDuration(
            int64_t framePositionTicks,
            int64_t audioPositionTicks,
            bool hasQueuedAudio) noexcept
        {
            if (!ShouldWaitForAudio(framePositionTicks, audioPositionTicks, hasQueuedAudio))
            {
                return std::chrono::microseconds(0);
            }

            return std::chrono::microseconds(
                (framePositionTicks - audioPositionTicks - VideoAheadToleranceTicks - AudioAheadRenderStartLeadTicks) / 10);
        }

        static constexpr bool ShouldDropLateFrame(
            int64_t framePositionTicks,
            int64_t audioPositionTicks,
            bool hasQueuedAudio) noexcept
        {
            return hasQueuedAudio &&
                audioPositionTicks > framePositionTicks + VideoDropToleranceTicks;
        }

        static constexpr int64_t LateFrameDropToleranceTicks(double videoFrameRate) noexcept
        {
            if (videoFrameRate <= 0.0)
            {
                return VideoDropToleranceTicks;
            }

            auto adaptiveTolerance = static_cast<int64_t>(
                10000000.0 * LateFrameDropFrameTolerance / videoFrameRate);
            return adaptiveTolerance > MinimumFrameRateAdaptiveDropToleranceTicks
                ? adaptiveTolerance
                : MinimumFrameRateAdaptiveDropToleranceTicks;
        }

        static constexpr bool ShouldDropLateFrame(
            int64_t framePositionTicks,
            int64_t audioPositionTicks,
            bool hasQueuedAudio,
            double videoFrameRate) noexcept
        {
            return hasQueuedAudio &&
                audioPositionTicks > framePositionTicks + LateFrameDropToleranceTicks(videoFrameRate);
        }

        static constexpr bool ShouldWaitForVideoClock(
            int64_t framePositionTicks,
            int64_t clockStartPositionTicks,
            int64_t clockElapsedTicks) noexcept
        {
            return framePositionTicks > clockStartPositionTicks &&
                framePositionTicks - clockStartPositionTicks > clockElapsedTicks + VideoAheadToleranceTicks;
        }

        static constexpr std::chrono::microseconds VideoClockWaitDuration(
            int64_t framePositionTicks,
            int64_t clockStartPositionTicks,
            int64_t clockElapsedTicks) noexcept
        {
            if (!ShouldWaitForVideoClock(framePositionTicks, clockStartPositionTicks, clockElapsedTicks))
            {
                return std::chrono::microseconds(0);
            }

            auto delay = std::chrono::microseconds(
                (framePositionTicks - clockStartPositionTicks - clockElapsedTicks - VideoAheadToleranceTicks) / 10);
            auto maxDelay = std::chrono::duration_cast<std::chrono::microseconds>(MaxVideoClockWait());
            return delay < maxDelay ? delay : maxDelay;
        }
    };
}
