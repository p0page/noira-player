#include "pch.h"
#include "NativePlaybackOpenRequest.h"
#include "NativePlaybackOpenRequest.g.cpp"

namespace winrt::NextGenEmby::Native::implementation
{
    winrt::hstring NativePlaybackOpenRequest::ItemId() const
    {
        return m_itemId;
    }

    void NativePlaybackOpenRequest::ItemId(winrt::hstring const& value)
    {
        m_itemId = value;
    }

    winrt::hstring NativePlaybackOpenRequest::MediaSourceId() const
    {
        return m_mediaSourceId;
    }

    void NativePlaybackOpenRequest::MediaSourceId(winrt::hstring const& value)
    {
        m_mediaSourceId = value;
    }

    winrt::hstring NativePlaybackOpenRequest::DirectStreamUrl() const
    {
        return m_directStreamUrl;
    }

    void NativePlaybackOpenRequest::DirectStreamUrl(winrt::hstring const& value)
    {
        m_directStreamUrl = value;
    }

    int64_t NativePlaybackOpenRequest::StartPositionTicks() const noexcept
    {
        return m_startPositionTicks;
    }

    void NativePlaybackOpenRequest::StartPositionTicks(int64_t value) noexcept
    {
        m_startPositionTicks = value;
    }

    int32_t NativePlaybackOpenRequest::AudioStreamIndex() const noexcept
    {
        return m_audioStreamIndex;
    }

    void NativePlaybackOpenRequest::AudioStreamIndex(int32_t value) noexcept
    {
        m_audioStreamIndex = value;
    }

    bool NativePlaybackOpenRequest::HasAudioStreamIndex() const noexcept
    {
        return m_hasAudioStreamIndex;
    }

    void NativePlaybackOpenRequest::HasAudioStreamIndex(bool value) noexcept
    {
        m_hasAudioStreamIndex = value;
    }

    int32_t NativePlaybackOpenRequest::SubtitleStreamIndex() const noexcept
    {
        return m_subtitleStreamIndex;
    }

    void NativePlaybackOpenRequest::SubtitleStreamIndex(int32_t value) noexcept
    {
        m_subtitleStreamIndex = value;
    }

    bool NativePlaybackOpenRequest::HasSubtitleStreamIndex() const noexcept
    {
        return m_hasSubtitleStreamIndex;
    }

    void NativePlaybackOpenRequest::HasSubtitleStreamIndex(bool value) noexcept
    {
        m_hasSubtitleStreamIndex = value;
    }

    bool NativePlaybackOpenRequest::IsHdr() const noexcept
    {
        return m_isHdr;
    }

    void NativePlaybackOpenRequest::IsHdr(bool value) noexcept
    {
        m_isHdr = value;
    }
}
