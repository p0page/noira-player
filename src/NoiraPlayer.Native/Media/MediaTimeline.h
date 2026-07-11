#pragma once

#include <algorithm>
#include <cstdint>
#include <limits>

namespace winrt::NoiraPlayer::Native::implementation
{
    class MediaTimeline
    {
    public:
        void Reset(int64_t originTicks = 0, int64_t durationTicks = 0) noexcept
        {
            m_originTicks = (std::max<int64_t>)(0, originTicks);
            m_durationTicks = (std::max<int64_t>)(0, durationTicks);
        }

        int64_t OriginTicks() const noexcept
        {
            return m_originTicks;
        }

        int64_t DurationTicks() const noexcept
        {
            return m_durationTicks;
        }

        int64_t ToLogicalTicks(int64_t demuxTicks) const noexcept
        {
            return demuxTicks <= m_originTicks ? 0 : demuxTicks - m_originTicks;
        }

        int64_t ToDemuxTicks(int64_t logicalTicks) const noexcept
        {
            auto normalized = (std::max<int64_t>)(0, logicalTicks);
            auto remaining = (std::numeric_limits<int64_t>::max)() - m_originTicks;
            return normalized >= remaining
                ? (std::numeric_limits<int64_t>::max)()
                : m_originTicks + normalized;
        }

    private:
        int64_t m_originTicks{0};
        int64_t m_durationTicks{0};
    };
}
