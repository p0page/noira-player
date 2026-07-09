#include <cassert>

#include "HdrDisplayRefreshRatePolicy.h"

using winrt::NoiraPlayer::Native::implementation::HdrDisplayRefreshRatePolicy;

int main()
{
    assert(HdrDisplayRefreshRatePolicy::HasUsableVideoFrameRate(23.976));
    assert(HdrDisplayRefreshRatePolicy::HasUsableVideoFrameRate(24.0));
    assert(!HdrDisplayRefreshRatePolicy::HasUsableVideoFrameRate(0.0));

    assert(HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(23.976024, 23.976));
    assert(HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(24.0, 23.976));
    assert(HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(50.0, 25.0));
    assert(HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(59.94006, 23.976));
    assert(HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(60.0, 24.0));
    assert(HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(59.94006, 29.97003));
    assert(HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(60.0, 30.0));
    assert(HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(119.88012, 23.976));
    assert(HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(120.0, 24.0));
    assert(HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(100.0, 25.0));
    assert(HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(120.0, 30.0));

    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(23.976024, 59.94006, 23.976));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(24.0, 59.94006, 23.976));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(24.0, 60.0, 24.0));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(59.94006, 60.0, 23.976));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(60.0, 59.94006, 24.0));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(119.88012, 120.0, 23.976));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(50.0, 59.94006, 25.0));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(50.0, 25.0, 25.0));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(59.94006, 29.97003, 29.97003));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(60.0, 30.0, 30.0));
    assert(!HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(59.94006, 23.976024, 23.976));

    assert(HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(23.976) == 23.976024);
    assert(HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(24.0) == 24.0);
    assert(HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(30.0) == 60.0);
    assert(HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(60.0) == 60.0);
    assert(HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(0.0) == 0.0);
}
