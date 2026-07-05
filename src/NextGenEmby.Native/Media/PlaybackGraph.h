#pragma once

#include "AudioRenderer.h"
#include "DxDeviceResources.h"
#include "HttpMediaInput.h"
#include "NativePlaybackEngine.g.h"
#include "SubtitleRenderer.h"
#include "VideoDecoder.h"
#include "VideoRenderer.h"

#include <condition_variable>
#include <functional>
#include <mutex>
#include <optional>
#include <thread>

namespace winrt::NextGenEmby::Native::implementation
{
    enum class PlaybackGraphState
    {
        Stopped,
        Failed
    };

    using PlaybackGraphStateChangedHandler = std::function<void(PlaybackGraphState state, winrt::hstring const& message)>;

    class PlaybackGraph
    {
    public:
        explicit PlaybackGraph(
            DxDeviceResources& deviceResources,
            PlaybackGraphStateChangedHandler stateChanged = nullptr);
        ~PlaybackGraph();

        void Open(NextGenEmby::Native::NativePlaybackOpenRequest const& request);
        void Pause();
        void Resume();
        void Seek(int64_t positionTicks);
        void Stop() noexcept;
        void SwitchAudioStream(int32_t audioStreamIndex);
        void SwitchSubtitleStream(std::optional<int32_t> subtitleStreamIndex);
        int64_t CurrentPositionTicks() const noexcept;

    private:
        void StartRenderLoop();
        void StopRenderLoop() noexcept;
        void RenderLoop() noexcept;
        bool RenderNextFrame();
        void NotifyStateChanged(PlaybackGraphState state, winrt::hstring const& message) const noexcept;

        DxDeviceResources& m_deviceResources;
        PlaybackGraphStateChangedHandler m_graphStateChanged;
        HttpMediaInput m_input;
        VideoDecoder m_videoDecoder;
        VideoRenderer m_videoRenderer;
        AudioRenderer m_audioRenderer;
        SubtitleRenderer m_subtitleRenderer;
        winrt::hstring m_url;
        int64_t m_positionTicks{0};
        bool m_open{false};
        bool m_paused{false};
        bool m_stopRenderLoop{false};
        std::thread m_renderThread;
        mutable std::mutex m_graphMutex;
        std::condition_variable m_stateChanged;
    };
}
