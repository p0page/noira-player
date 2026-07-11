#pragma once

#include "AudioBufferAccumulator.h"
#include "AudioDecoder.h"

#include <deque>
#include <memory>
#include <mutex>
#include <optional>
#include <wrl/client.h>
#include <xaudio2.h>

namespace winrt::NoiraPlayer::Native::implementation
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
        void Flush() noexcept;
        bool SubmitFrame(DecodedAudioFrame const& frame);
        bool DrainPendingFrame();
        size_t QueuedBufferCount() const;
        std::optional<int64_t> CurrentPositionTicks() const noexcept;
        std::optional<int32_t> SelectedStreamIndex() const noexcept;

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
        void ResetClock() noexcept;
        bool SubmitAccumulatedBuffer(AccumulatedAudioBuffer buffer);
        uint64_t CurrentSamplesPlayed() const noexcept;
        static WAVEFORMATEX CreateSourceVoiceFormat() noexcept;

        VoiceCallback m_voiceCallback;
        Microsoft::WRL::ComPtr<IXAudio2> m_audioEngine;
        IXAudio2MasteringVoice* m_masteringVoice{nullptr};
        IXAudio2SourceVoice* m_sourceVoice{nullptr};
        mutable std::mutex m_bufferMutex;
        AudioBufferAccumulator m_audioBufferAccumulator;
        std::deque<std::shared_ptr<std::vector<uint8_t>>> m_submittedBuffers;
        int64_t m_clockBasePositionTicks{0};
        uint64_t m_clockBaseSamplesPlayed{0};
        int32_t m_selectedAudioStreamIndex{0};
        bool m_hasSelection{false};
        bool m_hasClockBase{false};
        bool m_open{false};
        bool m_started{false};
        bool m_paused{false};
    };
}
