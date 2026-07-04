#pragma once

#include "NativePlaybackStatus.g.h"

namespace winrt::NextGenEmby::Native::implementation
{
    struct NativePlaybackStatus : NativePlaybackStatusT<NativePlaybackStatus>
    {
        NativePlaybackStatus() = default;

        NextGenEmby::Native::NativeHdrStatus HdrStatus() const noexcept;
        void HdrStatus(NextGenEmby::Native::NativeHdrStatus value) noexcept;
        bool IsHdrDisplayAvailable() const noexcept;
        void IsHdrDisplayAvailable(bool value) noexcept;
        bool IsHdrOutputActive() const noexcept;
        void IsHdrOutputActive(bool value) noexcept;
        winrt::hstring Message() const;
        void Message(winrt::hstring const& value);

    private:
        NextGenEmby::Native::NativeHdrStatus m_hdrStatus{NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unknown};
        bool m_isHdrDisplayAvailable{false};
        bool m_isHdrOutputActive{false};
        winrt::hstring m_message;
    };
}

namespace winrt::NextGenEmby::Native::factory_implementation
{
    struct NativePlaybackStatus : NativePlaybackStatusT<NativePlaybackStatus, implementation::NativePlaybackStatus>
    {
    };
}
