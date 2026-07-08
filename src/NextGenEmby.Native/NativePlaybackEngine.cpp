#include "pch.h"
#include "NativePlaybackEngine.h"
#include "NativePlaybackDiagnostics.h"
#include "NativePlaybackQualityMetrics.h"
#include "NativePlaybackStatus.h"
#include "NativePlaybackEngine.g.cpp"

#include <exception>
#include <optional>
#include <string>

namespace winrt::NextGenEmby::Native::implementation
{
    namespace
    {
        winrt::hstring FormatDxgiFormat(DXGI_FORMAT format)
        {
            switch (format)
            {
            case DXGI_FORMAT_R10G10B10A2_UNORM:
                return L"R10G10B10A2_UNORM";
            case DXGI_FORMAT_B8G8R8A8_UNORM:
                return L"B8G8R8A8_UNORM";
            case DXGI_FORMAT_UNKNOWN:
                return L"UNKNOWN";
            default:
                return winrt::to_hstring(static_cast<int32_t>(format));
            }
        }

        winrt::hstring FormatDxgiColorSpace(DXGI_COLOR_SPACE_TYPE colorSpace)
        {
            switch (colorSpace)
            {
            case DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709:
                return L"RGB_FULL_G22_NONE_P709";
            case DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020:
                return L"RGB_FULL_G2084_NONE_P2020";
            case DXGI_COLOR_SPACE_RGB_STUDIO_G2084_NONE_P2020:
                return L"RGB_STUDIO_G2084_NONE_P2020";
            case DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P2020:
                return L"RGB_FULL_G22_NONE_P2020";
            case DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P2020:
                return L"RGB_STUDIO_G22_NONE_P2020";
            case DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P709:
                return L"RGB_STUDIO_G22_NONE_P709";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_LEFT_P2020:
                return L"YCBCR_STUDIO_G2084_LEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_TOPLEFT_P2020:
                return L"YCBCR_STUDIO_G2084_TOPLEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_GHLG_TOPLEFT_P2020:
                return L"YCBCR_STUDIO_GHLG_TOPLEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_FULL_GHLG_TOPLEFT_P2020:
                return L"YCBCR_FULL_GHLG_TOPLEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P2020:
                return L"YCBCR_STUDIO_G22_LEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P2020:
                return L"YCBCR_FULL_G22_LEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_TOPLEFT_P2020:
                return L"YCBCR_STUDIO_G22_TOPLEFT_P2020";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P601:
                return L"YCBCR_STUDIO_G22_LEFT_P601";
            case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P601:
                return L"YCBCR_FULL_G22_LEFT_P601";
            case DXGI_COLOR_SPACE_YCBCR_FULL_G22_NONE_P709_X601:
                return L"YCBCR_FULL_G22_NONE_P709_X601";
            case DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P709:
                return L"YCBCR_STUDIO_G22_LEFT_P709";
            case DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P709:
                return L"YCBCR_FULL_G22_LEFT_P709";
            case DXGI_COLOR_SPACE_CUSTOM:
                return L"CUSTOM";
            default:
                return winrt::to_hstring(static_cast<int32_t>(colorSpace));
            }
        }

        std::wstring FormatBool(bool value)
        {
            return value ? L"true" : L"false";
        }

        std::wstring FormatHdrSnapshot(HdrDisplaySnapshot const& snapshot)
        {
            return L"status=" + std::to_wstring(static_cast<int32_t>(snapshot.Status)) +
                L" display=" + FormatBool(snapshot.IsHdrDisplayAvailable) +
                L" active=" + FormatBool(snapshot.IsHdrOutputActive) +
                L" refresh=" + std::to_wstring(snapshot.RefreshRateHz) +
                L" message=" + std::wstring(snapshot.Message.c_str());
        }

        PlaybackGraphOpenRequest CreatePlaybackGraphOpenRequest(
            NextGenEmby::Native::NativePlaybackOpenRequest const& request)
        {
            if (request == nullptr)
            {
                throw winrt::hresult_invalid_argument(L"Playback request is required.");
            }

            return PlaybackGraphOpenRequest
            {
                request.DirectStreamUrl(),
                request.StartPositionTicks(),
                request.AudioStreamIndex(),
                request.HasAudioStreamIndex(),
                request.SubtitleStreamIndex(),
                request.HasSubtitleStreamIndex(),
                request.VideoFrameRate()
            };
        }
    }

