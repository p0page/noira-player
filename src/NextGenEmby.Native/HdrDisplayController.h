#pragma once

#include "NativePlaybackEngine.g.h"

#include <winrt/Windows.Graphics.Display.Core.h>

namespace winrt::NextGenEmby::Native::implementation
{
    struct HdrDisplaySnapshot
    {
        NextGenEmby::Native::NativeHdrStatus Status{NextGenEmby::Native::NativeHdrStatus::NativeHdrStatus_Unknown};
        bool IsHdrDisplayAvailable{false};
        bool IsHdrOutputActive{false};
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

        bool m_hasInitialState{false};
        bool m_initialHdrActive{false};
        double m_initialRefreshRate{0.0};
    };
}
