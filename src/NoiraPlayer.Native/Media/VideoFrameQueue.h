#pragma once

#include <condition_variable>
#include <cstddef>
#include <cstdint>
#include <deque>
#include <exception>
#include <mutex>
#include <optional>
#include <utility>

namespace winrt::NoiraPlayer::Native::implementation
{
    enum class VideoFrameQueuePopStatus
    {
        Empty,
        Item,
        EndOfStream,
        Failed,
        Stopped,
        StaleGeneration
    };

    template<typename T>
    struct VideoFrameQueuePopResult
    {
        VideoFrameQueuePopStatus Status{VideoFrameQueuePopStatus::Empty};
        std::optional<T> Value;
        std::exception_ptr Error;
    };

    struct VideoFrameQueueSnapshot
    {
        uint64_t Generation{0};
        size_t Depth{0};
        size_t Capacity{0};
        size_t MaxDepth{0};
        uint64_t ProducerWaitCount{0};
        bool EndOfStream{false};
        bool Failed{false};
        bool Stopped{true};
    };

    template<typename T, size_t CapacityValue>
    class VideoFrameQueue
    {
        static_assert(CapacityValue > 0);

    public:
        uint64_t Reset()
        {
            std::lock_guard lock(m_mutex);
            ++m_generation;
            m_items.clear();
            m_error = nullptr;
            m_endOfStream = false;
            m_stopped = false;
            m_maxDepth = 0;
            m_producerWaitCount = 0;
            m_changed.notify_all();
            return m_generation;
        }

        void Stop() noexcept
        {
            std::lock_guard lock(m_mutex);
            m_stopped = true;
            m_items.clear();
            m_changed.notify_all();
        }

        bool Push(uint64_t generation, T value)
        {
            std::unique_lock lock(m_mutex);
            if (!CanPush(generation))
            {
                return false;
            }

            if (m_items.size() >= CapacityValue)
            {
                ++m_producerWaitCount;
                m_changed.wait(lock, [this, generation]()
                {
                    return !CanPush(generation) || m_items.size() < CapacityValue;
                });
            }

            if (!CanPush(generation))
            {
                return false;
            }

            m_items.push_back(std::move(value));
            if (m_items.size() > m_maxDepth)
            {
                m_maxDepth = m_items.size();
            }
            m_changed.notify_all();
            return true;
        }

        VideoFrameQueuePopResult<T> TryPop(uint64_t generation)
        {
            std::lock_guard lock(m_mutex);
            if (generation != m_generation)
            {
                return {VideoFrameQueuePopStatus::StaleGeneration, std::nullopt, nullptr};
            }
            if (m_stopped)
            {
                return {VideoFrameQueuePopStatus::Stopped, std::nullopt, nullptr};
            }
            if (!m_items.empty())
            {
                auto value = std::move(m_items.front());
                m_items.pop_front();
                m_changed.notify_all();
                return {VideoFrameQueuePopStatus::Item, std::move(value), nullptr};
            }
            if (m_error)
            {
                return {VideoFrameQueuePopStatus::Failed, std::nullopt, m_error};
            }
            if (m_endOfStream)
            {
                return {VideoFrameQueuePopStatus::EndOfStream, std::nullopt, nullptr};
            }
            return {VideoFrameQueuePopStatus::Empty, std::nullopt, nullptr};
        }

        void MarkEndOfStream(uint64_t generation) noexcept
        {
            std::lock_guard lock(m_mutex);
            if (generation == m_generation && !m_stopped && !m_error)
            {
                m_endOfStream = true;
                m_changed.notify_all();
            }
        }

        void Fail(uint64_t generation, std::exception_ptr error) noexcept
        {
            std::lock_guard lock(m_mutex);
            if (generation == m_generation && !m_stopped)
            {
                m_error = error;
                m_changed.notify_all();
            }
        }

        VideoFrameQueueSnapshot Snapshot() const noexcept
        {
            std::lock_guard lock(m_mutex);
            return VideoFrameQueueSnapshot{
                m_generation,
                m_items.size(),
                CapacityValue,
                m_maxDepth,
                m_producerWaitCount,
                m_endOfStream,
                m_error != nullptr,
                m_stopped};
        }

    private:
        bool CanPush(uint64_t generation) const noexcept
        {
            return generation == m_generation &&
                !m_stopped &&
                !m_endOfStream &&
                !m_error;
        }

        mutable std::mutex m_mutex;
        std::condition_variable m_changed;
        std::deque<T> m_items;
        std::exception_ptr m_error;
        uint64_t m_generation{0};
        size_t m_maxDepth{0};
        uint64_t m_producerWaitCount{0};
        bool m_endOfStream{false};
        bool m_stopped{true};
    };
}
