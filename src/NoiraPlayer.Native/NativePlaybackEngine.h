#pragma once

#include "DxDeviceResources.h"
#include "HdrDisplayController.h"
#include "Media/PlaybackGraph.h"
#include "NativePlaybackEngine.g.h"

#include <exception>
#include <memory>

namespace winrt::NoiraPlayer::Native::implementation
{
    struct NativePlaybackEngine : NativePlaybackEngineT<NativePlaybackEngine>
    {
        NativePlaybackEngine();

        winrt::event_token StateChanged(NoiraPlayer::Native::NativePlaybackStateChangedHandler const& handler);
        void StateChanged(winrt::event_token const& token) noexcept;

        void AttachSurface(winrt::Windows::UI::Xaml::Controls::SwapChainPanel const& panel);
        int64_t CurrentPositionTicks() const;
        NoiraPlayer::Native::NativePlaybackStatus DisplayStatus() const;
        NoiraPlayer::Native::NativePlaybackQualityMetrics QualityMetrics() const;

        winrt::Windows::Foundation::IAsyncAction OpenAsync(NoiraPlayer::Native::NativePlaybackOpenRequest request);
        winrt::Windows::Foundation::IAsyncAction PauseAsync();
        winrt::Windows::Foundation::IAsyncAction ResumeAsync();
        winrt::Windows::Foundation::IAsyncAction SeekAsync(int64_t positionTicks);
        winrt::Windows::Foundation::IAsyncAction StopAsync();
        winrt::Windows::Foundation::IAsyncAction SwitchAudioStreamAsync(int32_t audioStreamIndex);
        winrt::Windows::Foundation::IAsyncAction SwitchSubtitleStreamAsync(int32_t subtitleStreamIndex);
        winrt::Windows::Foundation::IAsyncAction DisableSubtitlesAsync();

    private:
        void ApplySwapChainColorSpace(HdrDisplaySnapshot const& snapshot);
        bool OnGraphHdrOutputChanged(bool desiredHdrOutput, double preferredRefreshRate);
        void OnGraphStateChanged(PlaybackGraphState state, winrt::hstring const& message);
        void RaiseFailed(std::exception const& error);
        void RaiseFailed(winrt::hresult_error const& error);
        void Raise(NoiraPlayer::Native::NativePlaybackState state, winrt::hstring const& message = L"");
        void UpdateDisplayStatus(HdrDisplaySnapshot const& snapshot);

        winrt::event<NoiraPlayer::Native::NativePlaybackStateChangedHandler> m_stateChanged;
        DxDeviceResources m_dx;
        std::unique_ptr<PlaybackGraph> m_graph;
        HdrDisplayController m_hdr;
        int64_t m_positionTicks{0};
        NoiraPlayer::Native::NativePlaybackStatus m_displayStatus{nullptr};
    };
}

namespace winrt::NoiraPlayer::Native::factory_implementation
{
    struct NativePlaybackEngine : NativePlaybackEngineT<NativePlaybackEngine, implementation::NativePlaybackEngine>
    {
    };
}
