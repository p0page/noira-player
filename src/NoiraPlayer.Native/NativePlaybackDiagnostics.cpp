#include "pch.h"
#include "NativePlaybackDiagnostics.h"

#include <winrt/Windows.Storage.h>

#include <string>
#include <windows.h>

namespace winrt::NoiraPlayer::Native::implementation
{
    namespace
    {
        std::wstring TimestampPrefix()
        {
            SYSTEMTIME now{};
            GetLocalTime(&now);

            wchar_t buffer[64]{};
            swprintf_s(
                buffer,
                L"%04hu-%02hu-%02huT%02hu:%02hu:%02hu.%03hu",
                now.wYear,
                now.wMonth,
                now.wDay,
                now.wHour,
                now.wMinute,
                now.wSecond,
                now.wMilliseconds);
            return buffer;
        }
    }

    void AppendNativePlaybackDiagnostic(std::wstring_view message) noexcept
    {
        try
        {
            auto folder = winrt::Windows::Storage::ApplicationData::Current().LocalFolder();
            auto path = std::wstring(folder.Path()) + L"\\playback-diagnostics.log";
            auto handle = CreateFile2(
                path.c_str(),
                FILE_APPEND_DATA,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                OPEN_ALWAYS,
                nullptr);
            if (handle == INVALID_HANDLE_VALUE)
            {
                return;
            }

            auto line = TimestampPrefix() + L" [native] " + std::wstring(message) + L"\r\n";
            auto utf8 = winrt::to_string(winrt::hstring(line));
            DWORD written = 0;
            (void)WriteFile(handle, utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr);
            CloseHandle(handle);
        }
        catch (...)
        {
        }
    }
}
