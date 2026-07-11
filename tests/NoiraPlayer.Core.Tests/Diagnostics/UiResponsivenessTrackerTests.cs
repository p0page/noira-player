using System;
using NoiraPlayer.Core.Diagnostics;
using Xunit;

namespace NoiraPlayer.Core.Tests.Diagnostics;

public sealed class UiResponsivenessTrackerTests
{
    [Fact]
    public void Completed_Probe_Records_Dispatch_Latency()
    {
        var tracker = new UiResponsivenessTracker();

        Assert.True(tracker.TryBeginProbe(TimeSpan.FromSeconds(1)));
        tracker.CompleteProbe(TimeSpan.FromSeconds(1.12));

        var snapshot = tracker.CaptureAndReset(TimeSpan.FromSeconds(2));
        Assert.Equal(1, snapshot.CompletedProbeCount);
        Assert.Equal(TimeSpan.FromMilliseconds(120), snapshot.MaximumDispatchLatency);
        Assert.Equal(TimeSpan.Zero, snapshot.PendingDuration);
    }

    [Fact]
    public void Pending_Probe_Reports_Stall_And_Skips_Overlapping_Probe()
    {
        var tracker = new UiResponsivenessTracker();

        Assert.True(tracker.TryBeginProbe(TimeSpan.FromSeconds(2)));
        Assert.False(tracker.TryBeginProbe(TimeSpan.FromSeconds(2.25)));

        var snapshot = tracker.CaptureAndReset(TimeSpan.FromSeconds(3.5));
        Assert.Equal(1, snapshot.SkippedProbeCount);
        Assert.Equal(TimeSpan.FromSeconds(1.5), snapshot.PendingDuration);
    }

    [Fact]
    public void Reset_Preserves_An_Outstanding_Probe()
    {
        var tracker = new UiResponsivenessTracker();
        tracker.TryBeginProbe(TimeSpan.FromSeconds(4));

        tracker.CaptureAndReset(TimeSpan.FromSeconds(5));
        tracker.CompleteProbe(TimeSpan.FromSeconds(5.25));

        var snapshot = tracker.CaptureAndReset(TimeSpan.FromSeconds(6));
        Assert.Equal(1, snapshot.CompletedProbeCount);
        Assert.Equal(TimeSpan.FromSeconds(1.25), snapshot.MaximumDispatchLatency);
    }
}
