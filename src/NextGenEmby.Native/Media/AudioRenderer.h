#pragma once

#include "AudioDecoder.h"

#include <deque>
#include <memory>
#include <mutex>
#include <wrl/client.h>
#include <xaudio2.h>

namespace winrt::NextGenEmby::Native::implementation
{
    class AudioRenderer
    {
    public:
        AudioRenderer();

        void Open(int32_t selectedAudioStreamIndex, bool hasSelection);
        void Start();
        void Pause() noexcept;
        void Resume() noexcept;
        void Stop() noexcept;
        void SwitchStream(int32_t audioStreamIndex);
        bool SubmitFrame(DecodedAudioFrame const& frame);
        size_t QueuedBufferCount() const;

    private:
        class VoiceCallback final : public IXAudio2VoiceCallback
        {
        public:
            explicit VoiceCallback(AudioRenderer& owner) noexcept;

            void STDMETHODCALLTYPE OnVoiceProcessingPassStart(UINT32) override {}
            void STDMETHODCALLTYPE OnVoiceProcessingPassEnd() override {}
            void STDMETHODCALLTYPE OnStreamEnd() override {}
            void STDMETHODCALLTYPE OnBufferStart(void*) override {}
            void STDMETHODCALLTYPE OnBufferEnd(void* context) override;
            void STDMETHODCALLTYPE OnLoopEnd(void*) override {}
            void STDMETHODCALLTYPE OnVoiceError(void*, HRESULT) override {}

        private:
            AudioRenderer& m_owner;
        };

        void CloseAudioDevice() noexcept;
        void OnBufferEnd(void* context) noexcept;
        static WAVEFORMATEX CreateSourceVoiceFormat() noexcept;

        VoiceCallback m_voiceCallback;
        Microsoft::WRL::ComPtr<IXAudio2> m_audioEngine;
        IXAudio2MasteringVoice* m_masteringVoice{nullptr};
        IXAudio2SourceVoice* m_sourceVoice{nullptr};
        mutable std::mutex m_bufferMutex;
        std::deque<std::shared_ptr<std::vector<uint8_t>>> m_submittedBuffers;
        int32_t m_selectedAudioStreamIndex{0};
        bool m_hasSelection{false};
        bool m_open{false};
        bool m_started{false};
        bool m_paused{false};
    };
}
