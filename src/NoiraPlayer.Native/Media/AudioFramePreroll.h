#pragma once

#include <algorithm>
#include <cstdint>
#include <limits>
#include <optional>

namespace winrt::NoiraPlayer::Native::implementation
{
    class AudioFramePreroll
    {
    public:
        static constexpr int64_t TicksPerSecond = 10'000'000;

        void Reset(int64_t targetTicks) noexcept
        {
            m_targetTicks = targetTicks > 0
                ? std::optional<int64_t>{targetTicks}
                : std::nullopt;
        }

        std::optional<int64_t> Accept(
            int64_t positionTicks,
            uint32_t sampleCount,
            uint32_t sampleRate) noexcept
        {
            auto position = (std::max<int64_t>)(0, positionTicks);
            if (!m_targetTicks)
            {
                return position;
            }

            auto durationTicks = sampleRate == 0
                ? int64_t{0}
                : static_cast<int64_t>(
                    static_cast<uint64_t>(sampleCount) * TicksPerSecond / sampleRate);
            auto remaining = (std::numeric_limits<int64_t>::max)() - position;
            auto endTicks = durationTicks >= remaining
                ? (std::numeric_limits<int64_t>::max)()
                : position + durationTicks;
            if (endTicks <= *m_targetTicks)
            {
                return std::nullopt;
            }

            auto acceptedPosition = (std::max)(position, *m_targetTicks);
            m_targetTicks.reset();
            return acceptedPosition;
        }

    private:
        std::optional<int64_t> m_targetTicks;
    };
}
