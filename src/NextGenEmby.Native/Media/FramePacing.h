#pragma once

#include <chrono>
#include <cstdint>

namespace winrt::NextGenEmby::Native::implementation
{
    class PlaybackFramePacing
    {
    public:
        static constexpr int64_t VideoAheadToleranceTicks = 100000;
        static constexpr int64_t VideoDropToleranceTicks = 1000000;

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

        static constexpr bool ShouldDropLateFrame(
            int64_t framePositionTicks,
            int64_t audioPositionTicks,
            bool hasQueuedAudio) noexcept
        {
            return hasQueuedAudio &&
                audioPositionTicks > framePositionTicks + VideoDropToleranceTicks;
        }
    };
}
