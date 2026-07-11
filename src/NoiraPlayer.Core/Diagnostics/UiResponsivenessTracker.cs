using System;

namespace NoiraPlayer.Core.Diagnostics
{
    public sealed class UiResponsivenessSnapshot
    {
        public UiResponsivenessSnapshot(
            int completedProbeCount,
            int skippedProbeCount,
            TimeSpan maximumDispatchLatency,
            TimeSpan pendingDuration)
        {
            CompletedProbeCount = completedProbeCount;
            SkippedProbeCount = skippedProbeCount;
            MaximumDispatchLatency = maximumDispatchLatency;
            PendingDuration = pendingDuration;
        }

        public int CompletedProbeCount { get; }

        public int SkippedProbeCount { get; }

        public TimeSpan MaximumDispatchLatency { get; }

        public TimeSpan PendingDuration { get; }
    }

    public sealed class UiResponsivenessTracker
    {
        private readonly object _gate = new object();
        private bool _probePending;
        private TimeSpan _pendingStartedAt;
        private int _completedProbeCount;
        private int _skippedProbeCount;
        private TimeSpan _maximumDispatchLatency;

        public bool TryBeginProbe(TimeSpan now)
        {
            lock (_gate)
            {
                if (_probePending)
                {
                    _skippedProbeCount++;
                    return false;
                }

                _probePending = true;
                _pendingStartedAt = now;
                return true;
            }
        }

        public void CompleteProbe(TimeSpan now)
        {
            lock (_gate)
            {
                if (!_probePending)
                {
                    return;
                }

                var latency = ClampElapsed(now - _pendingStartedAt);
                if (latency > _maximumDispatchLatency)
                {
                    _maximumDispatchLatency = latency;
                }

                _completedProbeCount++;
                _probePending = false;
            }
        }

        public void CancelProbe()
        {
            lock (_gate)
            {
                _probePending = false;
            }
        }

        public UiResponsivenessSnapshot CaptureAndReset(TimeSpan now)
        {
            lock (_gate)
            {
                var pendingDuration = _probePending
                    ? ClampElapsed(now - _pendingStartedAt)
                    : TimeSpan.Zero;
                var snapshot = new UiResponsivenessSnapshot(
                    _completedProbeCount,
                    _skippedProbeCount,
                    _maximumDispatchLatency,
                    pendingDuration);

                _completedProbeCount = 0;
                _skippedProbeCount = 0;
                _maximumDispatchLatency = TimeSpan.Zero;
                return snapshot;
            }
        }

        private static TimeSpan ClampElapsed(TimeSpan elapsed)
        {
            return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }
    }
}
