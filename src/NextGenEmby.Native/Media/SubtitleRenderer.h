#pragma once

#include <optional>

namespace winrt::NextGenEmby::Native::implementation
{
    class SubtitleRenderer
    {
    public:
        void Open(std::optional<int32_t> selectedSubtitleStreamIndex);
        void Disable() noexcept;
        void SwitchStream(int32_t subtitleStreamIndex);
        void RenderAt(int64_t positionTicks);

    private:
        std::optional<int32_t> m_selectedSubtitleStreamIndex;
        int64_t m_lastRenderedPositionTicks{0};
        bool m_open{false};
    };
}
