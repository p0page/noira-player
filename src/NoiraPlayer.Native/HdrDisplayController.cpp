#include "pch.h"
#include "HdrDisplayController.h"
#include "HdrDisplayRefreshRateSnapshot.h"
#include "HdrDisplayRefreshRatePolicy.h"
#include "NativePlaybackDiagnostics.h"

#include <cmath>
#include <memory>
#include <ppl.h>
#include <sdkddkver.h>
#include <string>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.Graphics.Display.h>
#include <winrt/Windows.Graphics.Display.Core.h>

namespace winrt::NoiraPlayer::Native::implementation
{
    using namespace winrt::Windows::Foundation::Metadata;
    using namespace winrt::Windows::Graphics::Display;
    using namespace winrt::Windows::Graphics::Display::Core;

    namespace
    {
        constexpr double RefreshRateTolerance = 0.1;

        bool IsStaThread()
        {
#ifdef NTDDI_WIN10_CO
            return winrt::impl::is_sta_thread();
#else
            return winrt::impl::is_sta();
#endif
        }

        bool WaitForDisplayModeResult(winrt::Windows::Foundation::IAsyncOperation<bool> const& operation)
        {
            if (operation.Status() == winrt::Windows::Foundation::AsyncStatus::Completed)
            {
                return operation.GetResults();
            }

            if (!IsStaThread())
            {
                return operation.get();
            }

            auto sync = std::make_shared<Concurrency::event>();
            operation.Completed([sync](auto&&, auto&&)
            {
                sync->set();
            });
            sync->wait();
            return operation.GetResults();
        }

        std::wstring FormatDisplayMode(HdmiDisplayMode const& mode)
        {
            if (mode == nullptr)
            {
                return L"null";
            }

            return std::to_wstring(mode.ResolutionWidthInRawPixels()) +
                L"x" +
                std::to_wstring(mode.ResolutionHeightInRawPixels()) +
                L"@" +
                std::to_wstring(mode.RefreshRate()) +
                L" color=" +
                std::to_wstring(static_cast<int32_t>(mode.ColorSpace())) +
                L" smpte2084=" +
                (mode.IsSmpte2084Supported() ? L"true" : L"false") +
                L" smpte2086=" +
                (mode.Is2086MetadataSupported() ? L"true" : L"false") +
                L" stereo=" +
                (mode.StereoEnabled() ? L"true" : L"false");
        }

        bool IsHdrDisplayMode(HdmiDisplayMode const& mode)
        {
            return mode != nullptr &&
                mode.ColorSpace() == HdmiDisplayColorSpace::BT2020;
        }

        bool MatchesRefreshRate(HdmiDisplayMode const& candidate, HdmiDisplayMode const& current)
        {
            return candidate != nullptr &&
                current != nullptr &&
                std::fabs(candidate.RefreshRate() - current.RefreshRate()) <= RefreshRateTolerance;
        }

        bool MatchesResolution(HdmiDisplayMode const& candidate, HdmiDisplayMode const& current)
        {
            return candidate != nullptr &&
                current != nullptr &&
                candidate.ResolutionWidthInRawPixels() == current.ResolutionWidthInRawPixels() &&
                candidate.ResolutionHeightInRawPixels() == current.ResolutionHeightInRawPixels();
        }

        uint64_t PixelCount(HdmiDisplayMode const& mode)
        {
            if (mode == nullptr)
            {
                return 0;
            }

            return static_cast<uint64_t>(mode.ResolutionWidthInRawPixels()) *
                static_cast<uint64_t>(mode.ResolutionHeightInRawPixels());
        }

        bool IsBetterHdrMode(
            HdmiDisplayMode const& candidate,
            HdmiDisplayMode const& selected,
            HdmiDisplayMode const& current,
            double preferredRefreshRate)
        {
            if (selected == nullptr)
            {
                return true;
            }

            if (HdrDisplayRefreshRatePolicy::HasUsableVideoFrameRate(preferredRefreshRate))
            {
                auto candidateWeight = HdrDisplayRefreshRatePolicy::RefreshWeight(
                    candidate.RefreshRate(),
                    preferredRefreshRate);
                auto selectedWeight = HdrDisplayRefreshRatePolicy::RefreshWeight(
                    selected.RefreshRate(),
                    preferredRefreshRate);
                if (candidateWeight != selectedWeight)
                {
                    return candidateWeight < selectedWeight;
                }
            }

            auto candidateMatchesStereo = candidate.StereoEnabled() == current.StereoEnabled();
            auto selectedMatchesStereo = selected.StereoEnabled() == current.StereoEnabled();
            if (candidateMatchesStereo != selectedMatchesStereo)
            {
                return candidateMatchesStereo;
            }

            auto candidateMatchesResolution = MatchesResolution(candidate, current);
            auto selectedMatchesResolution = MatchesResolution(selected, current);
            if (candidateMatchesResolution != selectedMatchesResolution)
            {
                return candidateMatchesResolution;
            }

            return PixelCount(candidate) > PixelCount(selected);
        }

