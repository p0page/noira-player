#pragma once

#include <cstdint>
#include <optional>
#include <utility>
#include <vector>

namespace winrt::NoiraPlayer::Native::implementation
{
    struct AccumulatedAudioBuffer
    {
        std::vector<uint8_t> PcmData;
        uint32_t SampleCount{0};
        int64_t PositionTicks{0};
    };

    class AudioBufferAccumulator
    {
    public:
        static constexpr uint32_t TargetSampleCount = 960;

        std::optional<AccumulatedAudioBuffer> Append(
            std::vector<uint8_t> const& pcmData,
            uint32_t sampleCount,
            int64_t positionTicks)
        {
            if (pcmData.empty() || sampleCount == 0)
            {
                return std::nullopt;
            }

            if (m_pcmData.empty())
            {
                m_positionTicks = positionTicks;
            }

            m_pcmData.insert(m_pcmData.end(), pcmData.begin(), pcmData.end());
            m_sampleCount += sampleCount;
            if (m_sampleCount < TargetSampleCount)
            {
                return std::nullopt;
            }

            return Drain();
        }

        std::optional<AccumulatedAudioBuffer> Drain()
        {
            if (m_pcmData.empty() || m_sampleCount == 0)
            {
                return std::nullopt;
            }

            AccumulatedAudioBuffer buffer;
            buffer.PcmData = std::move(m_pcmData);
            buffer.SampleCount = m_sampleCount;
            buffer.PositionTicks = m_positionTicks;
            m_pcmData.clear();
            m_sampleCount = 0;
            m_positionTicks = 0;
            return buffer;
        }

        void Reset() noexcept
        {
            m_pcmData.clear();
            m_sampleCount = 0;
            m_positionTicks = 0;
        }

    private:
        std::vector<uint8_t> m_pcmData;
        uint32_t m_sampleCount{0};
        int64_t m_positionTicks{0};
    };
}
