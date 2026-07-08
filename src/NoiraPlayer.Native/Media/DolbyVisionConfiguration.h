#pragma once

#include <cstddef>
#include <cstdint>
#include <optional>

namespace winrt::NoiraPlayer::Native::implementation
{
    struct DolbyVisionConfiguration
    {
        bool IsPresent{false};
        uint8_t Profile{0};
        uint8_t Level{0};
        bool RpuPresent{false};
        bool EnhancementLayerPresent{false};
        bool BaseLayerPresent{false};
        uint8_t BaseLayerSignalCompatibilityId{0};
    };

    inline std::optional<DolbyVisionConfiguration> TryParseDolbyVisionConfigurationRecord(
        uint8_t const* data,
        size_t size) noexcept
    {
        if (data == nullptr || size < 8)
        {
            return std::nullopt;
        }

        DolbyVisionConfiguration configuration{};
        configuration.IsPresent = true;
        configuration.Profile = data[2];
        configuration.Level = data[3];
        configuration.RpuPresent = data[4] != 0;
        configuration.EnhancementLayerPresent = data[5] != 0;
        configuration.BaseLayerPresent = data[6] != 0;
        configuration.BaseLayerSignalCompatibilityId = data[7];
        return configuration;
    }

    inline bool IsUnsupportedPureDolbyVision(DolbyVisionConfiguration const& configuration) noexcept
    {
        return configuration.IsPresent &&
            configuration.Profile == 5 &&
            configuration.BaseLayerSignalCompatibilityId == 0;
    }
}