        bool MatchesPreferredRefresh(HdmiDisplayMode const& candidate, double preferredRefreshRate)
        {
            return candidate != nullptr &&
                HdrDisplayRefreshRatePolicy::MatchesVideoFrameRate(
                    candidate.RefreshRate(),
                    preferredRefreshRate);
        }

        bool MatchesTargetRefresh(
            HdmiDisplayMode const& candidate,
            HdmiDisplayMode const& current,
            double preferredRefreshRate)
        {
            if (HdrDisplayRefreshRatePolicy::HasUsableVideoFrameRate(preferredRefreshRate))
            {
                return MatchesPreferredRefresh(candidate, preferredRefreshRate);
            }

            return MatchesRefreshRate(candidate, current);
        }

        bool MatchesTargetHdrMode(
            HdmiDisplayMode const& candidate,
            HdmiDisplayMode const& current,
            double preferredRefreshRate)
        {
            return IsHdrDisplayMode(candidate) &&
                MatchesTargetRefresh(candidate, current, preferredRefreshRate);
        }

        bool MatchesTargetSdrMode(
            HdmiDisplayMode const& candidate,
            HdmiDisplayMode const& current,
            double preferredRefreshRate)
        {
            return candidate != nullptr &&
                !IsHdrDisplayMode(candidate) &&
                MatchesTargetRefresh(candidate, current, preferredRefreshRate);
        }

        HdmiDisplayMode FindDisplayMode(
            HdmiDisplayInformation const& hdmi,
            HdmiDisplayMode const& current,
            bool enableHdr,
            double preferredRefreshRate)
        {
            HdmiDisplayMode selected = nullptr;
            AppendNativePlaybackDiagnostic(
                L"HdrDisplayController.FindDisplayMode current " +
                FormatDisplayMode(current) +
                L" enableHdr=" +
                (enableHdr ? L"true" : L"false") +
                L" preferredRefresh=" +
                std::to_wstring(preferredRefreshRate));
            auto modes = hdmi.GetSupportedDisplayModes();
            for (auto const& mode : modes)
            {
                AppendNativePlaybackDiagnostic(L"HdrDisplayController.FindDisplayMode candidate " + FormatDisplayMode(mode));
                auto matches = enableHdr
                    ? MatchesTargetHdrMode(mode, current, preferredRefreshRate)
                    : MatchesTargetSdrMode(mode, current, preferredRefreshRate);
                if (matches)
                {
                    if (IsBetterHdrMode(mode, selected, current, preferredRefreshRate))
                    {
                        selected = mode;
                    }
                }
            }

            if (selected == nullptr &&
                HdrDisplayRefreshRatePolicy::HasUsableVideoFrameRate(preferredRefreshRate))
            {
                AppendNativePlaybackDiagnostic(L"HdrDisplayController.FindDisplayMode retry current refresh");
                for (auto const& mode : modes)
                {
                    auto matches = enableHdr
                        ? MatchesTargetHdrMode(mode, current, 0.0)
                        : MatchesTargetSdrMode(mode, current, 0.0);
                    if (matches && IsBetterHdrMode(mode, selected, current, 0.0))
                    {
                        selected = mode;
                    }
                }
            }

            AppendNativePlaybackDiagnostic(L"HdrDisplayController.FindDisplayMode selected " + FormatDisplayMode(selected));
            return selected;
        }

        bool HasHdrMode(HdmiDisplayInformation const& hdmi, HdmiDisplayMode const& current)
        {
            auto modes = hdmi.GetSupportedDisplayModes();
            for (auto const& mode : modes)
            {
                if (MatchesTargetHdrMode(mode, current, 0.0))
                {
                    return true;
                }
            }

            return false;
        }

        void CaptureRefreshRate(HdrDisplaySnapshot& snapshot, HdmiDisplayMode const& mode)
        {
            snapshot.RefreshRateHz = HdrDisplayRefreshRateSnapshot::Normalize(
                mode != nullptr ? mode.RefreshRate() : 0.0);
        }
    }

