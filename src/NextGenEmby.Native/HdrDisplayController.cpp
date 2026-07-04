#include "pch.h"
#include "HdrDisplayController.h"

#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.Graphics.Display.h>
#include <winrt/Windows.Graphics.Display.Core.h>

namespace winrt::NextGenEmby::Native::implementation
{
    using namespace winrt::Windows::Foundation::Metadata;
    using namespace winrt::Windows::Graphics::Display;
    using namespace winrt::Windows::Graphics::Display::Core;

    HdrDisplaySnapshot HdrDisplayController::Probe()
    {
        HdrDisplaySnapshot snapshot;

        auto info = DisplayInformation::GetForCurrentView();
        if (info != nullptr)
        {
            auto advanced = info.GetAdvancedColorInfo();
            if (advanced != nullptr &&
                advanced.CurrentAdvancedColorKind() == AdvancedColorKind::HighDynamicRange)
            {
                snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_On;
                snapshot.IsHdrDisplayAvailable = true;
                snapshot.IsHdrOutputActive = true;
                return snapshot;
            }
        }

        if (!ApiInformation::IsTypePresent(L"Windows.Graphics.Display.Core.HdmiDisplayInformation"))
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
            snapshot.Message = L"HdmiDisplayInformation is unavailable.";
            return snapshot;
        }

        auto hdmi = HdmiDisplayInformation::GetForCurrentView();
        if (hdmi == nullptr)
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
            snapshot.Message = L"No HDMI display information is available.";
            return snapshot;
        }

        auto current = hdmi.GetCurrentDisplayMode();
        snapshot.IsHdrDisplayAvailable = current != nullptr && current.IsSmpte2084Supported();
        snapshot.IsHdrOutputActive = false;
        snapshot.Status = snapshot.IsHdrDisplayAvailable
            ? NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Off
            : NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
        return snapshot;
    }

    HdrDisplaySnapshot HdrDisplayController::EnterHdr10()
    {
        auto current = Probe();
        if (!m_hasInitialState)
        {
            m_hasInitialState = true;
            m_initialHdrActive = current.IsHdrOutputActive;
        }

        if (current.IsHdrOutputActive)
        {
            return current;
        }

        return Apply(true);
    }

    HdrDisplaySnapshot HdrDisplayController::RestoreInitialState()
    {
        if (!m_hasInitialState)
        {
            return Probe();
        }

        return Apply(m_initialHdrActive);
    }

    HdrDisplaySnapshot HdrDisplayController::Apply(bool enableHdr)
    {
        HdrDisplaySnapshot snapshot;

        if (!ApiInformation::IsTypePresent(L"Windows.Graphics.Display.Core.HdmiDisplayInformation"))
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
            snapshot.Message = L"HdmiDisplayInformation is unavailable.";
            return snapshot;
        }

        auto hdmi = HdmiDisplayInformation::GetForCurrentView();
        if (hdmi == nullptr)
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unsupported;
            snapshot.Message = L"No HDMI display information is available.";
            return snapshot;
        }

        auto mode = hdmi.GetCurrentDisplayMode();
        if (mode == nullptr)
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Failed;
            snapshot.Message = L"Current HDMI display mode is unavailable.";
            return snapshot;
        }

        auto option = enableHdr ? HdmiDisplayHdrOption::Eotf2084 : HdmiDisplayHdrOption::None;
        auto operation = hdmi.RequestSetCurrentDisplayModeAsync(mode, option);
        auto result = operation.get();

        if (!result)
        {
            snapshot.Status = NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Failed;
            snapshot.Message = enableHdr ? L"Failed to enter HDR10 display mode." : L"Failed to restore SDR display mode.";
            return snapshot;
        }

        snapshot.IsHdrDisplayAvailable = true;
        snapshot.IsHdrOutputActive = enableHdr;
        snapshot.Status = enableHdr
            ? NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_On
            : NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Off;
        return snapshot;
    }
}
