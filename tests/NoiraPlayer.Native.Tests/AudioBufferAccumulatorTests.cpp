#include <cassert>
#include <cstdint>
#include <vector>

#include "Media/AudioBufferAccumulator.h"

using winrt::NoiraPlayer::Native::implementation::AudioBufferAccumulator;

namespace
{
    constexpr uint32_t BytesPerSampleFrame = 8;

    std::vector<uint8_t> Pcm(uint32_t sampleCount, uint8_t value)
    {
        return std::vector<uint8_t>(
            static_cast<size_t>(sampleCount) * BytesPerSampleFrame,
            value);
    }
}

int main()
{
    AudioBufferAccumulator accumulator;

    for (uint32_t frame = 0; frame < 23; ++frame)
    {
        auto ready = accumulator.Append(Pcm(40, static_cast<uint8_t>(frame)), 40, 1'000'000 + frame);
        assert(!ready.has_value());
    }

    auto coalesced = accumulator.Append(Pcm(40, 23), 40, 1'000'023);
    assert(coalesced.has_value());
    assert(coalesced->SampleCount == AudioBufferAccumulator::TargetSampleCount);
    assert(coalesced->PositionTicks == 1'000'000);
    assert(coalesced->PcmData.size() ==
        static_cast<size_t>(AudioBufferAccumulator::TargetSampleCount) * BytesPerSampleFrame);
    assert(!accumulator.Drain().has_value());

    auto completeFrame = accumulator.Append(Pcm(1'024, 7), 1'024, 2'000'000);
    assert(completeFrame.has_value());
    assert(completeFrame->SampleCount == 1'024);
    assert(completeFrame->PositionTicks == 2'000'000);

    assert(!accumulator.Append({}, 0, 3'000'000).has_value());
    assert(!accumulator.Append(Pcm(120, 9), 120, 3'000'000).has_value());
    auto tail = accumulator.Drain();
    assert(tail.has_value());
    assert(tail->SampleCount == 120);
    assert(tail->PositionTicks == 3'000'000);

    assert(!accumulator.Append(Pcm(40, 1), 40, 4'000'000).has_value());
    accumulator.Reset();
    assert(!accumulator.Drain().has_value());
}
