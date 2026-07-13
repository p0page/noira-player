#pragma once

#include <algorithm>
#include <cstdint>

namespace winrt::NoiraPlayer::Native::implementation
{
    enum class FfmpegReadDisposition
    {
        Retry,
        EndOfStream,
        Interrupted,
        Fatal
    };

    struct FfmpegReadRecoverySnapshot
    {
        uint64_t ReadErrorCount{0};
        uint64_t ReadRetryCount{0};
        uint64_t ReadRecoveryCount{0};
        uint32_t ConsecutiveReadErrors{0};
        uint32_t MaxConsecutiveReadErrors{0};
        int LastReadErrorCode{0};
        int FatalReadErrorCode{0};
        double LastReadRecoveryDurationMs{0.0};
    };

    class FfmpegReadRecoveryState
    {
    public:
        static constexpr uint32_t MaxConsecutiveRetries = 10;

        FfmpegReadDisposition ObserveError(
            int errorCode,
            bool endOfStream,
            bool transient,
            bool httpSource,
            bool interrupted,
            bool recoveryEnabled) noexcept
        {
            if (endOfStream)
            {
                return FfmpegReadDisposition::EndOfStream;
            }

            if (interrupted)
            {
                return FfmpegReadDisposition::Interrupted;
            }

            ++m_snapshot.ReadErrorCount;
            ++m_snapshot.ConsecutiveReadErrors;
            m_snapshot.MaxConsecutiveReadErrors = (std::max)(
                m_snapshot.MaxConsecutiveReadErrors,
                m_snapshot.ConsecutiveReadErrors);
            m_snapshot.LastReadErrorCode = errorCode;

            auto const mayRetry = recoveryEnabled && (transient || httpSource);
            if (!mayRetry || m_snapshot.ConsecutiveReadErrors > MaxConsecutiveRetries)
            {
                m_snapshot.FatalReadErrorCode = errorCode;
                return FfmpegReadDisposition::Fatal;
            }

            ++m_snapshot.ReadRetryCount;
            return FfmpegReadDisposition::Retry;
        }

        void RecordPacketRecovered(double recoveryDurationMs) noexcept
        {
            if (m_snapshot.ConsecutiveReadErrors == 0)
            {
                return;
            }

            ++m_snapshot.ReadRecoveryCount;
            m_snapshot.ConsecutiveReadErrors = 0;
            m_snapshot.LastReadRecoveryDurationMs = (std::max)(0.0, recoveryDurationMs);
        }

        FfmpegReadRecoverySnapshot Snapshot() const noexcept
        {
            return m_snapshot;
        }

    private:
        FfmpegReadRecoverySnapshot m_snapshot;
    };
}
