#pragma once

#include <cstdint>
#include <deque>
#include <optional>
#include <unordered_map>
#include <unordered_set>
#include <winrt/Windows.Foundation.h>

struct AVFormatContext;
struct AVPacket;
struct AVStream;

namespace winrt::NextGenEmby::Native::implementation
{
    class FfmpegMediaSource
    {
    public:
        void Open(winrt::hstring const& url);
        void Close() noexcept;

        std::optional<int32_t> TryFindStream(int mediaType, int32_t selectedStreamIndex) const;
        int32_t FindRequiredStream(int mediaType, int32_t selectedStreamIndex) const;
        AVStream* Stream(int32_t streamIndex) const;
        void RegisterStream(int32_t streamIndex);
        bool TryReadPacket(int32_t streamIndex, AVPacket* packet);
        void Seek(int32_t streamIndex, int64_t timestamp);

    private:
        void ClearPacketQueues() noexcept;
        bool TryTakeQueuedPacket(int32_t streamIndex, AVPacket* packet);
        bool ShouldQueueStream(int32_t streamIndex) const;
        void QueuePacket(AVPacket* packet);

        winrt::hstring m_url;
        AVFormatContext* m_formatContext{nullptr};
        std::unordered_set<int32_t> m_activeStreams;
        std::unordered_map<int32_t, std::deque<AVPacket*>> m_packetQueues;
        uint32_t m_avformatVersion{0};
        bool m_open{false};
    };
}
