#include "pch.h"
#include "SubtitleRenderer.h"

#include <utility>

namespace winrt::NextGenEmby::Native::implementation
{
    SubtitleRenderer::SubtitleRenderer(DxDeviceResources& deviceResources)
        : m_deviceResources(deviceResources)
    {
    }

    void SubtitleRenderer::Open(std::optional<int32_t> selectedSubtitleStreamIndex)
    {
        if (selectedSubtitleStreamIndex.has_value() && selectedSubtitleStreamIndex.value() < 0)
        {
            throw winrt::hresult_invalid_argument(L"Subtitle stream index cannot be negative.");
        }

        m_selectedSubtitleStreamIndex = selectedSubtitleStreamIndex;
        m_textCue.clear();
        m_textCueStartTicks = 0;
        m_textCueEndTicks = 0;
        m_lastRenderedPositionTicks = 0;
        m_open = true;
    }

    void SubtitleRenderer::Disable() noexcept
    {
        m_selectedSubtitleStreamIndex.reset();
        m_textCue.clear();
        m_textCueStartTicks = 0;
        m_textCueEndTicks = 0;
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

    void SubtitleRenderer::SetTextCue(std::wstring text, int64_t startTicks, int64_t endTicks)
    {
        if (startTicks < 0 || endTicks < startTicks)
        {
            throw winrt::hresult_invalid_argument(L"Subtitle cue time range is invalid.");
        }

        m_textCue = std::move(text);
        m_textCueStartTicks = startTicks;
        m_textCueEndTicks = endTicks;
    }

    void SubtitleRenderer::ClearCue() noexcept
    {
        m_textCue.clear();
        m_textCueStartTicks = 0;
        m_textCueEndTicks = 0;
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
            if (!m_textCue.empty() &&
                positionTicks >= m_textCueStartTicks &&
                positionTicks <= m_textCueEndTicks)
            {
                m_deviceResources.DrawTextOverlay(m_textCue);
            }
        }
    }
}
