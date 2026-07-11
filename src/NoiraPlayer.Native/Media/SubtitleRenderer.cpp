#include "pch.h"
#include "SubtitleRenderer.h"

#include <utility>

namespace winrt::NoiraPlayer::Native::implementation
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
        m_bitmapRegions.clear();
        m_textCueStartTicks = 0;
        m_textCueEndTicks = 0;
        m_lastRenderedPositionTicks = 0;
        m_open = true;
    }

    void SubtitleRenderer::Disable() noexcept
    {
        m_selectedSubtitleStreamIndex.reset();
        m_textCue.clear();
        m_bitmapRegions.clear();
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

    void SubtitleRenderer::SetCue(DecodedSubtitleCue cue)
    {
        if (cue.StartTicks < 0 || cue.EndTicks < cue.StartTicks)
        {
            throw winrt::hresult_invalid_argument(L"Subtitle cue time range is invalid.");
        }

        m_textCue = std::move(cue.Text);
        m_bitmapRegions = std::move(cue.BitmapRegions);
        m_textCueStartTicks = cue.StartTicks;
        m_textCueEndTicks = cue.EndTicks;
    }

    void SubtitleRenderer::ClearCue() noexcept
    {
        m_textCue.clear();
        m_bitmapRegions.clear();
        m_textCueStartTicks = 0;
        m_textCueEndTicks = 0;
    }

    bool SubtitleRenderer::RenderAt(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Subtitle render position cannot be negative.");
        }

        if (m_open && m_selectedSubtitleStreamIndex.has_value())
        {
            m_lastRenderedPositionTicks = positionTicks;
            if (positionTicks >= m_textCueStartTicks &&
                positionTicks <= m_textCueEndTicks)
            {
                auto rendered = !m_textCue.empty() &&
                    m_deviceResources.DrawTextOverlay(m_textCue);
                for (auto const& region : m_bitmapRegions)
                {
                    rendered = m_deviceResources.DrawSubtitleBitmapOverlay(region) || rendered;
                }

                return rendered;
            }
        }

        return false;
    }

    std::optional<int32_t> SubtitleRenderer::SelectedStreamIndex() const noexcept
    {
        return m_selectedSubtitleStreamIndex;
    }
}
