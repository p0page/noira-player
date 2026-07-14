#include <cassert>

#include "Media/DecoderEagainRecovery.h"

using winrt::NoiraPlayer::Native::implementation::DecoderEagainRecovery;

int main()
{
    DecoderEagainRecovery recovery;
    assert(recovery.ConsecutiveRetryCount() == 0);
    assert(recovery.SendPacketEagainCount() == 0);
    assert(recovery.DoubleEagainRetryCount() == 0);
    assert(recovery.DoubleEagainRecoveryCount() == 0);
    assert(recovery.DoubleEagainExhaustedCount() == 0);

    for (uint32_t retry = 1; retry <= DecoderEagainRecovery::MaxConsecutiveRetries; ++retry)
    {
        recovery.RecordSendPacketEagain();
        assert(recovery.TryRecover());
        assert(recovery.ConsecutiveRetryCount() == retry);
        assert(recovery.SendPacketEagainCount() == retry);
        assert(recovery.DoubleEagainRetryCount() == retry);
    }

    recovery.RecordSendPacketEagain();
    assert(!recovery.TryRecover());
    assert(recovery.ConsecutiveRetryCount() == DecoderEagainRecovery::MaxConsecutiveRetries);
    assert(recovery.SendPacketEagainCount() == DecoderEagainRecovery::MaxConsecutiveRetries + 1);
    assert(recovery.DoubleEagainRetryCount() == DecoderEagainRecovery::MaxConsecutiveRetries);
    assert(recovery.DoubleEagainRecoveryCount() == 0);
    assert(recovery.DoubleEagainExhaustedCount() == 1);

    recovery.RecordProgress();
    assert(recovery.ConsecutiveRetryCount() == 0);
    assert(recovery.DoubleEagainRecoveryCount() == 0);
    recovery.RecordSendPacketEagain();
    assert(recovery.TryRecover());
    recovery.RecordProgress();
    assert(recovery.DoubleEagainRecoveryCount() == 1);

    recovery.Reset();
    assert(recovery.ConsecutiveRetryCount() == 0);
    assert(recovery.SendPacketEagainCount() == 0);
    assert(recovery.DoubleEagainRetryCount() == 0);
    assert(recovery.DoubleEagainRecoveryCount() == 0);
    assert(recovery.DoubleEagainExhaustedCount() == 0);
}
