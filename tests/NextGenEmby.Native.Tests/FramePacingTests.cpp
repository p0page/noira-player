#include <cassert>

#include "Media/FramePacing.h"

using winrt::NextGenEmby::Native::implementation::PlaybackFramePacing;

int main()
{
    assert(PlaybackFramePacing::RenderLoopWait().count() <= 5);

    assert(PlaybackFramePacing::ShouldWaitForAudio(1'500'000, 1'000'000, true));
    assert(!PlaybackFramePacing::ShouldWaitForAudio(1'050'000, 1'000'000, true));
    assert(!PlaybackFramePacing::ShouldWaitForAudio(1'500'000, 1'000'000, false));

    assert(PlaybackFramePacing::ShouldDropLateFrame(1'000'000, 2'100'001, true));
    assert(!PlaybackFramePacing::ShouldDropLateFrame(1'000'000, 1'800'000, true));
    assert(!PlaybackFramePacing::ShouldDropLateFrame(1'000'000, 2'100'001, false));
}
