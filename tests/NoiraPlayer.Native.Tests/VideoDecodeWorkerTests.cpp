#include <atomic>
#include <cassert>
#include <chrono>
#include <stdexcept>
#include <thread>

#include "Media/VideoDecodeWorker.h"

using namespace std::chrono_literals;
using winrt::NoiraPlayer::Native::implementation::DecodedVideoFrame;
using winrt::NoiraPlayer::Native::implementation::VideoDecodeWorker;
using winrt::NoiraPlayer::Native::implementation::VideoFrameQueuePopStatus;

namespace
{
    template<typename Predicate>
    bool WaitUntil(Predicate predicate, std::chrono::milliseconds timeout = 1s)
    {
        auto deadline = std::chrono::steady_clock::now() + timeout;
        while (!predicate() && std::chrono::steady_clock::now() < deadline)
        {
            std::this_thread::sleep_for(1ms);
        }

        return predicate();
    }

    DecodedVideoFrame FrameAt(int64_t positionTicks)
    {
        DecodedVideoFrame frame;
        frame.PositionTicks = positionTicks;
        return frame;
    }
}

int main()
{
    std::atomic<int> decodeCount{0};
    VideoDecodeWorker worker([&]() -> std::optional<DecodedVideoFrame>
    {
        auto value = ++decodeCount;
        return value <= 4
            ? std::optional<DecodedVideoFrame>{FrameAt(value * 1000)}
            : std::nullopt;
    });

    auto firstGeneration = worker.Start();
    assert(firstGeneration > 0);
    assert(WaitUntil([&]() { return worker.Snapshot().Depth == 3; }));
    assert(worker.Snapshot().MaxDepth == 3);

    auto first = worker.TryPop();
    assert(first.Status == VideoFrameQueuePopStatus::Item);
    assert(first.Value.has_value());
    assert(first.Value->Frame.PositionTicks == 1000);
    assert(first.Value->DecodeDurationMs >= 0.0);
    assert(WaitUntil([&]() { return worker.Snapshot().EndOfStream; }));

    for (auto expectedPosition : {2000LL, 3000LL, 4000LL})
    {
        auto item = worker.TryPop();
        assert(item.Status == VideoFrameQueuePopStatus::Item);
        assert(item.Value->Frame.PositionTicks == expectedPosition);
    }
    assert(worker.TryPop().Status == VideoFrameQueuePopStatus::EndOfStream);
    worker.Stop();

    VideoDecodeWorker failingWorker([]() -> std::optional<DecodedVideoFrame>
    {
        throw std::runtime_error("decoder failed");
    });
    failingWorker.Start();
    assert(WaitUntil([&]() { return failingWorker.Snapshot().Failed; }));
    auto failed = failingWorker.TryPop();
    assert(failed.Status == VideoFrameQueuePopStatus::Failed);
    assert(failed.Error != nullptr);
    failingWorker.Stop();

    std::atomic<int64_t> unboundedPosition{0};
    VideoDecodeWorker blockedWorker([&]() -> std::optional<DecodedVideoFrame>
    {
        return FrameAt(++unboundedPosition);
    });
    auto blockedGeneration = blockedWorker.Start();
    assert(blockedGeneration > 0);
    assert(WaitUntil([&]()
    {
        auto snapshot = blockedWorker.Snapshot();
        return snapshot.Depth == 3 && snapshot.ProducerWaitCount == 1;
    }));
    blockedWorker.Stop();
    assert(blockedWorker.Snapshot().Stopped);
    assert(blockedWorker.TryPop().Status == VideoFrameQueuePopStatus::Stopped);
}
