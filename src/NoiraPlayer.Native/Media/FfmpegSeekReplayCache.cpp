#include "pch.h"
#include "FfmpegSeekReplayCache.h"

#include <algorithm>
#include <limits>

namespace winrt::NoiraPlayer::Native::implementation
{
    namespace
    {
        constexpr int64_t CoverageToleranceTicks = 250LL * 10'000LL;

        uint64_t PacketBytes(AVPacket const* packet) noexcept
        {
            return packet != nullptr && packet->size > 0
                ? static_cast<uint64_t>(packet->size)
                : 0;
        }
    }

    void FfmpegPacketDeleter::operator()(AVPacket* packet) const noexcept
    {
        av_packet_free(&packet);
    }

    FfmpegSeekReplayCache::FfmpegSeekReplayCache(FfmpegSeekReplayCacheLimits limits)
        : m_limits(limits)
    {
    }

    void FfmpegSeekReplayCache::Configure(
        bool enabled,
        std::vector<int32_t> const& activeStreamIndexes,
        int32_t videoStreamIndex)
    {
        auto nextActiveStreams = std::unordered_set<int32_t>(
            activeStreamIndexes.begin(),
            activeStreamIndexes.end());
        auto configurationChanged =
            m_enabled != enabled ||
            m_videoStreamIndex != videoStreamIndex ||
            m_activeStreams != nextActiveStreams;

        m_enabled = enabled;
        m_videoStreamIndex = videoStreamIndex;
        m_activeStreams = std::move(nextActiveStreams);
        if (configurationChanged || !m_enabled)
        {
            Clear();
        }
    }

    void FfmpegSeekReplayCache::ObservePacket(AVPacket const* packet, int64_t positionTicks)
    {
        if (!m_enabled ||
            packet == nullptr ||
            positionTicks < 0 ||
            m_activeStreams.find(packet->stream_index) == m_activeStreams.end())
        {
            return;
        }

        auto clonedPacket = FfmpegPacketPtr(av_packet_clone(packet));
        if (!clonedPacket)
        {
            Clear();
            return;
        }

        auto bytes = PacketBytes(clonedPacket.get());
        m_entries.push_back(Entry
        {
            packet->stream_index,
            positionTicks,
            m_nextSequence++,
            packet->stream_index == m_videoStreamIndex &&
                (packet->flags & AV_PKT_FLAG_KEY) != 0,
            bytes,
            std::move(clonedPacket)
        });
        m_bytes += bytes;
        Prune();
    }

    FfmpegSeekReplayResult FfmpegSeekReplayCache::TryBuildReplay(
        int64_t targetPositionTicks,
        int64_t currentPositionTicks) const
    {
        FfmpegSeekReplayResult result;
        result.Enabled = m_enabled;
        if (!m_enabled)
        {
            result.FallbackReason = "disabled";
            return result;
        }

        if (m_entries.empty())
        {
            result.FallbackReason = "empty";
            return result;
        }

        auto earliestPosition = m_entries.front().PositionTicks;
        auto latestPosition = m_entries.front().PositionTicks;
        for (auto const& entry : m_entries)
        {
            earliestPosition = (std::min)(earliestPosition, entry.PositionTicks);
            latestPosition = (std::max)(latestPosition, entry.PositionTicks);
        }

        if (targetPositionTicks < earliestPosition || targetPositionTicks > latestPosition)
        {
            result.FallbackReason = "target-outside-window";
            return result;
        }

        auto replayStart = m_entries.end();
        for (auto entry = m_entries.begin(); entry != m_entries.end(); ++entry)
        {
            if (entry->IsVideoKeyFrame && entry->PositionTicks <= targetPositionTicks)
            {
                replayStart = entry;
            }
        }

        if (replayStart == m_entries.end())
        {
            result.FallbackReason = "video-keyframe-missing";
            return result;
        }

        if (currentPositionTicks < targetPositionTicks ||
            currentPositionTicks > latestPosition + CoverageToleranceTicks)
        {
            result.FallbackReason = "stream-coverage-missing";
            return result;
        }

        auto coveredStreams = std::unordered_set<int32_t>();
        for (auto entry = replayStart; entry != m_entries.end(); ++entry)
        {
            coveredStreams.insert(entry->StreamIndex);
        }

        for (auto streamIndex : m_activeStreams)
        {
            if (coveredStreams.find(streamIndex) == coveredStreams.end())
            {
                result.FallbackReason = "stream-coverage-missing";
                return result;
            }
        }

        result.Packets.reserve(static_cast<size_t>(m_entries.end() - replayStart));
        for (auto entry = replayStart; entry != m_entries.end(); ++entry)
        {
            auto clonedPacket = FfmpegPacketPtr(av_packet_clone(entry->Packet.get()));
            if (!clonedPacket)
            {
                result.Packets.clear();
                result.PacketCount = 0;
                result.Bytes = 0;
                result.WindowDurationTicks = 0;
                result.FallbackReason = "allocation-failed";
                return result;
            }

            result.Bytes += entry->Bytes;
            result.Packets.push_back(FfmpegSeekReplayPacket
            {
                entry->StreamIndex,
                entry->PositionTicks,
                entry->Sequence,
                std::move(clonedPacket)
            });
        }

        result.Hit = true;
        result.PacketCount = static_cast<uint64_t>(result.Packets.size());
        result.WindowDurationTicks = (std::max<int64_t>)(
            0,
            latestPosition - replayStart->PositionTicks);
        return result;
    }

    FfmpegSeekReplaySnapshot FfmpegSeekReplayCache::Snapshot() const noexcept
    {
        FfmpegSeekReplaySnapshot snapshot;
        snapshot.Enabled = m_enabled;
        snapshot.PacketCount = static_cast<uint64_t>(m_entries.size());
        snapshot.Bytes = m_bytes;
        if (m_entries.empty())
        {
            return snapshot;
        }

        auto earliestPosition = m_entries.front().PositionTicks;
        auto latestPosition = m_entries.front().PositionTicks;
        for (auto const& entry : m_entries)
        {
            earliestPosition = (std::min)(earliestPosition, entry.PositionTicks);
            latestPosition = (std::max)(latestPosition, entry.PositionTicks);
        }
        snapshot.WindowDurationTicks = (std::max<int64_t>)(0, latestPosition - earliestPosition);
        return snapshot;
    }

    void FfmpegSeekReplayCache::Clear() noexcept
    {
        m_entries.clear();
        m_bytes = 0;
    }

    bool FfmpegSeekReplayCache::LimitsExceeded() const noexcept
    {
        if (m_entries.size() > m_limits.MaxPackets || m_bytes > m_limits.MaxBytes)
        {
            return true;
        }

        auto snapshot = Snapshot();
        return snapshot.WindowDurationTicks > m_limits.MaxDurationTicks;
    }

    void FfmpegSeekReplayCache::Prune() noexcept
    {
        while (!m_entries.empty() && LimitsExceeded())
        {
            auto nextKeyFrame = std::find_if(
                m_entries.begin() + 1,
                m_entries.end(),
                [](Entry const& entry)
                {
                    return entry.IsVideoKeyFrame;
                });
            if (nextKeyFrame == m_entries.end())
            {
                Clear();
                return;
            }

            for (auto entry = m_entries.begin(); entry != nextKeyFrame; ++entry)
            {
                m_bytes = m_bytes > entry->Bytes ? m_bytes - entry->Bytes : 0;
            }
            m_entries.erase(m_entries.begin(), nextKeyFrame);
        }
    }
}
