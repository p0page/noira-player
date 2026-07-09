#include <cassert>
#include <chrono>

#include "Media/RenderLoopWaiter.h"

using namespace std::chrono_literals;
using winrt::NoiraPlayer::Native::implementation::RenderLoopWaiter;

int main()
{
    RenderLoopWaiter waiter;
    assert(waiter.WaitFor(0ms));

    auto const startedAt = std::chrono::steady_clock::now();
    assert(waiter.WaitFor(1ms));
    auto const elapsed = std::chrono::duration<double, std::milli>(
        std::chrono::steady_clock::now() - startedAt).count();

    assert(elapsed >= 0.5);
    assert(elapsed < 20.0);
}
