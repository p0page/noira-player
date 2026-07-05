#pragma once

#include <wrl/client.h>
#include <xaudio2.h>

namespace winrt::NextGenEmby::Native::implementation
{
    class AudioRenderer
    {
    public:
        void Open(int32_t selectedAudioStreamIndex, bool hasSelection);
        void Start();
        void Pause() noexcept;
        void Resume() noexcept;
        void Stop() noexcept;
        void SwitchStream(int32_t audioStreamIndex);

    private:
        void CloseAudioDevice() noexcept;

        Microsoft::WRL::ComPtr<IXAudio2> m_audioEngine;
        IXAudio2MasteringVoice* m_masteringVoice{nullptr};
        int32_t m_selectedAudioStreamIndex{0};
        bool m_hasSelection{false};
        bool m_open{false};
        bool m_started{false};
        bool m_paused{false};
    };
}
