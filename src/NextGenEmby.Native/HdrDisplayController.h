#pragma once

#include "NativePlaybackEngine.g.h"

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
        HdrDisplaySnapshot EnterHdr10();
        HdrDisplaySnapshot RestoreInitialState();

    private:
        HdrDisplaySnapshot Apply(bool enableHdr);

        bool m_hasInitialState{false};
        bool m_initialHdrActive{false};
    };
}
