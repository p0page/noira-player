#pragma once

#include <cstdint>
#include <memory>
#include <string>
#include <unordered_set>
#include <vector>

#pragma warning(push)
#pragma warning(disable : 4244 4819)
extern "C"
{
#include <libavcodec/packet.h>
}
#pragma warning(pop)

namespace winrt::NoiraPlayer::Native::implementation
{
    struct FfmpegPacketDeleter
    {
        void operator()(AVPacket* packet) const noexcept;
    };

    using FfmpegPacketPtr = std::unique_ptr<AVPacket, FfmpegPacketDeleter>;

    struct FfmpegSeekReplayCacheLimits
    {
        uint64_t MaxBytes{48ULL * 1024ULL * 1024ULL};
        uint64_t MaxPackets{32768};
        int64_t MaxDurationTicks{12LL * 10'000'000LL};
    };

    struct FfmpegSeekReplaySnapshot
    {
        bool Enabled{false};
        uint64_t PacketCount{0};
        uint64_t Bytes{0};
        int64_t WindowDurationTicks{0};
    };

    struct FfmpegSeekReplayPacket
    {
        int32_t StreamIndex{-1};
        int64_t PositionTicks{0};
        uint64_t Sequence{0};
        FfmpegPacketPtr Packet;
    };

    struct FfmpegSeekReplayResult
    {
        bool Enabled{false};
        bool Hit{false};
        uint64_t PacketCount{0};
        uint64_t Bytes{0};
        int64_t WindowDurationTicks{0};
        std::string FallbackReason;
        std::vector<FfmpegSeekReplayPacket> Packets;
    };

    class FfmpegSeekReplayCache
    {
    public:
        explicit FfmpegSeekReplayCache(FfmpegSeekReplayCacheLimits limits = {});

        void Configure(
            bool enabled,
            std::vector<int32_t> const& activeStreamIndexes,
            int32_t videoStreamIndex);
        void ObservePacket(AVPacket const* packet, int64_t positionTicks);
        FfmpegSeekReplayResult TryBuildReplay(
            int64_t targetPositionTicks,
            int64_t currentPositionTicks) const;
        FfmpegSeekReplaySnapshot Snapshot() const noexcept;
        void Clear() noexcept;

    private:
        struct Entry
        {
            int32_t StreamIndex{-1};
            int64_t PositionTicks{0};
            uint64_t Sequence{0};
            bool IsVideoKeyFrame{false};
            uint64_t Bytes{0};
            FfmpegPacketPtr Packet;
        };

        bool LimitsExceeded() const noexcept;
        void Prune() noexcept;

        FfmpegSeekReplayCacheLimits m_limits;
        std::unordered_set<int32_t> m_activeStreams;
        std::vector<Entry> m_entries;
        int32_t m_videoStreamIndex{-1};
        uint64_t m_nextSequence{0};
        uint64_t m_bytes{0};
        bool m_enabled{false};
    };
}
