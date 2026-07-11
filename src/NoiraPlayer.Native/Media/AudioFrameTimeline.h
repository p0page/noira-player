#pragma once

#include <algorithm>
#include <cstdint>
#include <optional>

namespace winrt::NoiraPlayer::Native::implementation
{
    class AudioFrameTimeline
    {
    public:
        static constexpr int64_t TicksPerSecond = 10'000'000;

        void Reset(int64_t positionTicks) noexcept
        {
            m_nextPositionTicks = (std::max<int64_t>)(0, positionTicks);
        }

        int64_t Resolve(
            std::optional<int64_t> decodedPositionTicks,
            uint32_t sampleCount,
            uint32_t sampleRate) noexcept
        {
            auto positionTicks = decodedPositionTicks && *decodedPositionTicks >= 0
                ? *decodedPositionTicks
                : m_nextPositionTicks;
            auto durationTicks = sampleRate == 0
                ? int64_t{0}
                : static_cast<int64_t>(
                    static_cast<uint64_t>(sampleCount) * TicksPerSecond / sampleRate);
            m_nextPositionTicks = positionTicks + durationTicks;
            return positionTicks;
        }

    private:
        int64_t m_nextPositionTicks{0};
    };
}
