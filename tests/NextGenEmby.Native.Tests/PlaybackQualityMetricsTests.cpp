#include <cassert>

#include "Media/PlaybackQualityMetrics.h"

using winrt::NextGenEmby::Native::implementation::PlaybackQualityMetrics;
using winrt::NextGenEmby::Native::implementation::PlaybackQualityMetricsSnapshot;

int main()
{
    PlaybackQualityMetrics metrics;
    metrics.RecordRenderIntervalMs(41.0);
    metrics.RecordRenderIntervalMs(42.0);
    metrics.RecordRenderIntervalMs(100.0);
    metrics.RecordPresentDurationMs(2.0);
    metrics.RecordPresentDurationMs(17.0);
    metrics.RecordPresentDurationMs(34.0);
    metrics.RecordAudioVideoDriftTicks(200000);
    metrics.RecordAudioVideoDriftTicks(-400000);

    metrics.RenderPasses = 10;
    metrics.DecodedVideoFrames = 4;
    metrics.RenderedVideoFrames = 3;
    metrics.DroppedVideoFrames = 1;
    metrics.VideoStarvedPasses = 2;
    metrics.AudioStarvedPasses = 1;
    metrics.AudioClockTicks = 2'000'000;
    metrics.VideoPositionTicks = 1'800'000;
    metrics.FramePacingSourceFrameRate = 60.0;
    metrics.LateFrameDropToleranceMs = 41.6667;

    PlaybackQualityMetricsSnapshot snapshot = metrics.Snapshot();

    assert(snapshot.RenderPasses == 10);
    assert(snapshot.DecodedVideoFrames == 4);
    assert(snapshot.RenderedVideoFrames == 3);
    assert(snapshot.DroppedVideoFrames == 1);
    assert(snapshot.VideoStarvedPasses == 2);
    assert(snapshot.AudioStarvedPasses == 1);
    assert(snapshot.RenderIntervalMsP50 >= 41.0);
    assert(snapshot.RenderIntervalMsP95 >= 100.0);
    assert(snapshot.MaxFrameGapMs == 100.0);
    assert(snapshot.PresentDurationMsP50 >= 2.0);
    assert(snapshot.PresentDurationMsP95 >= 34.0);
    assert(snapshot.PresentDurationMsMax == 34.0);
    assert(snapshot.AudioVideoDriftMsP95 >= 40.0);
    assert(snapshot.AudioVideoDriftMsMax >= 40.0);
    assert(snapshot.AudioClockTicks == 2'000'000);
    assert(snapshot.VideoPositionTicks == 1'800'000);
    assert(snapshot.FramePacingSourceFrameRate == 60.0);
    assert(snapshot.LateFrameDropToleranceMs > 41.0);

    metrics.Reset();
    snapshot = metrics.Snapshot();
    assert(snapshot.RenderPasses == 0);
    assert(snapshot.MaxFrameGapMs == 0.0);
    assert(snapshot.PresentDurationMsMax == 0.0);
    assert(snapshot.AudioVideoDriftMsMax == 0.0);
    assert(snapshot.FramePacingSourceFrameRate == 0.0);
    assert(snapshot.LateFrameDropToleranceMs == 0.0);

    return 0;
}
