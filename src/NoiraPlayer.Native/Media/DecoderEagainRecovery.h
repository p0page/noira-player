#pragma once

#include <cstdint>

namespace winrt::NoiraPlayer::Native::implementation
{
    class DecoderEagainRecovery
    {
    public:
        static constexpr uint32_t MaxConsecutiveRetries = 4;

        bool TryRecover() noexcept
        {
            if (m_consecutiveRetryCount >= MaxConsecutiveRetries)
            {
                return false;
            }

            ++m_consecutiveRetryCount;
            return true;
        }

        void RecordProgress() noexcept
        {
            m_consecutiveRetryCount = 0;
        }

        uint32_t ConsecutiveRetryCount() const noexcept
        {
            return m_consecutiveRetryCount;
        }

    private:
        uint32_t m_consecutiveRetryCount{0};
    };
}
