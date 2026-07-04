#include "pch.h"
#include "SubtitleRenderer.h"

namespace winrt::NextGenEmby::Native::implementation
{
    void SubtitleRenderer::Open(std::optional<int32_t> selectedSubtitleStreamIndex)
    {
        if (selectedSubtitleStreamIndex.has_value() && selectedSubtitleStreamIndex.value() < 0)
        {
            throw winrt::hresult_invalid_argument(L"Subtitle stream index cannot be negative.");
        }

        m_selectedSubtitleStreamIndex = selectedSubtitleStreamIndex;
        m_lastRenderedPositionTicks = 0;
        m_open = true;
    }

    void SubtitleRenderer::Disable() noexcept
    {
        m_selectedSubtitleStreamIndex.reset();
        m_lastRenderedPositionTicks = 0;
        m_open = false;
    }

    void SubtitleRenderer::SwitchStream(int32_t subtitleStreamIndex)
    {
        if (subtitleStreamIndex < 0)
        {
            throw winrt::hresult_invalid_argument(L"Subtitle stream index cannot be negative.");
        }

        m_selectedSubtitleStreamIndex = subtitleStreamIndex;
        m_open = true;
    }

    void SubtitleRenderer::RenderAt(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Subtitle render position cannot be negative.");
        }

        if (m_open && m_selectedSubtitleStreamIndex.has_value())
        {
            m_lastRenderedPositionTicks = positionTicks;
        }
    }
}
