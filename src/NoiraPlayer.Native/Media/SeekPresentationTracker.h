#pragma once

#include <chrono>
#include <cstdint>
#include <optional>

namespace winrt::NoiraPlayer::Native::implementation
{
    struct SeekPresentationSnapshot
    {
        uint64_t Generation{0};
        uint64_t PresentedFrameCount{0};
        std::optional<int64_t> ActualPositionTicks;
        std::optional<double> RecoveryDurationMs;
    };

    class SeekPresentationTracker
    {
    public:
        using Clock = std::chrono::steady_clock;

        uint64_t BeginSeek(
            uint64_t presentedFrameCount,
            Clock::time_point startedAt = Clock::now()) noexcept
        {
            ++m_generation;
            if (m_generation == 0)
            {
                ++m_generation;
            }

            m_presentedFrameCountAtStart = presentedFrameCount;
            m_presentedFrameCount = presentedFrameCount;
            m_actualPositionTicks.reset();
            m_recoveryDurationMs.reset();
            m_startedAt = startedAt;
            return m_generation;
        }

        void RecordPresentedFrame(
            uint64_t generation,
            uint64_t presentedFrameCount,
            int64_t positionTicks,
            Clock::time_point presentedAt = Clock::now()) noexcept
        {
            if (m_generation == 0 ||
                generation != m_generation ||
                m_actualPositionTicks.has_value() ||
                presentedFrameCount <= m_presentedFrameCountAtStart ||
                positionTicks < 0)
            {
                return;
            }

            m_presentedFrameCount = presentedFrameCount;
            m_actualPositionTicks = positionTicks;
            m_recoveryDurationMs = std::chrono::duration<double, std::milli>(
                presentedAt - m_startedAt).count();
        }

        uint64_t CurrentGeneration() const noexcept
        {
            return m_generation;
        }

        SeekPresentationSnapshot Snapshot() const noexcept
        {
            return {
                m_generation,
                m_presentedFrameCount,
                m_actualPositionTicks,
                m_recoveryDurationMs};
        }

        void Reset() noexcept
        {
            m_generation = 0;
            m_presentedFrameCountAtStart = 0;
            m_presentedFrameCount = 0;
            m_actualPositionTicks.reset();
            m_recoveryDurationMs.reset();
            m_startedAt = {};
        }

    private:
        uint64_t m_generation{0};
        uint64_t m_presentedFrameCountAtStart{0};
        uint64_t m_presentedFrameCount{0};
        std::optional<int64_t> m_actualPositionTicks;
        std::optional<double> m_recoveryDurationMs;
        Clock::time_point m_startedAt{};
    };
}
