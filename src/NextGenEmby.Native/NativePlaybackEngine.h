#pragma once

#include "NativePlaybackEngine.g.h"

namespace winrt::NextGenEmby::Native::implementation
{
    struct NativePlaybackEngine : NativePlaybackEngineT<NativePlaybackEngine>
    {
        NativePlaybackEngine() = default;

        winrt::event_token StateChanged(NextGenEmby::Native::NativePlaybackStateChangedHandler const& handler);
        void StateChanged(winrt::event_token const& token) noexcept;

        int64_t CurrentPositionTicks() const noexcept;
        NextGenEmby::Native::NativePlaybackStatus DisplayStatus() const;

        winrt::Windows::Foundation::IAsyncAction OpenAsync(NextGenEmby::Native::NativePlaybackOpenRequest request);
        winrt::Windows::Foundation::IAsyncAction PauseAsync();
        winrt::Windows::Foundation::IAsyncAction ResumeAsync();
        winrt::Windows::Foundation::IAsyncAction SeekAsync(int64_t positionTicks);
        winrt::Windows::Foundation::IAsyncAction StopAsync();

    private:
        void Raise(NextGenEmby::Native::NativePlaybackState state, winrt::hstring const& message = L"");

        winrt::event<NextGenEmby::Native::NativePlaybackStateChangedHandler> m_stateChanged;
        int64_t m_positionTicks{0};
        NextGenEmby::Native::NativePlaybackStatus m_displayStatus{nullptr};
    };
}

namespace winrt::NextGenEmby::Native::factory_implementation
{
    struct NativePlaybackEngine : NativePlaybackEngineT<NativePlaybackEngine, implementation::NativePlaybackEngine>
    {
    };
}
