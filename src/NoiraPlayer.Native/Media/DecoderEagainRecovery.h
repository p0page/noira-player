#pragma once

#include <atomic>
#include <cstdint>

namespace winrt::NoiraPlayer::Native::implementation
{
    class DecoderEagainRecovery
    {
    public:
        static constexpr uint32_t MaxConsecutiveRetries = 4;

        void RecordSendPacketEagain() noexcept
        {
            m_sendPacketEagainCount.fetch_add(1, std::memory_order_relaxed);
        }

        bool TryRecover() noexcept
        {
            auto const consecutiveRetryCount =
                m_consecutiveRetryCount.load(std::memory_order_relaxed);
            if (consecutiveRetryCount >= MaxConsecutiveRetries)
            {
                if (!m_episodeExhausted.exchange(true, std::memory_order_relaxed))
                {
                    m_doubleEagainExhaustedCount.fetch_add(1, std::memory_order_relaxed);
                }
                return false;
            }

            m_consecutiveRetryCount.store(consecutiveRetryCount + 1, std::memory_order_relaxed);
            m_doubleEagainRetryCount.fetch_add(1, std::memory_order_relaxed);
            return true;
        }

        void RecordProgress() noexcept
        {
            auto const consecutiveRetryCount =
                m_consecutiveRetryCount.exchange(0, std::memory_order_relaxed);
            auto const episodeExhausted =
                m_episodeExhausted.exchange(false, std::memory_order_relaxed);
            if (consecutiveRetryCount > 0 && !episodeExhausted)
            {
                m_doubleEagainRecoveryCount.fetch_add(1, std::memory_order_relaxed);
            }
        }

        void Reset() noexcept
        {
            m_consecutiveRetryCount.store(0, std::memory_order_relaxed);
            m_sendPacketEagainCount.store(0, std::memory_order_relaxed);
            m_doubleEagainRetryCount.store(0, std::memory_order_relaxed);
            m_doubleEagainRecoveryCount.store(0, std::memory_order_relaxed);
            m_doubleEagainExhaustedCount.store(0, std::memory_order_relaxed);
            m_episodeExhausted.store(false, std::memory_order_relaxed);
        }

        uint32_t ConsecutiveRetryCount() const noexcept
        {
            return m_consecutiveRetryCount.load(std::memory_order_relaxed);
        }

        uint64_t SendPacketEagainCount() const noexcept
        {
            return m_sendPacketEagainCount.load(std::memory_order_relaxed);
        }

        uint64_t DoubleEagainRetryCount() const noexcept
        {
            return m_doubleEagainRetryCount.load(std::memory_order_relaxed);
        }

        uint64_t DoubleEagainRecoveryCount() const noexcept
        {
            return m_doubleEagainRecoveryCount.load(std::memory_order_relaxed);
        }

        uint64_t DoubleEagainExhaustedCount() const noexcept
        {
            return m_doubleEagainExhaustedCount.load(std::memory_order_relaxed);
        }

    private:
        std::atomic<uint32_t> m_consecutiveRetryCount{0};
        std::atomic<uint64_t> m_sendPacketEagainCount{0};
        std::atomic<uint64_t> m_doubleEagainRetryCount{0};
        std::atomic<uint64_t> m_doubleEagainRecoveryCount{0};
        std::atomic<uint64_t> m_doubleEagainExhaustedCount{0};
        std::atomic<bool> m_episodeExhausted{false};
    };
}
