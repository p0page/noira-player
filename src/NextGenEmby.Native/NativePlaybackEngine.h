#pragma once

#include "DxDeviceResources.h"
#include "HdrDisplayController.h"
#include "Media/PlaybackGraph.h"
#include "NativePlaybackEngine.g.h"

#include <exception>
#include <memory>

namespace winrt::NextGenEmby::Native::implementation
{
    struct NativePlaybackEngine : NativePlaybackEngineT<NativePlaybackEngine>
    {
        NativePlaybackEngine();

        winrt::event_token StateChanged(NextGenEmby::Native::NativePlaybackStateChangedHandler const& handler);
        void StateChanged(winrt::event_token const& token) noexcept;

        void AttachSurface(winrt::Windows::UI::Xaml::Controls::SwapChainPanel const& panel);
        int64_t CurrentPositionTicks() const;
        NextGenEmby::Native::NativePlaybackStatus DisplayStatus() const;

        winrt::Windows::Foundation::IAsyncAction OpenAsync(NextGenEmby::Native::NativePlaybackOpenRequest request);
        winrt::Windows::Foundation::IAsyncAction PauseAsync();
        winrt::Windows::Foundation::IAsyncAction ResumeAsync();
        winrt::Windows::Foundation::IAsyncAction SeekAsync(int64_t positionTicks);
        winrt::Windows::Foundation::IAsyncAction StopAsync();

    private:
        void ApplySwapChainColorSpace(HdrDisplaySnapshot const& snapshot);
        void OnGraphStateChanged(PlaybackGraphState state, winrt::hstring const& message);
        void RaiseFailed(std::exception const& error);
        void RaiseFailed(winrt::hresult_error const& error);
        void Raise(NextGenEmby::Native::NativePlaybackState state, winrt::hstring const& message = L"");
        void UpdateDisplayStatus(HdrDisplaySnapshot const& snapshot);

        winrt::event<NextGenEmby::Native::NativePlaybackStateChangedHandler> m_stateChanged;
        DxDeviceResources m_dx;
        std::unique_ptr<PlaybackGraph> m_graph;
        HdrDisplayController m_hdr;
        int64_t m_positionTicks{0};
        NextGenEmby::Native::NativePlaybackStatus m_displayStatus{nullptr};
    };
}

namespace winrt::NextGenEmby::Native::factory_implementation
{
    struct NativePlaybackEngine : NativePlaybackEngineT<NativePlaybackEngine, implementation::NativePlaybackEngine>
    {
    };
}
