#include "pch.h"
#include "VideoDecodeWorker.h"

#include <chrono>
#include <stdexcept>
#include <utility>

namespace winrt::NoiraPlayer::Native::implementation
{
    VideoDecodeWorker::VideoDecodeWorker(DecodeNextFrame decodeNextFrame)
        : m_decodeNextFrame(std::move(decodeNextFrame))
    {
        if (!m_decodeNextFrame)
        {
            throw std::invalid_argument("Video decode callback is required.");
        }
    }

    VideoDecodeWorker::~VideoDecodeWorker()
    {
        Stop();
    }

    uint64_t VideoDecodeWorker::Start()
    {
        Stop();
        auto generation = m_queue.Reset();
        m_generation.store(generation, std::memory_order_release);
        m_thread = std::thread([this, generation]()
        {
            Run(generation);
        });
        return generation;
    }

    void VideoDecodeWorker::Stop() noexcept
    {
        m_queue.Stop();
        if (m_thread.joinable())
        {
            m_thread.join();
        }
    }

    VideoDecodeWorker::PopResult VideoDecodeWorker::TryPop()
    {
        return m_queue.TryPop(m_generation.load(std::memory_order_acquire));
    }

    VideoFrameQueueSnapshot VideoDecodeWorker::Snapshot() const noexcept
    {
        return m_queue.Snapshot();
    }

    void VideoDecodeWorker::Run(uint64_t generation) noexcept
    {
        try
        {
            while (true)
            {
                auto startedAt = std::chrono::steady_clock::now();
                auto frame = m_decodeNextFrame();
                auto endedAt = std::chrono::steady_clock::now();
                if (!frame)
                {
                    m_queue.MarkEndOfStream(generation);
                    return;
                }

                auto durationMs = std::chrono::duration<double, std::milli>(
                    endedAt - startedAt).count();
                if (!m_queue.Push(generation, QueuedVideoFrame{std::move(*frame), durationMs}))
                {
                    return;
                }
            }
        }
        catch (...)
        {
            m_queue.Fail(generation, std::current_exception());
        }
    }
}
