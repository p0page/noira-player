#include "pch.h"
#include "AudioRenderer.h"

#include <algorithm>

namespace
{
    constexpr size_t MaxSubmittedAudioBuffers = 8;
    constexpr uint32_t OutputSampleRate = 48000;
    constexpr int64_t TicksPerSecond = 10000000;

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
    AudioRenderer::VoiceCallback::VoiceCallback(AudioRenderer& owner) noexcept
        : m_owner(owner)
    {
    }

    void AudioRenderer::VoiceCallback::OnBufferEnd(void* context)
    {
        m_owner.OnBufferEnd(context);
    }

    AudioRenderer::AudioRenderer()
        : m_voiceCallback(*this)
    {
    }

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

        auto sourceVoiceFormat = CreateSourceVoiceFormat();
        CheckXAudioResult(
            m_audioEngine->CreateSourceVoice(
                &m_sourceVoice,
                &sourceVoiceFormat,
                0,
                XAUDIO2_DEFAULT_FREQ_RATIO,
                &m_voiceCallback),
            L"Could not create XAudio2 source voice.");

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
        if (m_sourceVoice != nullptr)
        {
            CheckXAudioResult(m_sourceVoice->Start(0), L"Could not start XAudio2 source voice.");
        }

        m_started = true;
        m_paused = false;
    }

    void AudioRenderer::Pause() noexcept
    {
        if (m_started)
        {
            if (m_sourceVoice != nullptr)
            {
                (void)m_sourceVoice->Stop(0);
            }

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

            if (m_sourceVoice != nullptr)
            {
                (void)m_sourceVoice->Start(0);
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

    void AudioRenderer::Flush() noexcept
    {
        if (m_sourceVoice != nullptr)
        {
            m_sourceVoice->FlushSourceBuffers();
        }

        std::lock_guard lock(m_bufferMutex);
        m_submittedBuffers.clear();
        ResetClock();
    }

    bool AudioRenderer::SubmitFrame(DecodedAudioFrame const& frame)
    {
        if (!m_open || m_sourceVoice == nullptr || frame.PcmData.empty())
        {
            return false;
        }

        auto pcmBuffer = std::make_shared<std::vector<uint8_t>>(frame.PcmData);
        auto establishedClockBase = false;
        {
            std::lock_guard lock(m_bufferMutex);
            if (m_submittedBuffers.size() >= MaxSubmittedAudioBuffers)
            {
                return false;
            }

            if (!m_hasClockBase && frame.SampleCount > 0)
            {
                m_clockBasePositionTicks = frame.PositionTicks;
                m_clockBaseSamplesPlayed = CurrentSamplesPlayed();
                m_hasClockBase = true;
                establishedClockBase = true;
            }

            m_submittedBuffers.push_back(pcmBuffer);
        }

        XAUDIO2_BUFFER sourceBuffer{};
        sourceBuffer.AudioBytes = static_cast<UINT32>(pcmBuffer->size());
        sourceBuffer.pAudioData = pcmBuffer->data();
        sourceBuffer.pContext = pcmBuffer.get();

        auto result = m_sourceVoice->SubmitSourceBuffer(&sourceBuffer);
        if (FAILED(result))
        {
            OnBufferEnd(pcmBuffer.get());
            if (establishedClockBase)
            {
                std::lock_guard lock(m_bufferMutex);
                ResetClock();
            }

            throw winrt::hresult_error(result, L"Could not submit XAudio2 source buffer.");
        }

        return true;
    }

    size_t AudioRenderer::QueuedBufferCount() const
    {
        std::lock_guard lock(m_bufferMutex);
        return m_submittedBuffers.size();
    }

    std::optional<int64_t> AudioRenderer::CurrentPositionTicks() const noexcept
    {
        if (!m_open || m_sourceVoice == nullptr)
        {
            return std::nullopt;
        }

        auto samplesPlayed = CurrentSamplesPlayed();

        std::lock_guard lock(m_bufferMutex);
        if (!m_hasClockBase)
        {
            return std::nullopt;
        }

        if (samplesPlayed <= m_clockBaseSamplesPlayed)
        {
            return m_clockBasePositionTicks;
        }

        auto elapsedSamples = samplesPlayed - m_clockBaseSamplesPlayed;
        auto elapsedTicks = static_cast<int64_t>(
            elapsedSamples * TicksPerSecond / OutputSampleRate);
        return m_clockBasePositionTicks + elapsedTicks;
    }

    void AudioRenderer::CloseAudioDevice() noexcept
    {
        if (m_sourceVoice != nullptr)
        {
            m_sourceVoice->Stop(0);
            m_sourceVoice->FlushSourceBuffers();
            m_sourceVoice->DestroyVoice();
            m_sourceVoice = nullptr;
        }

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

        {
            std::lock_guard lock(m_bufferMutex);
            m_submittedBuffers.clear();
            ResetClock();
        }
    }

    void AudioRenderer::OnBufferEnd(void* context) noexcept
    {
        std::lock_guard lock(m_bufferMutex);
        auto buffer = std::find_if(
            m_submittedBuffers.begin(),
            m_submittedBuffers.end(),
            [context](std::shared_ptr<std::vector<uint8_t>> const& queuedBuffer)
            {
                return queuedBuffer.get() == context;
            });
        if (buffer != m_submittedBuffers.end())
        {
            m_submittedBuffers.erase(buffer);
        }
    }

    void AudioRenderer::ResetClock() noexcept
    {
        m_clockBasePositionTicks = 0;
        m_clockBaseSamplesPlayed = 0;
        m_hasClockBase = false;
    }

    uint64_t AudioRenderer::CurrentSamplesPlayed() const noexcept
    {
        if (m_sourceVoice == nullptr)
        {
            return 0;
        }

        XAUDIO2_VOICE_STATE state{};
        m_sourceVoice->GetState(&state);
        return state.SamplesPlayed;
    }

    WAVEFORMATEX AudioRenderer::CreateSourceVoiceFormat() noexcept
    {
        WAVEFORMATEX format{};
        format.wFormatTag = WAVE_FORMAT_IEEE_FLOAT;
        format.nChannels = 2;
        format.nSamplesPerSec = OutputSampleRate;
        format.wBitsPerSample = 32;
        format.nBlockAlign = format.nChannels * format.wBitsPerSample / 8;
        format.nAvgBytesPerSec = format.nSamplesPerSec * format.nBlockAlign;
        return format;
    }
}
