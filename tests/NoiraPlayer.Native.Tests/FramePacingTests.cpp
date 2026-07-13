#include <cassert>

#include "Media/FramePacing.h"

using winrt::NoiraPlayer::Native::implementation::PlaybackFramePacing;

int main()
{
    assert(PlaybackFramePacing::RenderLoopWait().count() <= 5);
    assert(PlaybackFramePacing::ShouldUseRenderLoopTimer(PlaybackFramePacing::RenderLoopWait()));

    assert(PlaybackFramePacing::ShouldWaitForAudio(1'500'000, 1'000'000, true));
    assert(!PlaybackFramePacing::ShouldWaitForAudio(1'050'000, 1'000'000, true));
    assert(!PlaybackFramePacing::ShouldWaitForAudio(1'130'000, 1'000'000, true));
    assert(PlaybackFramePacing::ShouldWaitForAudio(1'130'001, 1'000'000, true));
    assert(!PlaybackFramePacing::ShouldWaitForAudio(1'500'000, 1'000'000, false));
    assert(PlaybackFramePacing::AudioAheadWaitDuration(1'333'333, 1'000'000, true).count() == 20'333);
    assert(PlaybackFramePacing::AudioAheadWaitDuration(1'050'000, 1'000'000, true).count() == 0);
    assert(PlaybackFramePacing::AudioAheadWaitDuration(1'130'000, 1'000'000, true).count() == 0);
    assert(PlaybackFramePacing::AudioAheadWaitDuration(1'333'333, 1'000'000, false).count() == 0);
    assert(PlaybackFramePacing::AccumulateAudioAheadWaitTargetMs(0.0, std::chrono::microseconds(20'000)) == 20.0);
    assert(PlaybackFramePacing::AccumulateAudioAheadWaitTargetMs(20.0, std::chrono::microseconds(5'500)) == 25.5);
    assert(PlaybackFramePacing::AccumulateAudioAheadWaitTargetMs(-1.0, std::chrono::microseconds(5'000)) == 5.0);
    assert(PlaybackFramePacing::AccumulateAudioAheadWaitTargetMs(20.0, std::chrono::microseconds(0)) == 20.0);
    assert(PlaybackFramePacing::AccumulateAudioAheadWaitTargetMs(20.0, std::chrono::microseconds(-5'000)) == 20.0);

    auto completedEpisodeGeneration = uint64_t{7};
    auto currentEpisodeGeneration = completedEpisodeGeneration;
    assert(PlaybackFramePacing::ShouldRecordAudioAheadWaitPass(
        completedEpisodeGeneration,
        currentEpisodeGeneration,
        true));
    ++currentEpisodeGeneration;
    assert(!PlaybackFramePacing::ShouldRecordAudioAheadWaitPass(
        completedEpisodeGeneration,
        currentEpisodeGeneration,
        true));
    assert(!PlaybackFramePacing::ShouldRecordAudioAheadWaitPass(
        currentEpisodeGeneration,
        currentEpisodeGeneration,
        false));

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
    assert(PlaybackFramePacing::VideoClockWaitDuration(2'000'000, 0, 0).count() == 10'000);
    assert(PlaybackFramePacing::VideoClockWaitDuration(1'000'000, 0, 950'000).count() == 0);
    assert(PlaybackFramePacing::VideoClockWaitDuration(0, 0, 0).count() == 0);
}
