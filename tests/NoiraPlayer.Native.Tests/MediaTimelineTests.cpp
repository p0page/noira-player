#include <cassert>

#include "Media/MediaTimeline.h"

using winrt::NoiraPlayer::Native::implementation::MediaTimeline;

int main()
{
    MediaTimeline timeline;
    timeline.Reset(14'000'000, 60'000'000);

    assert(timeline.OriginTicks() == 14'000'000);
    assert(timeline.DurationTicks() == 60'000'000);
    assert(timeline.ToLogicalTicks(14'000'000) == 0);
    assert(timeline.ToLogicalTicks(14'213'333) == 213'333);
    assert(timeline.ToLogicalTicks(13'000'000) == 0);
    assert(timeline.ToDemuxTicks(0) == 14'000'000);
    assert(timeline.ToDemuxTicks(10'000'000) == 24'000'000);

    timeline.Reset(-1, -1);
    assert(timeline.OriginTicks() == 0);
    assert(timeline.DurationTicks() == 0);
    assert(timeline.ToLogicalTicks(10'000'000) == 10'000'000);
    assert(timeline.ToDemuxTicks(10'000'000) == 10'000'000);

    return 0;
}
