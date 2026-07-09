#pragma once

#include "NativePlaybackOpenRequest.g.h"

namespace winrt::NoiraPlayer::Native::implementation
{
    struct NativePlaybackOpenRequest : NativePlaybackOpenRequestT<NativePlaybackOpenRequest>
    {
        NativePlaybackOpenRequest() = default;

        winrt::hstring ItemId() const;
        void ItemId(winrt::hstring const& value);
        winrt::hstring MediaSourceId() const;
        void MediaSourceId(winrt::hstring const& value);
        winrt::hstring DirectStreamUrl() const;
        void DirectStreamUrl(winrt::hstring const& value);
        int64_t StartPositionTicks() const noexcept;
        void StartPositionTicks(int64_t value) noexcept;
        int32_t AudioStreamIndex() const noexcept;
        void AudioStreamIndex(int32_t value) noexcept;
        bool HasAudioStreamIndex() const noexcept;
        void HasAudioStreamIndex(bool value) noexcept;
        int32_t SubtitleStreamIndex() const noexcept;
        void SubtitleStreamIndex(int32_t value) noexcept;
        bool HasSubtitleStreamIndex() const noexcept;
        void HasSubtitleStreamIndex(bool value) noexcept;
        double VideoFrameRate() const noexcept;
        void VideoFrameRate(double value) noexcept;

    private:
        winrt::hstring m_itemId;
        winrt::hstring m_mediaSourceId;
        winrt::hstring m_directStreamUrl;
        int64_t m_startPositionTicks{0};
        int32_t m_audioStreamIndex{0};
        bool m_hasAudioStreamIndex{false};
        int32_t m_subtitleStreamIndex{0};
        bool m_hasSubtitleStreamIndex{false};
        double m_videoFrameRate{0.0};
    };
}

namespace winrt::NoiraPlayer::Native::factory_implementation
{
    struct NativePlaybackOpenRequest : NativePlaybackOpenRequestT<NativePlaybackOpenRequest, implementation::NativePlaybackOpenRequest>
    {
    };
}
