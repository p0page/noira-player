#include "pch.h"
#include "AudioRenderer.h"

namespace winrt::NextGenEmby::Native::implementation
{
    void AudioRenderer::Open(int32_t selectedAudioStreamIndex, bool hasSelection)
    {
        if (hasSelection && selectedAudioStreamIndex < 0)
        {
            throw winrt::hresult_invalid_argument(L"Audio stream index cannot be negative.");
        }

        m_selectedAudioStreamIndex = hasSelection ? selectedAudioStreamIndex : 0;
        m_hasSelection = hasSelection;
        m_open = true;
        m_started = false;
        m_paused = false;
    }

    void AudioRenderer::Start() noexcept
    {
        if (m_open)
        {
            m_started = true;
            m_paused = false;
        }
    }

    void AudioRenderer::Pause() noexcept
    {
        if (m_started)
        {
            m_paused = true;
        }
    }

    void AudioRenderer::Resume() noexcept
    {
        if (m_started)
        {
            m_paused = false;
        }
    }

    void AudioRenderer::Stop() noexcept
    {
        m_open = false;
        m_started = false;
        m_paused = false;
        m_hasSelection = false;
        m_selectedAudioStreamIndex = 0;
    }

    void AudioRenderer::SwitchStream(int32_t audioStreamIndex)
    {
        if (audioStreamIndex < 0)
        {
            throw winrt::hresult_invalid_argument(L"Audio stream index cannot be negative.");
        }

        m_selectedAudioStreamIndex = audioStreamIndex;
        m_hasSelection = true;
    }
}
