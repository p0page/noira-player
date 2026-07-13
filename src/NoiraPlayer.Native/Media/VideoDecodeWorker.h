#pragma once

#include "VideoDecoder.h"
#include "VideoFrameQueue.h"

#include <atomic>
#include <cstdint>
#include <functional>
#include <optional>
#include <thread>

namespace winrt::NoiraPlayer::Native::implementation
{
    struct QueuedVideoFrame
    {
        DecodedVideoFrame Frame;
        double DecodeDurationMs{0.0};
    };

    class VideoDecodeWorker
    {
    public:
        using DecodeNextFrame = std::function<std::optional<DecodedVideoFrame>()>;
        using PopResult = VideoFrameQueuePopResult<QueuedVideoFrame>;

        explicit VideoDecodeWorker(DecodeNextFrame decodeNextFrame);
        ~VideoDecodeWorker();

        VideoDecodeWorker(VideoDecodeWorker const&) = delete;
        VideoDecodeWorker& operator=(VideoDecodeWorker const&) = delete;

        uint64_t Start();
        void Stop() noexcept;
        PopResult TryPop();
        VideoFrameQueueSnapshot Snapshot() const noexcept;

    private:
        void Run(uint64_t generation) noexcept;

        static constexpr size_t QueueCapacity = 3;
        DecodeNextFrame m_decodeNextFrame;
        VideoFrameQueue<QueuedVideoFrame, QueueCapacity> m_queue;
        std::atomic<uint64_t> m_generation{0};
        std::thread m_thread;
    };
}
