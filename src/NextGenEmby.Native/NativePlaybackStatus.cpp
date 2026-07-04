#include "pch.h"
#include "NativePlaybackStatus.h"
#include "NativePlaybackStatus.g.cpp"

namespace winrt::NextGenEmby::Native::implementation
{
    NextGenEmby::Native::NativeHdrStatus NativePlaybackStatus::HdrStatus() const noexcept
    {
        return m_hdrStatus;
    }

    void NativePlaybackStatus::HdrStatus(NextGenEmby::Native::NativeHdrStatus value) noexcept
    {
        m_hdrStatus = value;
    }

    bool NativePlaybackStatus::IsHdrDisplayAvailable() const noexcept
    {
        return m_isHdrDisplayAvailable;
    }

    void NativePlaybackStatus::IsHdrDisplayAvailable(bool value) noexcept
    {
        m_isHdrDisplayAvailable = value;
    }

    bool NativePlaybackStatus::IsHdrOutputActive() const noexcept
    {
        return m_isHdrOutputActive;
    }

    void NativePlaybackStatus::IsHdrOutputActive(bool value) noexcept
    {
        m_isHdrOutputActive = value;
    }

    winrt::hstring NativePlaybackStatus::Message() const
    {
        return m_message;
    }

    void NativePlaybackStatus::Message(winrt::hstring const& value)
    {
        m_message = value;
    }
}
