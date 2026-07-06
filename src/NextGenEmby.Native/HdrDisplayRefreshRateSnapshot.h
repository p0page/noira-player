#pragma once

#include <cmath>

namespace winrt::NextGenEmby::Native::implementation
{
    class HdrDisplayRefreshRateSnapshot
    {
    public:
        static double Normalize(double refreshRateHz) noexcept
        {
            if (!std::isfinite(refreshRateHz) || refreshRateHz <= 0.0)
            {
                return 0.0;
            }

            return refreshRateHz;
        }
    };
}