    NativePlaybackEngine::NativePlaybackEngine()
        : m_graph(std::make_unique<PlaybackGraph>(
              m_dx,
              [this](PlaybackGraphState state, winrt::hstring const& message)
              {
                  OnGraphStateChanged(state, message);
              },
              [this](bool desiredHdrOutput, double preferredRefreshRate)
              {
                  return OnGraphHdrOutputChanged(desiredHdrOutput, preferredRefreshRate);
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
        auto status = winrt::make<NativePlaybackStatus>();
        if (m_displayStatus == nullptr)
        {
            status.HdrStatus(NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unknown);
            status.IsHdrDisplayAvailable(false);
            status.IsHdrOutputActive(false);
            status.Message(L"Native engine has not probed the display yet.");
        }
        else
        {
            status.HdrStatus(m_displayStatus.HdrStatus());
            status.IsHdrDisplayAvailable(m_displayStatus.IsHdrDisplayAvailable());
            status.IsHdrOutputActive(m_displayStatus.IsHdrOutputActive());
            status.RefreshRateHz(m_displayStatus.RefreshRateHz());
            status.Message(m_displayStatus.Message());
        }

        status.SwapChainFormat(FormatDxgiFormat(m_dx.SwapChainFormat()));
        status.SwapChainColorSpace(FormatDxgiColorSpace(m_dx.SwapChainColorSpace()));
        status.IsTenBitSwapChain(m_dx.IsTenBitSwapChain());
        status.IsVideoProcessorColorSpaceValidated(m_dx.LastVideoProcessorConversionWasValidated());
        status.VideoProcessorInputColorSpace(FormatDxgiColorSpace(m_dx.LastVideoProcessorInputColorSpace()));
        status.VideoProcessorOutputColorSpace(FormatDxgiColorSpace(m_dx.LastVideoProcessorOutputColorSpace()));
        status.VideoProcessorConversionStatus(winrt::hstring(m_dx.LastVideoProcessorConversionStatus()));
        return status;
    }

    NextGenEmby::Native::NativePlaybackQualityMetrics NativePlaybackEngine::QualityMetrics() const
    {
        auto snapshot = m_graph->QualityMetricsSnapshot();
        auto metrics = winrt::make<NativePlaybackQualityMetrics>();
        metrics.RenderPasses(snapshot.RenderPasses);
        metrics.DecodedVideoFrames(snapshot.DecodedVideoFrames);
        metrics.RenderedVideoFrames(snapshot.RenderedVideoFrames);
        metrics.SubmittedAudioFrames(snapshot.SubmittedAudioFrames);
        metrics.DroppedVideoFrames(snapshot.DroppedVideoFrames);
        metrics.SeekPrerollDroppedFrames(snapshot.SeekPrerollDroppedFrames);
        metrics.VideoAheadWaitCount(snapshot.VideoAheadWaitCount);
        metrics.VideoStarvedPasses(snapshot.VideoStarvedPasses);
        metrics.AudioStarvedPasses(snapshot.AudioStarvedPasses);
        metrics.QueuedAudioBuffers(snapshot.QueuedAudioBuffers);
        metrics.AudioClockTicks(snapshot.AudioClockTicks);
        metrics.VideoPositionTicks(snapshot.VideoPositionTicks);
        metrics.RenderIntervalMsP50(snapshot.RenderIntervalMsP50);
        metrics.RenderIntervalMsP95(snapshot.RenderIntervalMsP95);
        metrics.RenderIntervalMsP99(snapshot.RenderIntervalMsP99);
        metrics.MaxFrameGapMs(snapshot.MaxFrameGapMs);
        metrics.PresentDurationMsP50(snapshot.PresentDurationMsP50);
        metrics.PresentDurationMsP95(snapshot.PresentDurationMsP95);
        metrics.PresentDurationMsP99(snapshot.PresentDurationMsP99);
        metrics.PresentDurationMsMax(snapshot.PresentDurationMsMax);
        metrics.FramePacingSourceFrameRate(snapshot.FramePacingSourceFrameRate);
        metrics.LateFrameDropToleranceMs(snapshot.LateFrameDropToleranceMs);
        metrics.AudioVideoDriftMsP50(snapshot.AudioVideoDriftMsP50);
        metrics.AudioVideoDriftMsP95(snapshot.AudioVideoDriftMsP95);
        metrics.AudioVideoDriftMsP99(snapshot.AudioVideoDriftMsP99);
        metrics.AudioVideoDriftMsMax(snapshot.AudioVideoDriftMsMax);
        return metrics;
    }

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::OpenAsync(
        NextGenEmby::Native::NativePlaybackOpenRequest request)
    {
        try
        {
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync enter");
            auto graphRequest = CreatePlaybackGraphOpenRequest(request);
            auto videoFrameRate = graphRequest.VideoFrameRate;
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync request videoFrameRate=" + std::to_wstring(videoFrameRate));
            UpdateDisplayStatus(m_hdr.Probe());

            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync graph Open begin");
            m_graph->Open(graphRequest);
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync graph Open end");
            m_positionTicks = m_graph->CurrentPositionTicks();
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync CurrentPositionTicks read");
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync Raise Opening begin");
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Opening);
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync Raise Playing begin");
            Raise(NextGenEmby::Native::NativePlaybackState::NativePlaybackState_Playing);
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync success end");
        }
        catch (winrt::hresult_error const& error)
        {
            AppendNativePlaybackDiagnostic(L"NativePlaybackEngine.OpenAsync hresult exception " + std::wstring(error.message().c_str()));
            RaiseFailed(error);
        }
        catch (std::exception const& error)
        {
            AppendNativePlaybackDiagnostic(
                L"NativePlaybackEngine.OpenAsync std exception " +
                std::wstring(winrt::to_hstring(error.what()).c_str()));
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

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::SwitchAudioStreamAsync(int32_t audioStreamIndex)
    {
        try
        {
            m_graph->SwitchAudioStream(audioStreamIndex);
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

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::SwitchSubtitleStreamAsync(int32_t subtitleStreamIndex)
    {
        try
        {
            m_graph->SwitchSubtitleStream(subtitleStreamIndex);
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

    winrt::Windows::Foundation::IAsyncAction NativePlaybackEngine::DisableSubtitlesAsync()
    {
        try
        {
            m_graph->SwitchSubtitleStream(std::nullopt);
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

    bool NativePlaybackEngine::OnGraphHdrOutputChanged(
        bool desiredHdrOutput,
        double preferredRefreshRate)
    {
        AppendNativePlaybackDiagnostic(desiredHdrOutput
            ? L"NativePlaybackEngine.OnGraphHdrOutputChanged EnterHdr10 begin"
            : L"NativePlaybackEngine.OnGraphHdrOutputChanged LeaveHdr10 begin");
        auto display = desiredHdrOutput
            ? m_hdr.EnterHdr10(preferredRefreshRate)
            : m_hdr.LeaveHdr10();
        AppendNativePlaybackDiagnostic(
            L"NativePlaybackEngine.OnGraphHdrOutputChanged display end " + FormatHdrSnapshot(display));
        UpdateDisplayStatus(display);

        if (!display.IsHdrOutputActive)
        {
            m_dx.SetSdrColorSpace();
        }

        return desiredHdrOutput && display.IsHdrOutputActive;
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
        status.RefreshRateHz(snapshot.RefreshRateHz);
        status.Message(snapshot.Message);
        status.SwapChainFormat(FormatDxgiFormat(m_dx.SwapChainFormat()));
        status.SwapChainColorSpace(FormatDxgiColorSpace(m_dx.SwapChainColorSpace()));
        status.IsTenBitSwapChain(m_dx.IsTenBitSwapChain());
        status.IsVideoProcessorColorSpaceValidated(m_dx.LastVideoProcessorConversionWasValidated());
        status.VideoProcessorInputColorSpace(FormatDxgiColorSpace(m_dx.LastVideoProcessorInputColorSpace()));
        status.VideoProcessorOutputColorSpace(FormatDxgiColorSpace(m_dx.LastVideoProcessorOutputColorSpace()));
        status.VideoProcessorConversionStatus(winrt::hstring(m_dx.LastVideoProcessorConversionStatus()));
        m_displayStatus = status;
    }
}
