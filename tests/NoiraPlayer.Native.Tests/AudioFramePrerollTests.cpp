#include <cassert>

#include "Media/AudioFramePreroll.h"

using winrt::NoiraPlayer::Native::implementation::AudioFramePreroll;

int main()
{
    AudioFramePreroll preroll;
    preroll.Reset(600'000'000);

    assert(!preroll.Accept(540'000'000, 960, 48'000).has_value());
    assert(!preroll.Accept(599'700'000, 960, 48'000).has_value());
    assert(preroll.Accept(599'900'000, 960, 48'000) == 600'000'000);
    assert(preroll.Accept(600'200'000, 960, 48'000) == 600'200'000);

    preroll.Reset(600'000'000);
    assert(preroll.Accept(599'900'000, 960, 48'000) == 600'000'000);
    assert(preroll.Accept(600'100'000, 960, 48'000) == 600'100'000);

    preroll.Reset(0);
    assert(preroll.Accept(100'000, 960, 48'000) == 100'000);

    return 0;
}
