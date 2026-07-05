#include "pch.h"
#include "NativePlaybackEngine.h"
#include "NativePlaybackStatus.h"
#include "NativePlaybackEngine.g.cpp"

#include <exception>

namespace winrt::NextGenEmby::Native::implementation
{
    NativePlaybackEngine::NativePlaybackEngine()
        : m_graph(std::make_unique<PlaybackGraph>(
              m_dx,
              [this](PlaybackGraphState state, winrt::hstring const& message)
              {
                  OnGraphStateChanged(state, message);
              }))
    {
        UpdateDisplayStatus(m_hdr.Probe());
    }

    winrt::event_token NativePlaybackEngine::StateChanged(
        NextGenEmby::Native::NativePlaybackStateChangedHandler const& handler)
    {
        return m_stateChanged.add(handler);
    }

    void NativePlaybackEngine::StateChanged(winrt::event_token const& token) noexcept
    {
        m_stateChanged.remove(token);
    }

    void NativePlaybackEngine::AttachSurface(winrt::Windows::UI::Xaml::Controls::SwapChainPanel const& panel)
    {
        m_dx.AttachSurface(panel);
    }

    int64_t NativePlaybackEngine::CurrentPositionTicks() const
    {
        return m_graph ? m_graph->CurrentPositionTicks() : m_positionTicks;
    }

    NextGenEmby::Native::NativePlaybackStatus NativePlaybackEngine::DisplayStatus() const
    {
        if (m_displayStatus == nullptr)
        {
            auto status = winrt::make<NativePlaybackStatus>();
            status.HdrStatus(NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unknown);
            status.IsHdrDisplayAvailable(false);
            status.IsHdrOutputActive(false);
            status.Message(L"Native engine has not probed the display yet.");
            return status;
        }

        return m_displayStatus;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::OpenAsync(
        NextGenEmby::Native::NativePlaybackOpenRequest request)
    {
        try
        {
            m_graph->Open(request);
            m_positionTicks = m_graph->CurrentPositionTicks();

            auto display = m_hdr.EnterHdr10();
            UpdateDisplayStatus(display);
            ApplySwapChainColorSpace(display);
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Opening);
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Playing);
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::PauseAsync()
    {
        try
        {
            m_graph->Pause();
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Paused);
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::ResumeAsync()
    {
        try
        {
            m_graph->Resume();
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Playing);
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::SeekAsync(int64_t positionTicks)
    {
        try
        {
            m_graph->Seek(positionTicks);
            m_positionTicks = m_graph->CurrentPositionTicks();
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::StopAsync()
    {
        try
        {
            m_graph->Stop();
            m_positionTicks = m_graph->CurrentPositionTicks();

            auto display = m_hdr.RestoreInitialState();
            UpdateDisplayStatus(display);
            ApplySwapChainColorSpace(display);
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Stopped);
        }
        catch (winrt::hresult_error const& error)
        {
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            RaiseFailed(error);
        }

        co_return;
    }

    void NativePlaybackEngine::ApplySwapChainColorSpace(HdrDisplaySnapshot const& snapshot)
    {
        if (snapshot.IsHdrOutputActive)
        {
            m_dx.SetHdr10ColorSpace();
            return;
        }

        m_dx.SetSdrColorSpace();
    }

    void NativePlaybackEngine::OnGraphStateChanged(
        PlaybackGraphState state,
        winrt::hstring const& message)
    {
        switch (state)
        {
        case PlaybackGraphState::Stopped:
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Stopped, message);
            break;
        case PlaybackGraphState::Failed:
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Failed, message);
            break;
        }
    }

    void NativePlaybackEngine::RaiseFailed(std::exception const& error)
    {
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Failed, winrt::to_hstring(error.what()));
    }

    void NativePlaybackEngine::RaiseFailed(winrt::hresult_error const& error)
    {
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Failed, error.message());
    }

    void NativePlaybackEngine::Raise(NextGenEmby::Native::NativePlaybackState state, winrt::hstring const& message)
    {
        m_stateChanged(state, message);
    }

    void NativePlaybackEngine::UpdateDisplayStatus(HdrDisplaySnapshot const& snapshot)
    {
        auto status = winrt::make<NativePlaybackStatus>();
        status.HdrStatus(snapshot.Status);
        status.IsHdrDisplayAvailable(snapshot.IsHdrDisplayAvailable);
        status.IsHdrOutputActive(snapshot.IsHdrOutputActive);
        status.Message(snapshot.Message);
        m_displayStatus = status;
    }
}
