#pragma once

namespace winrt::NextGenEmby::Native::implementation
{
    class AudioRenderer
    {
    public:
        void Open(int32_t selectedAudioStreamIndex, bool hasSelection);
        void Start() noexcept;
        void Pause() noexcept;
        void Resume() noexcept;
        void Stop() noexcept;
        void SwitchStream(int32_t audioStreamIndex);

    private:
        int32_t m_selectedAudioStreamIndex{0};
        bool m_hasSelection{false};
        bool m_open{false};
        bool m_started{false};
        bool m_paused{false};
    };
}
