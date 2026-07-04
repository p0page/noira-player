#pragma once

#include "DxDeviceResources.h"
#include "HttpMediaInput.h"
#include "NativePlaybackEngine.g.h"
#include "VideoDecoder.h"
#include "VideoRenderer.h"

namespace winrt::NextGenEmby::Native::implementation
{
    class PlaybackGraph
    {
    public:
        explicit PlaybackGraph(DxDeviceResources& deviceResources);

        void Open(NextGenEmby::Native::NativePlaybackOpenRequest const& request);
        void Pause();
        void Resume();
        void Seek(int64_t positionTicks);
        void Stop() noexcept;
        int64_t CurrentPositionTicks() const noexcept;

    private:
        void RenderNextFrame();

        HttpMediaInput m_input;
        VideoDecoder m_videoDecoder;
        VideoRenderer m_videoRenderer;
        winrt::hstring m_url;
        int64_t m_positionTicks{0};
        bool m_open{false};
        bool m_paused{false};
    };
}
