#include "pch.h"
#include "AudioRenderer.h"

namespace
{
    void CheckXAudioResult(HRESULT result, wchar_t const* message)
    {
        if (FAILED(result))
        {
            throw winrt::hresult_error(result, message);
        }
    }
}

namespace winrt::NextGenEmby::Native::implementation
{
    void AudioRenderer::Open(int32_t selectedAudioStreamIndex, bool hasSelection)
    {
        if (hasSelection && selectedAudioStreamIndex < 0)
        {
            throw winrt::hresult_invalid_argument(L"Audio stream index cannot be negative.");
        }

        Stop();

        CheckXAudioResult(XAudio2Create(m_audioEngine.GetAddressOf()), L"Could not create XAudio2 engine.");
        CheckXAudioResult(
            m_audioEngine->CreateMasteringVoice(&m_masteringVoice),
            L"Could not create XAudio2 mastering voice.");

        m_selectedAudioStreamIndex = hasSelection ? selectedAudioStreamIndex : 0;
        m_hasSelection = hasSelection;
        m_open = true;
        m_started = false;
        m_paused = false;
    }

    void AudioRenderer::Start()
    {
        if (!m_open || !m_audioEngine)
        {
            return;
        }

        CheckXAudioResult(m_audioEngine->StartEngine(), L"Could not start XAudio2 engine.");
        m_started = true;
        m_paused = false;
    }

    void AudioRenderer::Pause() noexcept
    {
        if (m_started)
        {
            if (m_audioEngine)
            {
                m_audioEngine->StopEngine();
            }

            m_paused = true;
        }
    }

    void AudioRenderer::Resume() noexcept
    {
        if (m_started)
        {
            if (m_audioEngine)
            {
                (void)m_audioEngine->StartEngine();
            }

            m_paused = false;
        }
    }

    void AudioRenderer::Stop() noexcept
    {
        CloseAudioDevice();
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

    void AudioRenderer::CloseAudioDevice() noexcept
    {
        if (m_audioEngine)
        {
            m_audioEngine->StopEngine();
        }

        if (m_masteringVoice != nullptr)
        {
            m_masteringVoice->DestroyVoice();
            m_masteringVoice = nullptr;
        }

        m_audioEngine.Reset();
    }
}
