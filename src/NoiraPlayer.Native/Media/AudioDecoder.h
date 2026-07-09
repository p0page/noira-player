#pragma once

#include "FfmpegMediaSource.h"

#include <cstdint>
#include <optional>
#include <vector>

struct AVCodecContext;
struct SwrContext;

namespace winrt::NoiraPlayer::Native::implementation
{
    enum class AudioSampleFormat
    {
        Unknown,
        UInt8,
        Int16,
        Int32,
        Float,
        Double
    };

    struct DecodedAudioFrame
    {
        uint32_t SampleRate{0};
        uint32_t ChannelCount{0};
        uint32_t SampleCount{0};
        AudioSampleFormat Format{AudioSampleFormat::Unknown};
        int64_t PositionTicks{0};
        std::vector<uint8_t> PcmData;
    };

    class AudioDecoder
    {
    public:
        void Open(
            FfmpegMediaSource& mediaSource,
            int32_t selectedAudioStreamIndex,
            bool hasSelection);
        std::optional<DecodedAudioFrame> TryReadFrame();
        void Flush(int64_t positionTicks);
        void Close() noexcept;
        bool IsOpen() const noexcept;

    private:
        FfmpegMediaSource* m_mediaSource{nullptr};
        AVCodecContext* m_codecContext{nullptr};
        SwrContext* m_resampler{nullptr};
        int32_t m_audioStreamIndex{-1};
        int64_t m_positionTicks{0};
        bool m_decoderDraining{false};
        bool m_open{false};
    };
}
