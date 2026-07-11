#pragma once

#include "NativePlaybackEngine.g.h"

#include <winrt/Windows.Graphics.Display.Core.h>

namespace winrt::NoiraPlayer::Native::implementation
{
    struct HdrDisplaySnapshot
    {
        NoiraPlayer::Native::NativeHdrStatus Status{NoiraPlayer::Native::NativeHdrStatus::NativeHdrStatus_Unknown};
        bool IsHdrDisplayAvailable{false};
        bool IsHdrOutputActive{false};
        double RefreshRateHz{0.0};
        winrt::hstring Message{};
    };

    class HdrDisplayController
    {
    public:
        HdrDisplaySnapshot Probe();
        HdrDisplaySnapshot EnterHdr10(double videoFrameRate);
        HdrDisplaySnapshot LeaveHdr10();
        HdrDisplaySnapshot RestoreInitialState();

    private:
        HdrDisplaySnapshot Apply(bool enableHdr, double preferredRefreshRate);
        HdrDisplaySnapshot Remember(HdrDisplaySnapshot const& snapshot);
        void CaptureInitialStateIfNeeded(HdrDisplaySnapshot const& snapshot);

        winrt::Windows::Graphics::Display::Core::HdmiDisplayInformation m_hdmi{nullptr};
        HdrDisplaySnapshot m_lastSnapshot{};
        bool m_hasInitialState{false};
        bool m_initialHdrActive{false};
        double m_initialRefreshRate{0.0};
    };
}
