#pragma once

#include <atomic>
#include <cstdint>
#include <deque>
#include <optional>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>
#include <winrt/Windows.Foundation.h>

#include "MediaTimeline.h"

struct AVFormatContext;
struct AVPacket;
struct AVStream;

namespace winrt::NoiraPlayer::Native::implementation
{
    struct FfmpegVideoStreamSnapshot
    {
        int32_t StreamIndex{-1};
        std::string Codec;
        uint32_t Width{0};
        uint32_t Height{0};
        double FrameRate{0.0};
        std::string HdrKind;
        std::string VideoRange;
        std::string ColorPrimaries;
        std::string ColorTransfer;
        std::string ColorSpace;
        bool IsDolbyVision{false};
        uint32_t DolbyVisionProfile{0};
        uint32_t DolbyVisionCompatibilityId{0};
        bool HasHdr10BaseLayer{false};
        bool HasHlgBaseLayer{false};
    };

    struct FfmpegStreamSnapshot
    {
        int32_t StreamIndex{-1};
        std::string Kind;
        std::string Codec;
        std::string Language;
        std::string ChannelLayout;
        int32_t Channels{0};
        bool IsDefault{false};
        bool IsForced{false};
        double RealFrameRate{0.0};
        double AverageFrameRate{0.0};
    };

    struct FfmpegTimelineSnapshot
    {
        int64_t ContainerStartTimeTicks{0};
        int64_t StreamStartTimeTicks{0};
        int64_t LogicalDurationTicks{0};
        int64_t LastSeekDemuxTargetTicks{-1};
    };

    struct FfmpegOpenTimingSnapshot
    {
        double OpenInputDurationMs{0.0};
        double StreamInfoDurationMs{0.0};
    };

    struct FfmpegReadTimingSnapshot
    {
        double ReadFrameDurationMs{0.0};
        uint64_t PacketCount{0};
        uint64_t Bytes{0};
    };

    struct FfmpegSwitchPacketCacheSnapshot
    {
        bool HasCoverage{false};
        uint64_t PacketCount{0};
        uint64_t Bytes{0};
        int64_t WindowDurationTicks{0};
    };

    class FfmpegMediaSource
    {
    public:
        void Open(winrt::hstring const& url);
        void Close() noexcept;
        void Interrupt() noexcept;

        std::optional<int32_t> TryFindStream(int mediaType, int32_t selectedStreamIndex) const;
        int32_t FindRequiredStream(int mediaType, int32_t selectedStreamIndex) const;
        AVStream* Stream(int32_t streamIndex) const;
        std::optional<FfmpegVideoStreamSnapshot> BestVideoStreamSnapshot() const;
        std::vector<FfmpegStreamSnapshot> StreamSnapshots() const;
        FfmpegTimelineSnapshot TimelineSnapshot(int32_t streamIndex) const;
        FfmpegOpenTimingSnapshot OpenTimingSnapshot() const noexcept;
        FfmpegReadTimingSnapshot ReadTimingSnapshot() const noexcept;
        int64_t NormalizeTimestampTicks(int64_t demuxTicks) const noexcept;
        void RegisterStream(int32_t streamIndex);
        void UnregisterStream(int32_t streamIndex) noexcept;
        void ConfigureSwitchPacketCache(std::vector<int32_t> const& streamIndexes);
        FfmpegSwitchPacketCacheSnapshot SwitchPacketCacheSnapshot(
            int32_t streamIndex,
            int64_t positionTicks,
            bool requirePacketAtOrAfter) const;
        bool TryReadPacket(int32_t streamIndex, AVPacket* packet);
        bool TryReadQueuedPacket(int32_t streamIndex, AVPacket* packet);
        void Seek(int32_t streamIndex, int64_t positionTicks);

    private:
        static int InterruptCallback(void* opaque) noexcept;
        void BeginBlockingIo(int64_t timeoutMilliseconds) noexcept;
        void ClearPacketQueues() noexcept;
        bool TryTakeQueuedPacket(int32_t streamIndex, AVPacket* packet);
        bool ShouldQueueStream(int32_t streamIndex) const;
        void QueuePacket(AVPacket* packet);
        void TrimSwitchPacketCache(int32_t streamIndex) noexcept;
        std::optional<int64_t> PacketPositionTicks(AVPacket const* packet) const noexcept;

        winrt::hstring m_url;
        AVFormatContext* m_formatContext{nullptr};
        std::unordered_set<int32_t> m_activeStreams;
        std::unordered_set<int32_t> m_switchCacheStreams;
        std::unordered_map<int32_t, std::deque<AVPacket*>> m_packetQueues;
        std::unordered_map<int32_t, uint64_t> m_packetQueueBytes;
        uint32_t m_avformatVersion{0};
        std::atomic<bool> m_interruptRequested{false};
        std::atomic<int64_t> m_ioDeadlineNanoseconds{0};
        MediaTimeline m_timeline;
        int64_t m_lastSeekDemuxTargetTicks{-1};
        FfmpegOpenTimingSnapshot m_openTiming;
        FfmpegReadTimingSnapshot m_readTiming;
        bool m_open{false};
    };
}
