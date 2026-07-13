#pragma once

#include <cstdint>
#include <string>
#include <winrt/Windows.Foundation.h>

struct AVDictionary;
struct AVFormatContext;
struct AVIOContext;
struct AVIOInterruptCB;

namespace winrt::NoiraPlayer::Native::implementation
{
    struct HttpMediaInputSnapshot
    {
        std::string Provider{"instrumented-ffmpeg-avio"};
        bool EvidenceAvailable{false};
        uint64_t ReadCalls{0};
        uint64_t SeekCalls{0};
        uint64_t BytesRead{0};
        uint64_t SeekDistanceBytes{0};
        double ReadWaitMs{0.0};
        double SeekWaitMs{0.0};
        int32_t LastError{0};
    };

    class HttpMediaInput
    {
    public:
        ~HttpMediaInput();

        static void ValidateUrl(winrt::hstring const& url);
        void Open(
            std::string const& source,
            AVDictionary** options,
            AVIOInterruptCB const& interruptCallback);
        void Attach(AVFormatContext* formatContext);
        bool ReopenAt(
            int64_t byteOffset,
            AVDictionary** options,
            AVIOInterruptCB const& interruptCallback,
            int& errorCode) noexcept;
        void Close() noexcept;

        bool IsOpen() const noexcept;
        int PendingReadError() const noexcept;
        HttpMediaInputSnapshot Snapshot() const noexcept;

    private:
        static int ReadCallback(void* opaque, uint8_t* buffer, int bufferLength) noexcept;
        static int64_t SeekCallback(void* opaque, int64_t offset, int whence) noexcept;

        AVIOContext* m_innerContext{nullptr};
        AVIOContext* m_outerContext{nullptr};
        std::string m_source;
        HttpMediaInputSnapshot m_snapshot;
        int64_t m_expectedSize{-1};
        int m_pendingReadError{0};
        bool m_open{false};
    };
}
