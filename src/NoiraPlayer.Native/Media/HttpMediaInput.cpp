#include "pch.h"
#include "HttpMediaInput.h"

namespace winrt::NoiraPlayer::Native::implementation
{
    using winrt::Windows::Foundation::Uri;

    void HttpMediaInput::Open(winrt::hstring const& url)
    {
        if (url.empty())
        {
            throw winrt::hresult_invalid_argument(L"Direct stream URL is required.");
        }

        Uri uri{url};
        auto scheme = uri.SchemeName();
        if (scheme != L"http" && scheme != L"https" && scheme != L"file")
        {
            throw winrt::hresult_invalid_argument(L"Direct stream URL must use HTTP, HTTPS, or file.");
        }

        if (scheme != L"file" && uri.Host().empty())
        {
            throw winrt::hresult_invalid_argument(L"Direct stream URL must include a host.");
        }

        m_url = url;
        m_open = true;
    }

    uint32_t HttpMediaInput::Read(uint8_t* buffer, uint32_t bufferLength)
    {
        if (!m_open)
        {
            throw winrt::hresult_invalid_argument(L"HTTP media input is not open.");
        }

        if (bufferLength > 0 && buffer == nullptr)
        {
            throw winrt::hresult_invalid_argument(L"Read buffer is required.");
        }

        return 0;
    }

    void HttpMediaInput::Close() noexcept
    {
        m_url.clear();
        m_open = false;
    }

    bool HttpMediaInput::IsOpen() const noexcept
    {
        return m_open;
    }

    winrt::hstring HttpMediaInput::Url() const noexcept
    {
        return m_url;
    }
}
