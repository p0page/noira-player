#pragma once

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <optional>
#include <vector>

namespace winrt::NoiraPlayer::Native::implementation
{
    struct SubtitleBitmapRegion
    {
        int32_t X{0};
        int32_t Y{0};
        uint32_t Width{0};
        uint32_t Height{0};
        uint32_t CanvasWidth{0};
        uint32_t CanvasHeight{0};
        uint32_t Stride{0};
        std::vector<uint8_t> BgraPixels;
    };

    struct SubtitleDestinationRect
    {
        float Left{0.0f};
        float Top{0.0f};
        float Right{0.0f};
        float Bottom{0.0f};
    };

    inline std::optional<SubtitleBitmapRegion> TryConvertIndexedSubtitleBitmap(
        uint8_t const* indexedPixels,
        uint32_t indexedStride,
        uint32_t const* palette,
        uint32_t paletteColorCount,
        int32_t x,
        int32_t y,
        uint32_t width,
        uint32_t height,
        uint32_t canvasWidth,
        uint32_t canvasHeight)
    {
        if (indexedPixels == nullptr ||
            palette == nullptr ||
            width == 0 ||
            height == 0 ||
            indexedStride < width ||
            paletteColorCount == 0 ||
            canvasWidth == 0 ||
            canvasHeight == 0 ||
            width > (std::numeric_limits<uint32_t>::max)() / 4)
        {
            return std::nullopt;
        }

        auto stride = width * 4;
        if (height > (std::numeric_limits<size_t>::max)() / stride)
        {
            return std::nullopt;
        }

        SubtitleBitmapRegion region;
        region.X = x;
        region.Y = y;
        region.Width = width;
        region.Height = height;
        region.CanvasWidth = canvasWidth;
        region.CanvasHeight = canvasHeight;
        region.Stride = stride;
        region.BgraPixels.resize(static_cast<size_t>(stride) * height);

        for (auto row = uint32_t{0}; row < height; ++row)
        {
            auto source = indexedPixels + static_cast<size_t>(row) * indexedStride;
            auto destination = region.BgraPixels.data() + static_cast<size_t>(row) * stride;
            for (auto column = uint32_t{0}; column < width; ++column)
            {
                auto paletteIndex = source[column];
                if (paletteIndex >= paletteColorCount)
                {
                    return std::nullopt;
                }

                auto color = palette[paletteIndex];
                auto alpha = static_cast<uint8_t>((color >> 24) & 0xff);
                auto red = static_cast<uint8_t>((color >> 16) & 0xff);
                auto green = static_cast<uint8_t>((color >> 8) & 0xff);
                auto blue = static_cast<uint8_t>(color & 0xff);
                auto premultiply = [alpha](uint8_t channel)
                {
                    return static_cast<uint8_t>(
                        (static_cast<uint32_t>(channel) * alpha + 127) / 255);
                };

                destination[column * 4] = premultiply(blue);
                destination[column * 4 + 1] = premultiply(green);
                destination[column * 4 + 2] = premultiply(red);
                destination[column * 4 + 3] = alpha;
            }
        }

        return region;
    }

    inline SubtitleDestinationRect MapSubtitleRegionToContainedVideo(
        SubtitleBitmapRegion const& region,
        uint32_t targetWidth,
        uint32_t targetHeight) noexcept
    {
        if (region.CanvasWidth == 0 ||
            region.CanvasHeight == 0 ||
            targetWidth == 0 ||
            targetHeight == 0)
        {
            return {};
        }

        auto scale = (std::min)(
            static_cast<float>(targetWidth) / region.CanvasWidth,
            static_cast<float>(targetHeight) / region.CanvasHeight);
        auto videoWidth = region.CanvasWidth * scale;
        auto videoHeight = region.CanvasHeight * scale;
        auto offsetX = (static_cast<float>(targetWidth) - videoWidth) * 0.5f;
        auto offsetY = (static_cast<float>(targetHeight) - videoHeight) * 0.5f;

        return SubtitleDestinationRect{
            offsetX + region.X * scale,
            offsetY + region.Y * scale,
            offsetX + (region.X + static_cast<int64_t>(region.Width)) * scale,
            offsetY + (region.Y + static_cast<int64_t>(region.Height)) * scale};
    }
}
