#include <cassert>

#include "Media/FfmpegReadRecovery.h"

using winrt::NoiraPlayer::Native::implementation::FfmpegReadDisposition;
using winrt::NoiraPlayer::Native::implementation::FfmpegReadRecoveryState;

int main()
{
    {
        FfmpegReadRecoveryState recovery;
        auto const disposition = recovery.ObserveError(-1, true, false, false, false, true);
        auto const snapshot = recovery.Snapshot();

        assert(disposition == FfmpegReadDisposition::EndOfStream);
        assert(snapshot.ReadErrorCount == 0);
        assert(snapshot.ReadRetryCount == 0);
        assert(snapshot.FatalReadErrorCode == 0);
    }

    {
        FfmpegReadRecoveryState recovery;
        auto const disposition = recovery.ObserveError(-4, false, true, true, true, true);
        auto const snapshot = recovery.Snapshot();

        assert(disposition == FfmpegReadDisposition::Interrupted);
        assert(snapshot.ReadErrorCount == 0);
        assert(snapshot.ReadRetryCount == 0);
        assert(snapshot.FatalReadErrorCode == 0);
    }

    {
        FfmpegReadRecoveryState recovery;
        auto const disposition = recovery.ObserveError(-5, false, false, false, false, true);
        auto const snapshot = recovery.Snapshot();

        assert(disposition == FfmpegReadDisposition::Fatal);
        assert(snapshot.ReadErrorCount == 1);
        assert(snapshot.ReadRetryCount == 0);
        assert(snapshot.MaxConsecutiveReadErrors == 1);
        assert(snapshot.LastReadErrorCode == -5);
        assert(snapshot.FatalReadErrorCode == -5);
    }

    {
        FfmpegReadRecoveryState recovery;
        auto const disposition = recovery.ObserveError(-11, false, true, false, false, true);
        auto const snapshot = recovery.Snapshot();

        assert(disposition == FfmpegReadDisposition::Retry);
        assert(snapshot.ReadErrorCount == 1);
        assert(snapshot.ReadRetryCount == 1);
        assert(snapshot.FatalReadErrorCode == 0);
    }

    {
        FfmpegReadRecoveryState recovery;
        for (uint32_t retry = 1; retry <= FfmpegReadRecoveryState::MaxConsecutiveRetries; ++retry)
        {
            auto const disposition = recovery.ObserveError(-5, false, false, true, false, true);
            assert(disposition == FfmpegReadDisposition::Retry);
            assert(recovery.Snapshot().ReadRetryCount == retry);
        }

        auto const disposition = recovery.ObserveError(-5, false, false, true, false, true);
        auto const snapshot = recovery.Snapshot();
        assert(disposition == FfmpegReadDisposition::Fatal);
        assert(snapshot.ReadErrorCount == FfmpegReadRecoveryState::MaxConsecutiveRetries + 1);
        assert(snapshot.ReadRetryCount == FfmpegReadRecoveryState::MaxConsecutiveRetries);
        assert(snapshot.MaxConsecutiveReadErrors == FfmpegReadRecoveryState::MaxConsecutiveRetries + 1);
        assert(snapshot.FatalReadErrorCode == -5);
    }

    {
        FfmpegReadRecoveryState recovery;
        auto const disposition = recovery.ObserveError(-5, false, false, true, false, false);
        auto const snapshot = recovery.Snapshot();

        assert(disposition == FfmpegReadDisposition::Fatal);
        assert(snapshot.ReadErrorCount == 1);
        assert(snapshot.ReadRetryCount == 0);
        assert(snapshot.FatalReadErrorCode == -5);
    }

    {
        FfmpegReadRecoveryState recovery;
        assert(recovery.ObserveError(-5, false, false, true, false, true) ==
            FfmpegReadDisposition::Retry);
        recovery.RecordPacketRecovered(37.5);

        auto snapshot = recovery.Snapshot();
        assert(snapshot.ReadRecoveryCount == 1);
        assert(snapshot.ConsecutiveReadErrors == 0);
        assert(snapshot.LastReadRecoveryDurationMs == 37.5);
        assert(snapshot.FatalReadErrorCode == 0);

        assert(recovery.ObserveError(-5, false, false, true, false, true) ==
            FfmpegReadDisposition::Retry);
        recovery.RecordPacketRecovered(12.25);
        snapshot = recovery.Snapshot();
        assert(snapshot.ReadErrorCount == 2);
        assert(snapshot.ReadRetryCount == 2);
        assert(snapshot.ReadRecoveryCount == 2);
        assert(snapshot.MaxConsecutiveReadErrors == 1);
        assert(snapshot.LastReadRecoveryDurationMs == 12.25);
    }
}