    HdrDisplaySnapshot HdrDisplayController::Probe()
    {
        AppendNativePlaybackDiagnostic(L"HdrDisplayController.Probe begin");
        HdrDisplaySnapshot snapshot;
        auto advancedHdrActive = false;

        auto info = DisplayInformation::GetForCurrentView();
        AppendNativePlaybackDiagnostic(info != nullptr
            ? L"HdrDisplayController.Probe DisplayInformation available"
            : L"HdrDisplayController.Probe DisplayInformation unavailable");
        if (info != nullptr)
        {
            auto advanced = info.GetAdvancedColorInfo();
            AppendNativePlaybackDiagnostic(advanced != nullptr
                ? L"HdrDisplayController.Probe AdvancedColorInfo available"
                : L"HdrDisplayController.Probe AdvancedColorInfo unavailable");
            if (advanced != nullptr &&
                advanced.CurrentAdvancedColorKind() == AdvancedColorKind::HighDynamicRange)
            {
                snapshot.Status = NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_On;
                snapshot.IsHdrDisplayAvailable = true;
                snapshot.IsHdrOutputActive = true;
                advancedHdrActive = true;
                AppendNativePlaybackDiagnostic(L"HdrDisplayController.Probe current view already HDR");
            }
        }

        if (!ApiInformation::IsTypePresent(L"Windows.Graphics.Display.Core.HdmiDisplayInformation"))
        {
            if (!advancedHdrActive)
            {
                snapshot.Status = NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
                snapshot.Message = L"HdmiDisplayInformation is unavailable.";
            }
            AppendNativePlaybackDiagnostic(L"HdrDisplayController.Probe HdmiDisplayInformation unavailable");
            return snapshot;
        }

        AppendNativePlaybackDiagnostic(L"HdrDisplayController.Probe GetForCurrentView begin");
        auto hdmi = HdmiDisplayInformation::GetForCurrentView();
        AppendNativePlaybackDiagnostic(hdmi != nullptr
            ? L"HdrDisplayController.Probe GetForCurrentView end available"
            : L"HdrDisplayController.Probe GetForCurrentView end null");
        if (hdmi == nullptr)
        {
            if (!advancedHdrActive)
            {
                snapshot.Status = NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
                snapshot.Message = L"No HDMI display information is available.";
            }
            return snapshot;
        }

        AppendNativePlaybackDiagnostic(L"HdrDisplayController.Probe GetCurrentDisplayMode begin");
        auto current = hdmi.GetCurrentDisplayMode();
        AppendNativePlaybackDiagnostic(current != nullptr
            ? L"HdrDisplayController.Probe GetCurrentDisplayMode end available"
            : L"HdrDisplayController.Probe GetCurrentDisplayMode end null");
        CaptureRefreshRate(snapshot, current);
        if (advancedHdrActive)
        {
            return snapshot;
        }

        snapshot.IsHdrDisplayAvailable =
            current != nullptr &&
            (current.IsSmpte2084Supported() || HasHdrMode(hdmi, current));
        snapshot.IsHdrOutputActive = false;
        snapshot.Status = snapshot.IsHdrDisplayAvailable
            ? NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Off
            : NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
        AppendNativePlaybackDiagnostic(snapshot.IsHdrDisplayAvailable
            ? L"HdrDisplayController.Probe end hdr display available"
            : L"HdrDisplayController.Probe end hdr display unavailable");
        return snapshot;
    }

    HdrDisplaySnapshot HdrDisplayController::EnterHdr10(double videoFrameRate)
    {
        AppendNativePlaybackDiagnostic(L"HdrDisplayController.EnterHdr10 begin");
        auto current = Probe();
        if (!m_hasInitialState)
        {
            m_hasInitialState = true;
            m_initialHdrActive = current.IsHdrOutputActive;
            AppendNativePlaybackDiagnostic(current.IsHdrOutputActive
                ? L"HdrDisplayController.EnterHdr10 initial active true"
                : L"HdrDisplayController.EnterHdr10 initial active false");
        }

        if (current.IsHdrOutputActive)
        {
            if (!HdrDisplayRefreshRatePolicy::HasUsableVideoFrameRate(videoFrameRate))
            {
                AppendNativePlaybackDiagnostic(L"HdrDisplayController.EnterHdr10 already active");
                return current;
            }
        }

        AppendNativePlaybackDiagnostic(L"HdrDisplayController.EnterHdr10 Apply(true) begin");
        return Apply(true, videoFrameRate);
    }

    HdrDisplaySnapshot HdrDisplayController::LeaveHdr10()
    {
        AppendNativePlaybackDiagnostic(L"HdrDisplayController.LeaveHdr10 begin");
        return Apply(false, 0.0);
    }

    HdrDisplaySnapshot HdrDisplayController::RestoreInitialState()
    {
        AppendNativePlaybackDiagnostic(L"HdrDisplayController.RestoreInitialState begin");
        if (!m_hasInitialState)
        {
            return Probe();
        }

        return Apply(m_initialHdrActive, m_initialRefreshRate);
    }

