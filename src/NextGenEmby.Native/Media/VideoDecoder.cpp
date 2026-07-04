#include "pch.h"
#include "VideoDecoder.h"
#include "HttpMediaInput.h"

#pragma warning(push)
#pragma warning(disable : 4244 4819)
extern "C"
{
#include <libavformat/avformat.h>
}
#pragma warning(pop)

namespace winrt::NextGenEmby::Native::implementation
{
    void VideoDecoder::Open(winrt::hstring const& url, int32_t)
    {
        HttpMediaInput input;
        input.Open(url);

        m_url = url;
        m_avformatVersion = static_cast<uint32_t>(avformat_version());
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
        m_avformatVersion = 0;
        m_positionTicks = 0;
        m_open = false;
    }
}
