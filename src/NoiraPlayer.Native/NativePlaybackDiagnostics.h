#pragma once

#include <string_view>

namespace winrt::NoiraPlayer::Native::implementation
{
    void AppendNativePlaybackDiagnostic(std::wstring_view message) noexcept;
}
