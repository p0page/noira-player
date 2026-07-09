#include <cassert>

#include "Media/FramePacing.h"

using winrt::NoiraPlayer::Native::implementation::PlaybackFramePacing;

int main()
{
    assert(PlaybackFramePacing::RenderLoopWait().count() <= 5);

    assert(PlaybackFramePacing::ShouldWaitForAudio(1'500'000, 1'000'000, true));
    assert(!PlaybackFramePacing::ShouldWaitForAudio(1'150'000, 1'000'000, true));
    assert(!PlaybackFramePacing::ShouldWaitForAudio(1'050'000, 1'000'000, true));
    assert(!PlaybackFramePacing::ShouldWaitForAudio(1'500'000, 1'000'000, false));
    assert(PlaybackFramePacing::AudioAheadWaitDuration(1'333'333, 1'000'000, true).count() == 13'333);
    assert(PlaybackFramePacing::AudioAheadWaitDuration(1'050'000, 1'000'000, true).count() == 0);
    assert(PlaybackFramePacing::AudioAheadWaitDuration(1'333'333, 1'000'000, false).count() == 0);

    assert(PlaybackFramePacing::ShouldDropLateFrame(1'000'000, 2'100'001, true));
    assert(!PlaybackFramePacing::ShouldDropLateFrame(1'000'000, 1'800'000, true));
    assert(!PlaybackFramePacing::ShouldDropLateFrame(1'000'000, 2'100'001, false));

    assert(PlaybackFramePacing::LateFrameDropToleranceTicks(0.0) ==
        PlaybackFramePacing::VideoDropToleranceTicks);
    assert(PlaybackFramePacing::LateFrameDropToleranceTicks(23.976) > 1'000'000);
    assert(PlaybackFramePacing::LateFrameDropToleranceTicks(60.0) < 500'000);

    assert(PlaybackFramePacing::ShouldDropLateFrame(1'000'000, 1'500'001, true, 60.0));
    assert(!PlaybackFramePacing::ShouldDropLateFrame(1'000'000, 1'350'000, true, 60.0));
    assert(!PlaybackFramePacing::ShouldDropLateFrame(1'000'000, 1'500'001, false, 60.0));

    assert(PlaybackFramePacing::ShouldWaitForVideoClock(1'000'000, 0, 800'000));
    assert(!PlaybackFramePacing::ShouldWaitForVideoClock(1'000'000, 0, 950'000));
    assert(!PlaybackFramePacing::ShouldWaitForVideoClock(0, 0, 0));
    assert(PlaybackFramePacing::VideoClockWaitDuration(1'000'000, 0, 800'000).count() == 10'000);
    assert(PlaybackFramePacing::VideoClockWaitDuration(1'000'000, 0, 950'000).count() == 0);
    assert(PlaybackFramePacing::VideoClockWaitDuration(0, 0, 0).count() == 0);
}
