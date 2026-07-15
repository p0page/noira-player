#include <cassert>
#include <chrono>

#include "Media/SeekPresentationTracker.h"

using winrt::NoiraPlayer::Native::implementation::SeekPresentationTracker;

int main()
{
    SeekPresentationTracker tracker;
    using Clock = std::chrono::steady_clock;
    auto const firstSeekStartedAt = Clock::time_point{} + std::chrono::seconds(1);

    tracker.RecordPresentedFrame(0, 1, 500'000, firstSeekStartedAt);
    assert(!tracker.Snapshot().ActualPositionTicks.has_value());
    assert(!tracker.Snapshot().RecoveryDurationMs.has_value());

    auto firstGeneration = tracker.BeginSeek(12, firstSeekStartedAt);
    auto pending = tracker.Snapshot();
    assert(pending.Generation == firstGeneration);
    assert(!pending.ActualPositionTicks.has_value());
    assert(!pending.RecoveryDurationMs.has_value());

    tracker.RecordPresentedFrame(
        firstGeneration - 1,
        13,
        10'000'000,
        firstSeekStartedAt + std::chrono::milliseconds(100));
    assert(!tracker.Snapshot().ActualPositionTicks.has_value());

    tracker.RecordPresentedFrame(
        firstGeneration,
        12,
        10'000'000,
        firstSeekStartedAt + std::chrono::milliseconds(200));
    assert(!tracker.Snapshot().ActualPositionTicks.has_value());

    tracker.RecordPresentedFrame(
        firstGeneration,
        13,
        12'750'000,
        firstSeekStartedAt + std::chrono::milliseconds(375));
    auto landed = tracker.Snapshot();
    assert(landed.Generation == firstGeneration);
    assert(landed.PresentedFrameCount == 13);
    assert(landed.ActualPositionTicks == 12'750'000);
    assert(landed.RecoveryDurationMs == 375.0);

    tracker.RecordPresentedFrame(
        firstGeneration,
        14,
        13'000'000,
        firstSeekStartedAt + std::chrono::milliseconds(900));
    assert(tracker.Snapshot().ActualPositionTicks == 12'750'000);
    assert(tracker.Snapshot().RecoveryDurationMs == 375.0);

    auto const secondSeekStartedAt = firstSeekStartedAt + std::chrono::seconds(2);
    auto secondGeneration = tracker.BeginSeek(14, secondSeekStartedAt);
    assert(secondGeneration > firstGeneration);
    assert(!tracker.Snapshot().ActualPositionTicks.has_value());
    assert(!tracker.Snapshot().RecoveryDurationMs.has_value());

    tracker.RecordPresentedFrame(
        firstGeneration,
        15,
        20'000'000,
        secondSeekStartedAt + std::chrono::milliseconds(50));
    assert(!tracker.Snapshot().ActualPositionTicks.has_value());

    tracker.RecordPresentedFrame(
        secondGeneration,
        15,
        20'000'000,
        secondSeekStartedAt + std::chrono::milliseconds(125));
    assert(tracker.Snapshot().ActualPositionTicks == 20'000'000);
    assert(tracker.Snapshot().RecoveryDurationMs == 125.0);

    return 0;
}
