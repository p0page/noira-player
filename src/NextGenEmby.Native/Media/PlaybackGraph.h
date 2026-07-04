#pragma once

#include "HttpMediaInput.h"
#include "NativePlaybackEngine.g.h"

namespace winrt::NextGenEmby::Native::implementation
{
    class PlaybackGraph
    {
    public:
        void Open(NextGenEmby::Native::NativePlaybackOpenRequest const& request);
        void Pause();
        void Resume();
        void Seek(int64_t positionTicks);
        void Stop() noexcept;
        int64_t CurrentPositionTicks() const noexcept;

    private:
        HttpMediaInput m_input;
        winrt::hstring m_url;
        int64_t m_positionTicks{0};
        bool m_open{false};
        bool m_paused{false};
    };
}
