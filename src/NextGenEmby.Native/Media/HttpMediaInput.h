#pragma once

#include <cstdint>

namespace winrt::NextGenEmby::Native::implementation
{
    class HttpMediaInput
    {
    public:
        void Open(winrt::hstring const& url);
        uint32_t Read(uint8_t* buffer, uint32_t bufferLength);
        void Close() noexcept;

        bool IsOpen() const noexcept;
        winrt::hstring Url() const noexcept;

    private:
        winrt::hstring m_url;
        bool m_open{false};
    };
}
