#include <cassert>
#include <optional>

#include "Media/AudioFrameTimeline.h"

using winrt::NoiraPlayer::Native::implementation::AudioFrameTimeline;

int main()
{
    AudioFrameTimeline timeline;
    timeline.Reset(25'033'333);

    auto firstMissingTimestamp = timeline.Resolve(std::nullopt, 40, 48'000);
    assert(firstMissingTimestamp == 25'033'333);

    auto secondMissingTimestamp = timeline.Resolve(std::nullopt, 40, 48'000);
    assert(secondMissingTimestamp == 25'041'666);

    auto explicitTimestamp = timeline.Resolve(30'000'000, 1'536, 48'000);
    assert(explicitTimestamp == 30'000'000);

    auto afterExplicitTimestamp = timeline.Resolve(std::nullopt, 1'536, 48'000);
    assert(afterExplicitTimestamp == 30'320'000);

    timeline.Reset(-1);
    assert(timeline.Resolve(std::nullopt, 0, 0) == 0);
}
