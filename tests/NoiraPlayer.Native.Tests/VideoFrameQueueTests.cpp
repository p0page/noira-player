#include <cassert>
#include <chrono>
#include <stdexcept>
#include <thread>

#include "Media/VideoFrameQueue.h"

using namespace std::chrono_literals;
using winrt::NoiraPlayer::Native::implementation::VideoFrameQueue;
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
}

int main()
{
    VideoFrameQueue<int, 3> queue;
    auto generation = queue.Reset();

    assert(queue.Push(generation, 1));
    assert(queue.Push(generation, 2));
    assert(queue.Push(generation, 3));
    auto fullSnapshot = queue.Snapshot();
    assert(fullSnapshot.Depth == 3);
    assert(fullSnapshot.MaxDepth == 3);
    assert(fullSnapshot.Capacity == 3);

    auto fourthPushSucceeded = false;
    std::thread blockedProducer([&]()
    {
        fourthPushSucceeded = queue.Push(generation, 4);
    });
    assert(WaitUntil([&]() { return queue.Snapshot().ProducerWaitCount == 1; }));

    auto first = queue.TryPop(generation);
    assert(first.Status == VideoFrameQueuePopStatus::Item);
    assert(first.Value.has_value() && first.Value.value() == 1);
    blockedProducer.join();
    assert(fourthPushSucceeded);
    assert(queue.Snapshot().Depth == 3);

    auto nextGeneration = queue.Reset();
    assert(nextGeneration > generation);
    assert(queue.Snapshot().Depth == 0);
    assert(!queue.Push(generation, 5));
    assert(queue.TryPop(generation).Status == VideoFrameQueuePopStatus::StaleGeneration);
    assert(queue.TryPop(nextGeneration).Status == VideoFrameQueuePopStatus::Empty);

    queue.MarkEndOfStream(nextGeneration);
    assert(queue.TryPop(nextGeneration).Status == VideoFrameQueuePopStatus::EndOfStream);
    assert(!queue.Push(nextGeneration, 6));

    auto failedGeneration = queue.Reset();
    queue.Fail(failedGeneration, std::make_exception_ptr(std::runtime_error("decode failed")));
    auto failed = queue.TryPop(failedGeneration);
    assert(failed.Status == VideoFrameQueuePopStatus::Failed);
    assert(failed.Error != nullptr);
    try
    {
        std::rethrow_exception(failed.Error);
        assert(false);
    }
    catch (std::runtime_error const& error)
    {
        assert(std::string(error.what()) == "decode failed");
    }

    auto stoppedGeneration = queue.Reset();
    assert(queue.Push(stoppedGeneration, 7));
    assert(queue.Push(stoppedGeneration, 8));
    assert(queue.Push(stoppedGeneration, 9));
    auto stoppedPushSucceeded = true;
    std::thread stoppedProducer([&]()
    {
        stoppedPushSucceeded = queue.Push(stoppedGeneration, 10);
    });
    assert(WaitUntil([&]() { return queue.Snapshot().ProducerWaitCount == 1; }));
    queue.Stop();
    stoppedProducer.join();
    assert(!stoppedPushSucceeded);
    assert(queue.TryPop(stoppedGeneration).Status == VideoFrameQueuePopStatus::Stopped);
}
