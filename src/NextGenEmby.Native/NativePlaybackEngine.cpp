#include "pch.h"
#include "NativePlaybackEngine.h"
#include "NativePlaybackStatus.h"
#include "NativePlaybackEngine.g.cpp"

namespace winrt::NextGenEmby::Native::implementation
{
    NativePlaybackEngine::NativePlaybackEngine()
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

    int64_t NativePlaybackEngine::CurrentPositionTicks() const noexcept
    {
        return m_positionTicks;
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
        if (request == nullptr || request.DirectStreamUrl().empty())
        {
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Failed, L"Direct stream URL is required.");
            co_return;
        }

        m_positionTicks = request.StartPositionTicks();
        UpdateDisplayStatus(m_hdr.EnterHdr10());
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Opening);
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Playing);
        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::PauseAsync()
    {
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Paused);
        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::ResumeAsync()
    {
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Playing);
        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::SeekAsync(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Failed, L"Seek position cannot be negative.");
            co_return;
        }

        m_positionTicks = positionTicks;
        co_return;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::StopAsync()
    {
        m_positionTicks = 0;
        UpdateDisplayStatus(m_hdr.RestoreInitialState());
        Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Stopped);
        co_return;
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
