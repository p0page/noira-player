#pragma once

#include <string_view>

namespace winrt::NextGenEmby::Native::implementation
{
    void AppendNativePlaybackDiagnostic(std::wstring_view message) noexcept;
}
