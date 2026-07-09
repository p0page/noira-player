#include <cassert>
#include <cstdint>

#include "Media/DolbyVisionConfiguration.h"

using winrt::NoiraPlayer::Native::implementation::IsUnsupportedPureDolbyVision;
using winrt::NoiraPlayer::Native::implementation::TryParseDolbyVisionConfigurationRecord;

int main()
{
    const uint8_t profile5Compat0[] = {1, 0, 5, 6, 1, 0, 1, 0};
    auto profile5 = TryParseDolbyVisionConfigurationRecord(
        profile5Compat0,
        sizeof(profile5Compat0));

    assert(profile5.has_value());
    assert(profile5->Profile == 5);
    assert(profile5->Level == 6);
    assert(profile5->RpuPresent);
    assert(!profile5->EnhancementLayerPresent);
    assert(profile5->BaseLayerPresent);
    assert(profile5->BaseLayerSignalCompatibilityId == 0);
    assert(IsUnsupportedPureDolbyVision(*profile5));

    const uint8_t profile8Compat1[] = {1, 0, 8, 6, 1, 0, 1, 1};
    auto profile8 = TryParseDolbyVisionConfigurationRecord(
        profile8Compat1,
        sizeof(profile8Compat1));

    assert(profile8.has_value());
    assert(profile8->Profile == 8);
    assert(profile8->BaseLayerSignalCompatibilityId == 1);
    assert(!IsUnsupportedPureDolbyVision(*profile8));

    const uint8_t undersized[] = {1, 0, 5};
    assert(!TryParseDolbyVisionConfigurationRecord(undersized, sizeof(undersized)).has_value());
    assert(!TryParseDolbyVisionConfigurationRecord(nullptr, sizeof(profile5Compat0)).has_value());
}
