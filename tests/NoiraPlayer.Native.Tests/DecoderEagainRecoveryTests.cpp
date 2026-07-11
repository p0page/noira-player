#include <cassert>

#include "Media/DecoderEagainRecovery.h"

using winrt::NoiraPlayer::Native::implementation::DecoderEagainRecovery;

int main()
{
    DecoderEagainRecovery recovery;
    assert(recovery.ConsecutiveRetryCount() == 0);

    for (uint32_t retry = 1; retry <= DecoderEagainRecovery::MaxConsecutiveRetries; ++retry)
    {
        assert(recovery.TryRecover());
        assert(recovery.ConsecutiveRetryCount() == retry);
    }

    assert(!recovery.TryRecover());
    assert(recovery.ConsecutiveRetryCount() == DecoderEagainRecovery::MaxConsecutiveRetries);

    recovery.RecordProgress();
    assert(recovery.ConsecutiveRetryCount() == 0);
    assert(recovery.TryRecover());
}
