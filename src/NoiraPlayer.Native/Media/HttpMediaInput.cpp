#include "pch.h"
#include "HttpMediaInput.h"

#include <chrono>

#pragma warning(push)
#pragma warning(disable : 4244 4819)
extern "C"
{
#include <libavformat/avformat.h>
#include <libavutil/error.h>
#include <libavutil/mem.h>
}
#pragma warning(pop)

namespace winrt::NoiraPlayer::Native::implementation
{
    using winrt::Windows::Foundation::Uri;

    namespace
    {
        constexpr int OuterBufferSize = 32 * 1024;

        double ElapsedMilliseconds(std::chrono::steady_clock::time_point startedAt) noexcept
        {
            return std::chrono::duration<double, std::milli>(
                std::chrono::steady_clock::now() - startedAt).count();
        }

        uint64_t AbsoluteDistance(int64_t left, int64_t right) noexcept
        {
            if (left < 0 || right < 0)
            {
                return 0;
            }

            auto distance = left > right ? left - right : right - left;
            return static_cast<uint64_t>(distance);
        }
    }

    HttpMediaInput::~HttpMediaInput()
    {
        Close();
    }

    void HttpMediaInput::ValidateUrl(winrt::hstring const& url)
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
    }

    void HttpMediaInput::Open(
        std::string const& source,
        AVDictionary** options,
        AVIOInterruptCB const& interruptCallback)
    {
        if (source.empty())
        {
            throw winrt::hresult_invalid_argument(L"Media input source is required.");
        }

        Close();
        m_snapshot = {};

        auto result = avio_open2(
            &m_innerContext,
            source.c_str(),
            AVIO_FLAG_READ,
            &interruptCallback,
            options);
        if (result < 0)
        {
            m_snapshot.LastError = result;
            throw winrt::hresult_error(E_FAIL, L"avio_open2 failed for instrumented media input.");
        }

        m_expectedSize = avio_size(m_innerContext);

        auto buffer = static_cast<uint8_t*>(av_malloc(OuterBufferSize));
        if (buffer == nullptr)
        {
            Close();
            throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not allocate instrumented AVIO buffer.");
        }

        m_outerContext = avio_alloc_context(
            buffer,
            OuterBufferSize,
            0,
            this,
            &HttpMediaInput::ReadCallback,
            nullptr,
            &HttpMediaInput::SeekCallback);
        if (m_outerContext == nullptr)
        {
            av_free(buffer);
            Close();
            throw winrt::hresult_error(E_OUTOFMEMORY, L"Could not allocate instrumented AVIO context.");
        }

        m_outerContext->seekable = m_innerContext->seekable;
        m_outerContext->max_packet_size = m_innerContext->max_packet_size;
        m_source = source;
        m_pendingReadError = 0;
        m_snapshot.EvidenceAvailable = true;
        m_open = true;
    }

    void HttpMediaInput::Attach(AVFormatContext* formatContext)
    {
        if (!m_open || m_outerContext == nullptr || formatContext == nullptr)
        {
            throw winrt::hresult_invalid_argument(L"Instrumented media input must be open before attach.");
        }

        formatContext->pb = m_outerContext;
        formatContext->flags |= AVFMT_FLAG_CUSTOM_IO;
    }

    bool HttpMediaInput::ReopenAt(
        int64_t byteOffset,
        AVDictionary** options,
        AVIOInterruptCB const& interruptCallback,
        int& errorCode) noexcept
    {
        errorCode = AVERROR(EIO);
        if (byteOffset < 0 || m_source.empty() || m_outerContext == nullptr)
        {
            return false;
        }

        if (m_innerContext != nullptr)
        {
            avio_closep(&m_innerContext);
        }

        auto result = avio_open2(
            &m_innerContext,
            m_source.c_str(),
            AVIO_FLAG_READ,
            &interruptCallback,
            options);
        if (result < 0)
        {
            m_snapshot.LastError = result;
            m_open = false;
            errorCode = result;
            return false;
        }

        m_outerContext->buf_ptr = m_outerContext->buffer;
        m_outerContext->buf_end = m_outerContext->buffer;
        m_outerContext->pos = byteOffset;
        m_outerContext->error = 0;
        m_outerContext->eof_reached = 0;
        m_outerContext->seekable = m_innerContext->seekable;
        m_outerContext->max_packet_size = m_innerContext->max_packet_size;
        m_pendingReadError = 0;
        m_open = true;
        errorCode = 0;
        return true;
    }

    int HttpMediaInput::ReadCallback(void* opaque, uint8_t* buffer, int bufferLength) noexcept
    {
        auto self = static_cast<HttpMediaInput*>(opaque);
        if (self == nullptr || self->m_innerContext == nullptr || buffer == nullptr || bufferLength <= 0)
        {
            return AVERROR(EINVAL);
        }

        auto startedAt = std::chrono::steady_clock::now();
        auto result = avio_read(self->m_innerContext, buffer, bufferLength);
        self->m_snapshot.ReadWaitMs += ElapsedMilliseconds(startedAt);
        ++self->m_snapshot.ReadCalls;
        if (result > 0)
        {
            self->m_snapshot.BytesRead += static_cast<uint64_t>(result);
            self->m_pendingReadError = 0;
        }
        else if (result == AVERROR_EOF)
        {
            auto const position = avio_tell(self->m_innerContext);
            if (self->m_expectedSize > 0 && position >= 0 && position < self->m_expectedSize)
            {
                result = AVERROR(EIO);
                self->m_snapshot.LastError = result;
                self->m_pendingReadError = result;
            }
        }
        else
        {
            self->m_snapshot.LastError = result;
            self->m_pendingReadError = result;
        }

        return result;
    }

    int64_t HttpMediaInput::SeekCallback(void* opaque, int64_t offset, int whence) noexcept
    {
        auto self = static_cast<HttpMediaInput*>(opaque);
        if (self == nullptr || self->m_innerContext == nullptr)
        {
            return AVERROR(EINVAL);
        }

        auto startedAt = std::chrono::steady_clock::now();
        auto before = avio_tell(self->m_innerContext);
        auto const isSizeQuery = (whence & AVSEEK_SIZE) != 0;
        auto result = isSizeQuery
            ? avio_size(self->m_innerContext)
            : avio_seek(self->m_innerContext, offset, whence);
        auto const waitMs = ElapsedMilliseconds(startedAt);
        self->m_snapshot.SeekWaitMs += waitMs;
        ++self->m_snapshot.SeekCalls;
        if (isSizeQuery)
        {
            ++self->m_snapshot.SizeQueryCalls;
            self->m_snapshot.SizeQueryWaitMs += waitMs;
        }
        else
        {
            ++self->m_snapshot.DataSeekCalls;
            self->m_snapshot.DataSeekWaitMs += waitMs;
        }

        if (result >= 0 && !isSizeQuery)
        {
            auto const distance = AbsoluteDistance(before, result);
            self->m_snapshot.SeekDistanceBytes += distance;
            self->m_snapshot.DataSeekDistanceBytes += distance;
            if (result > before)
            {
                ++self->m_snapshot.ForwardDataSeekCalls;
                self->m_snapshot.ForwardDataSeekWaitMs += waitMs;
                self->m_snapshot.ForwardDataSeekDistanceBytes += distance;
            }
            else if (result < before)
            {
                ++self->m_snapshot.BackwardDataSeekCalls;
                self->m_snapshot.BackwardDataSeekWaitMs += waitMs;
                self->m_snapshot.BackwardDataSeekDistanceBytes += distance;
            }
            else
            {
                ++self->m_snapshot.NoOpDataSeekCalls;
                self->m_snapshot.NoOpDataSeekWaitMs += waitMs;
            }
        }
        else if (result < 0)
        {
            self->m_snapshot.LastError = static_cast<int32_t>(result);
        }

        return result;
    }

    void HttpMediaInput::Close() noexcept
    {
        if (m_outerContext != nullptr)
        {
            av_freep(&m_outerContext->buffer);
            avio_context_free(&m_outerContext);
        }

        if (m_innerContext != nullptr)
        {
            avio_closep(&m_innerContext);
        }

        m_source.clear();
        m_expectedSize = -1;
        m_pendingReadError = 0;
        m_open = false;
    }

    bool HttpMediaInput::IsOpen() const noexcept
    {
        return m_open;
    }

    int HttpMediaInput::PendingReadError() const noexcept
    {
        return m_pendingReadError;
    }

    HttpMediaInputSnapshot HttpMediaInput::Snapshot() const noexcept
    {
        return m_snapshot;
    }
}
