#pragma once

#include <cmath>

namespace winrt::NextGenEmby::Native::implementation
{
    class HdrDisplayRefreshRatePolicy
    {
    public:
        static constexpr double MatchTolerance = 0.15;
        static constexpr double NoMatchWeight = 1000000.0;
        static constexpr double FractionalCadencePenalty = 0.1;

        static bool HasUsableVideoFrameRate(double videoFrameRate) noexcept
        {
            return std::isfinite(videoFrameRate) &&
                videoFrameRate >= 5.0 &&
                videoFrameRate <= 120.0;
        }

        static bool MatchesVideoFrameRate(double displayRefreshRate, double videoFrameRate) noexcept
        {
            return RefreshWeight(displayRefreshRate, videoFrameRate) < NoMatchWeight;
        }

        static bool IsBetterRefreshRateForVideo(
            double candidateRefreshRate,
            double selectedRefreshRate,
            double videoFrameRate) noexcept
        {
            return RefreshWeight(candidateRefreshRate, videoFrameRate) <
                RefreshWeight(selectedRefreshRate, videoFrameRate);
        }

        static double RefreshWeight(double displayRefreshRate, double videoFrameRate) noexcept
        {
            if (!HasUsableVideoFrameRate(videoFrameRate) ||
                !std::isfinite(displayRefreshRate) ||
                displayRefreshRate <= 0.0)
            {
                return NoMatchWeight;
            }

            auto bestWeight = NoMatchWeight;
            constexpr double SupportedRatios[] = {1.0, 2.0, 2.5, 3.0, 4.0, 5.0};
            for (auto ratio : SupportedRatios)
            {
                auto targetRefreshRate = videoFrameRate * ratio;
                auto difference = std::fabs(displayRefreshRate - targetRefreshRate);
                if (difference <= MatchTolerance)
                {
                    auto weight = difference / videoFrameRate;
                    if (IsFractionalMultiplier(ratio))
                    {
                        weight += FractionalCadencePenalty;
                    }

                    if (displayRefreshRate <= 30.0 &&
                        !IsCinemaLowRefresh(displayRefreshRate))
                    {
                        weight += 1.0;
                    }

                    if (displayRefreshRate > 60.0 && ratio > 1.0)
                    {
                        weight += ratio / 10000.0;
                    }

                    if (weight < bestWeight)
                    {
                        bestWeight = weight;
                    }
                }
            }

            return bestWeight;
        }

    private:
        static bool IsCinemaLowRefresh(double displayRefreshRate) noexcept
        {
            return std::fabs(displayRefreshRate - 24.0) <= MatchTolerance ||
                std::fabs(displayRefreshRate - (24.0 / 1.001)) <= MatchTolerance;
        }

        static bool IsFractionalMultiplier(double ratio) noexcept
        {
            return std::fabs(ratio - std::round(ratio)) > 0.001;
        }
    };
}
