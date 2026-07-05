#pragma once

#include "../DxDeviceResources.h"

#include <optional>
#include <string>

namespace winrt::NextGenEmby::Native::implementation
{
    class SubtitleRenderer
    {
    public:
        explicit SubtitleRenderer(DxDeviceResources& deviceResources);

        void Open(std::optional<int32_t> selectedSubtitleStreamIndex);
        void Disable() noexcept;
        void SwitchStream(int32_t subtitleStreamIndex);
        void SetTextCue(std::wstring text, int64_t startTicks, int64_t endTicks);
        void RenderAt(int64_t positionTicks);

    private:
        DxDeviceResources& m_deviceResources;
        std::optional<int32_t> m_selectedSubtitleStreamIndex;
        std::wstring m_textCue;
        int64_t m_textCueStartTicks{0};
        int64_t m_textCueEndTicks{0};
        int64_t m_lastRenderedPositionTicks{0};
        bool m_open{false};
    };
}
