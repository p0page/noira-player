#include <cassert>

#include "HdrDisplayRefreshRatePolicy.h"

using winrt::NextGenEmby::Native::implementation::HdrDisplayRefreshRatePolicy;

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

    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(23.976024, 59.94006, 23.976));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(59.94006, 60.0, 23.976));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(60.0, 59.94006, 24.0));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(50.0, 59.94006, 25.0));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(50.0, 25.0, 25.0));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(59.94006, 29.97003, 29.97003));
    assert(HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(60.0, 30.0, 30.0));
    assert(!HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(59.94006, 23.976024, 23.976));
}
