#pragma once

#include <chrono>
#include <cstdint>

namespace winrt::NoiraPlayer::Native::implementation
{
    class PlaybackFramePacing
    {
    public:
        static constexpr int64_t VideoAheadToleranceTicks = 100000;
        static constexpr int64_t VideoDropToleranceTicks = 1000000;
        static constexpr int64_t MinimumFrameRateAdaptiveDropToleranceTicks = 400000;
        static constexpr double LateFrameDropFrameTolerance = 2.5;

        static constexpr std::chrono::milliseconds RenderLoopWait() noexcept
        {
            return std::chrono::milliseconds(5);
        }

        static constexpr bool ShouldWaitForAudio(
            int64_t framePositionTicks,
            int64_t audioPositionTicks,
            bool hasQueuedAudio) noexcept
        {
            return hasQueuedAudio &&
                framePositionTicks > audioPositionTicks + VideoAheadToleranceTicks;
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
                (framePositionTicks - audioPositionTicks - VideoAheadToleranceTicks) / 10);
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
    };
}
