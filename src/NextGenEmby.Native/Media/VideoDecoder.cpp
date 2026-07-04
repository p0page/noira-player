#include "pch.h"
#include "VideoDecoder.h"
#include "HttpMediaInput.h"

namespace winrt::NextGenEmby::Native::implementation
{
    void VideoDecoder::Open(winrt::hstring const& url, int32_t)
    {
        HttpMediaInput input;
        input.Open(url);

        m_url = url;
        m_positionTicks = 0;
        m_open = true;
    }

    std::optional<DecodedVideoFrame> VideoDecoder::TryReadFrame()
    {
        if (!m_open)
        {
            return std::nullopt;
        }

        return std::nullopt;
    }

    void VideoDecoder::Seek(int64_t positionTicks)
    {
        if (positionTicks < 0)
        {
            throw winrt::hresult_invalid_argument(L"Seek position cannot be negative.");
        }

        m_positionTicks = positionTicks;
    }

    void VideoDecoder::Close() noexcept
    {
        m_url.clear();
        m_positionTicks = 0;
        m_open = false;
    }
}
