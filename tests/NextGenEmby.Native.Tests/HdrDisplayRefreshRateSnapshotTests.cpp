#include <cassert>
#include <cmath>

#include "HdrDisplayRefreshRateSnapshot.h"

using winrt::NextGenEmby::Native::implementation::HdrDisplayRefreshRateSnapshot;

int main()
{
    assert(HdrDisplayRefreshRateSnapshot::Normalize(59.94006) == 59.94006);
    assert(HdrDisplayRefreshRateSnapshot::Normalize(24.0) == 24.0);
    assert(HdrDisplayRefreshRateSnapshot::Normalize(0.0) == 0.0);
    assert(HdrDisplayRefreshRateSnapshot::Normalize(-60.0) == 0.0);
    assert(HdrDisplayRefreshRateSnapshot::Normalize(std::nan("")) == 0.0);
    assert(HdrDisplayRefreshRateSnapshot::Normalize(INFINITY) == 0.0);
}
