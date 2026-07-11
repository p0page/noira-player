#include <cassert>
#include <cstdint>

#include "Media/SubtitleBitmap.h"

using namespace winrt::NoiraPlayer::Native::implementation;

int main()
{
    uint8_t indexed[] =
    {
        1, 0, 99, 99,
        2, 1, 99, 99
    };
    uint32_t palette[] =
    {
        0x00000000,
        0x80ff0000,
        0xff00ff00
    };

    auto region = TryConvertIndexedSubtitleBitmap(
        indexed,
        4,
        palette,
        3,
        100,
        900,
        2,
        2,
        1920,
        1080);
    assert(region.has_value());
    assert(region->Stride == 8);
    assert(region->BgraPixels.size() == 16);

    // Palette values are AARRGGBB. D2D consumes premultiplied BGRA bytes.
    assert(region->BgraPixels[0] == 0);
    assert(region->BgraPixels[1] == 0);
    assert(region->BgraPixels[2] == 128);
    assert(region->BgraPixels[3] == 128);
    assert(region->BgraPixels[4] == 0);
    assert(region->BgraPixels[5] == 0);
    assert(region->BgraPixels[6] == 0);
    assert(region->BgraPixels[7] == 0);
    assert(region->BgraPixels[8] == 0);
    assert(region->BgraPixels[9] == 255);
    assert(region->BgraPixels[10] == 0);
    assert(region->BgraPixels[11] == 255);

    auto destination = MapSubtitleRegionToContainedVideo(*region, 1920, 1200);
    assert(destination.Left == 100.0f);
    assert(destination.Top == 960.0f);
    assert(destination.Right == 102.0f);
    assert(destination.Bottom == 962.0f);

    uint8_t invalidIndex[] = {3};
    assert(!TryConvertIndexedSubtitleBitmap(
        invalidIndex,
        1,
        palette,
        3,
        0,
        0,
        1,
        1,
        1920,
        1080));

    assert(!TryConvertIndexedSubtitleBitmap(
        indexed,
        1,
        palette,
        3,
        0,
        0,
        2,
        2,
        1920,
        1080));

    return 0;
}
