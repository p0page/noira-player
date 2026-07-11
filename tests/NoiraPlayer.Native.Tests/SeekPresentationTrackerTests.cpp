#include <cassert>

#include "Media/SeekPresentationTracker.h"

using winrt::NoiraPlayer::Native::implementation::SeekPresentationTracker;

int main()
{
    SeekPresentationTracker tracker;

    auto firstGeneration = tracker.BeginSeek(12);
    auto pending = tracker.Snapshot();
    assert(pending.Generation == firstGeneration);
    assert(!pending.ActualPositionTicks.has_value());

    tracker.RecordPresentedFrame(firstGeneration - 1, 13, 10'000'000);
    assert(!tracker.Snapshot().ActualPositionTicks.has_value());

    tracker.RecordPresentedFrame(firstGeneration, 12, 10'000'000);
    assert(!tracker.Snapshot().ActualPositionTicks.has_value());

    tracker.RecordPresentedFrame(firstGeneration, 13, 12'750'000);
    auto landed = tracker.Snapshot();
    assert(landed.Generation == firstGeneration);
    assert(landed.PresentedFrameCount == 13);
    assert(landed.ActualPositionTicks == 12'750'000);

    tracker.RecordPresentedFrame(firstGeneration, 14, 13'000'000);
    assert(tracker.Snapshot().ActualPositionTicks == 12'750'000);

    auto secondGeneration = tracker.BeginSeek(14);
    assert(secondGeneration > firstGeneration);
    assert(!tracker.Snapshot().ActualPositionTicks.has_value());

    tracker.RecordPresentedFrame(firstGeneration, 15, 20'000'000);
    assert(!tracker.Snapshot().ActualPositionTicks.has_value());

    tracker.RecordPresentedFrame(secondGeneration, 15, 20'000'000);
    assert(tracker.Snapshot().ActualPositionTicks == 20'000'000);

    return 0;
}
