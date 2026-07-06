#pragma once

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstddef>
#include <cmath>

namespace winrt::NextGenEmby::Native::implementation
{
    struct PlaybackQualityMetricsSnapshot
    {
        uint64_t RenderPasses{0};
        uint64_t DecodedVideoFrames{0};
        uint64_t RenderedVideoFrames{0};
        uint64_t SubmittedAudioFrames{0};
        uint64_t DroppedVideoFrames{0};
        uint64_t SeekPrerollDroppedFrames{0};
        uint64_t VideoAheadWaitCount{0};
        uint64_t VideoStarvedPasses{0};
        uint64_t AudioStarvedPasses{0};
        uint64_t QueuedAudioBuffers{0};
        int64_t AudioClockTicks{0};
        int64_t VideoPositionTicks{0};
        double RenderIntervalMsP50{0.0};
        double RenderIntervalMsP95{0.0};
        double RenderIntervalMsP99{0.0};
        double MaxFrameGapMs{0.0};
        double AudioVideoDriftMsP50{0.0};
        double AudioVideoDriftMsP95{0.0};
        double AudioVideoDriftMsP99{0.0};
        double AudioVideoDriftMsMax{0.0};
    };

    class PlaybackQualityHistogram
    {
    public:
        void Reset() noexcept
        {
            m_count = 0;
            m_replaceIndex = 0;
            m_max = 0.0;
            m_values.fill(0.0);
        }

        void Add(double value) noexcept
        {
            if (value < 0.0)
            {
                value = -value;
            }

            m_max = (std::max)(m_max, value);
            if (m_count < m_values.size())
            {
                m_values[m_count] = value;
                ++m_count;
                return;
            }

            const auto index = m_replaceIndex % m_values.size();
            m_values[index] = value;
            ++m_replaceIndex;
        }

        double Percentile(double percentile) const noexcept
        {
            if (m_count == 0)
            {
                return 0.0;
            }

            auto copy = m_values;
            const auto count = m_count;
            std::sort(copy.begin(), copy.begin() + static_cast<std::ptrdiff_t>(count));
            const auto clamped = (std::min)((std::max)(percentile, 0.0), 100.0);
            auto position = static_cast<size_t>(std::ceil((clamped / 100.0) * count));
            if (position == 0)
            {
                position = 1;
            }

            return copy[position - 1];
        }

        double Max() const noexcept
        {
            return m_max;
        }

    private:
        std::array<double, 512> m_values{};
        size_t m_count{0};
        size_t m_replaceIndex{0};
        double m_max{0.0};
    };

    class PlaybackQualityMetrics
    {
    public:
        uint64_t RenderPasses{0};
        uint64_t DecodedVideoFrames{0};
        uint64_t RenderedVideoFrames{0};
        uint64_t SubmittedAudioFrames{0};
        uint64_t DroppedVideoFrames{0};
        uint64_t SeekPrerollDroppedFrames{0};
        uint64_t VideoAheadWaitCount{0};
        uint64_t VideoStarvedPasses{0};
        uint64_t AudioStarvedPasses{0};
        uint64_t QueuedAudioBuffers{0};
        int64_t AudioClockTicks{0};
        int64_t VideoPositionTicks{0};

        void Reset() noexcept
        {
            *this = PlaybackQualityMetrics{};
        }

        void RecordRenderIntervalMs(double value) noexcept
        {
            m_renderIntervals.Add(value);
        }

        void RecordAudioVideoDriftTicks(int64_t driftTicks) noexcept
        {
            m_audioVideoDriftMs.Add(static_cast<double>(driftTicks) / 10000.0);
        }

        PlaybackQualityMetricsSnapshot Snapshot() const noexcept
        {
            PlaybackQualityMetricsSnapshot snapshot{};
            snapshot.RenderPasses = RenderPasses;
            snapshot.DecodedVideoFrames = DecodedVideoFrames;
            snapshot.RenderedVideoFrames = RenderedVideoFrames;
            snapshot.SubmittedAudioFrames = SubmittedAudioFrames;
            snapshot.DroppedVideoFrames = DroppedVideoFrames;
            snapshot.SeekPrerollDroppedFrames = SeekPrerollDroppedFrames;
            snapshot.VideoAheadWaitCount = VideoAheadWaitCount;
            snapshot.VideoStarvedPasses = VideoStarvedPasses;
            snapshot.AudioStarvedPasses = AudioStarvedPasses;
            snapshot.QueuedAudioBuffers = QueuedAudioBuffers;
            snapshot.AudioClockTicks = AudioClockTicks;
            snapshot.VideoPositionTicks = VideoPositionTicks;
            snapshot.RenderIntervalMsP50 = m_renderIntervals.Percentile(50);
            snapshot.RenderIntervalMsP95 = m_renderIntervals.Percentile(95);
            snapshot.RenderIntervalMsP99 = m_renderIntervals.Percentile(99);
            snapshot.MaxFrameGapMs = m_renderIntervals.Max();
            snapshot.AudioVideoDriftMsP50 = m_audioVideoDriftMs.Percentile(50);
            snapshot.AudioVideoDriftMsP95 = m_audioVideoDriftMs.Percentile(95);
            snapshot.AudioVideoDriftMsP99 = m_audioVideoDriftMs.Percentile(99);
            snapshot.AudioVideoDriftMsMax = m_audioVideoDriftMs.Max();
            return snapshot;
        }

    private:
        PlaybackQualityHistogram m_renderIntervals;
        PlaybackQualityHistogram m_audioVideoDriftMs;
    };
}
