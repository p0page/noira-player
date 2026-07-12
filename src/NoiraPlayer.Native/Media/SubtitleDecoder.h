#pragma once

#include "FfmpegMediaSource.h"
#include "SubtitleBitmap.h"

#include <cstdint>
#include <deque>
#include <optional>
#include <string>
#include <vector>

struct AVCodecContext;

namespace winrt::NoiraPlayer::Native::implementation
{
    struct DecodedSubtitleCue
    {
        std::wstring Text;
        std::vector<SubtitleBitmapRegion> BitmapRegions;
        int64_t StartTicks{0};
        int64_t EndTicks{0};
    };

    class SubtitleDecoder
    {
    public:
        void Open(FfmpegMediaSource& mediaSource, int32_t selectedSubtitleStreamIndex);
        void PumpQueuedPackets();
        std::optional<DecodedSubtitleCue> TryGetCueAt(int64_t positionTicks);
        void Flush();
        void Close() noexcept;
        bool IsOpen() const noexcept;
        uint64_t DecodedCueCount() const noexcept;
        std::optional<int32_t> SelectedStreamIndex() const noexcept;

    private:
        FfmpegMediaSource* m_mediaSource{nullptr};
        AVCodecContext* m_codecContext{nullptr};
        std::deque<DecodedSubtitleCue> m_cues;
        int32_t m_subtitleStreamIndex{-1};
        uint64_t m_decodedCueCount{0};
        bool m_open{false};
    };
}