    HdrDisplaySnapshot HdrDisplayController::Apply(bool enableHdr, double preferredRefreshRate)
    {
        AppendNativePlaybackDiagnostic(enableHdr
            ? L"HdrDisplayController.Apply enable begin"
            : L"HdrDisplayController.Apply disable begin");
        HdrDisplaySnapshot snapshot;

        if (!ApiInformation::IsTypePresent(L"Windows.Graphics.Display.Core.HdmiDisplayInformation"))
        {
            snapshot.Status = NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
            snapshot.Message = L"HdmiDisplayInformation is unavailable.";
            AppendNativePlaybackDiagnostic(L"HdrDisplayController.Apply HdmiDisplayInformation unavailable");
            return snapshot;
        }

        AppendNativePlaybackDiagnostic(L"HdrDisplayController.Apply GetForCurrentView begin");
        auto hdmi = HdmiDisplayInformation::GetForCurrentView();
        AppendNativePlaybackDiagnostic(hdmi != nullptr
            ? L"HdrDisplayController.Apply GetForCurrentView end available"
            : L"HdrDisplayController.Apply GetForCurrentView end null");
        if (hdmi == nullptr)
        {
            snapshot.Status = NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
            snapshot.Message = L"No HDMI display information is available.";
            return snapshot;
        }

        AppendNativePlaybackDiagnostic(L"HdrDisplayController.Apply GetCurrentDisplayMode begin");
        auto mode = hdmi.GetCurrentDisplayMode();
        AppendNativePlaybackDiagnostic(mode != nullptr
            ? L"HdrDisplayController.Apply GetCurrentDisplayMode end available"
            : L"HdrDisplayController.Apply GetCurrentDisplayMode end null");
        if (mode == nullptr)
        {
            snapshot.Status = NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Failed;
            snapshot.Message = L"Current HDMI display mode is unavailable.";
            return snapshot;
        }

        CaptureRefreshRate(snapshot, mode);
        if (m_hasInitialState && m_initialRefreshRate <= 0.0)
        {
            m_initialRefreshRate = mode.RefreshRate();
            AppendNativePlaybackDiagnostic(
                L"HdrDisplayController.Apply captured initial refresh=" +
                std::to_wstring(m_initialRefreshRate));
        }

        auto targetMode = mode;
        if (enableHdr || HdrDisplayRefreshRatePolicy::HasUsableVideoFrameRate(preferredRefreshRate))
        {
            AppendNativePlaybackDiagnostic(L"HdrDisplayController.Apply FindDisplayMode begin");
            targetMode = FindDisplayMode(hdmi, mode, enableHdr, preferredRefreshRate);
            AppendNativePlaybackDiagnostic(L"HdrDisplayController.Apply FindDisplayMode end");
            if (targetMode == nullptr)
            {
                snapshot.Status = NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
                snapshot.Message = enableHdr
                    ? L"No matching BT.2020 HDMI display mode is available for HDR10."
                    : L"No matching SDR HDMI display mode is available.";
                AppendNativePlaybackDiagnostic(L"HdrDisplayController.Apply no matching mode");
                return snapshot;
            }
        }

        auto option = enableHdr ? HdmiDisplayHdrOption::Eotf2084 : HdmiDisplayHdrOption::None;
        AppendNativePlaybackDiagnostic(enableHdr
            ? L"HdrDisplayController.Apply RequestSetCurrentDisplayModeAsync HDR begin"
            : L"HdrDisplayController.Apply RequestSetCurrentDisplayModeAsync SDR begin");
        auto operation = hdmi.RequestSetCurrentDisplayModeAsync(targetMode, option);
        AppendNativePlaybackDiagnostic(L"HdrDisplayController.Apply RequestSetCurrentDisplayModeAsync get begin");
        auto result = WaitForDisplayModeResult(operation);
        AppendNativePlaybackDiagnostic(result
            ? L"HdrDisplayController.Apply RequestSetCurrentDisplayModeAsync get success"
            : L"HdrDisplayController.Apply RequestSetCurrentDisplayModeAsync get failed");

        if (!result)
        {
            CaptureRefreshRate(snapshot, mode);
            snapshot.Status = NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Failed;
            snapshot.Message = enableHdr ? L"Failed to enter HDR10 display mode." : L"Failed to restore SDR display mode.";
            return snapshot;
        }

        CaptureRefreshRate(snapshot, targetMode);
        snapshot.IsHdrDisplayAvailable = true;
        snapshot.IsHdrOutputActive = enableHdr;
        snapshot.Status = enableHdr
            ? NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_On
            : NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Off;
        AppendNativePlaybackDiagnostic(enableHdr
            ? L"HdrDisplayController.Apply enable end active"
            : L"HdrDisplayController.Apply disable end inactive");
        return snapshot;
    }
}
