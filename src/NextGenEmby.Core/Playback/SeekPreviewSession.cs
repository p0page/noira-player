using System;

namespace NextGenEmby.Core.Playback;

public enum SeekPreviewDecisionKind
{
    None,
    Commit,
    Cancel
}

public readonly struct SeekPreviewDecision
{
    public SeekPreviewDecision(SeekPreviewDecisionKind kind, long positionTicks)
    {
        Kind = kind;
        PositionTicks = positionTicks;
    }

    public SeekPreviewDecisionKind Kind { get; }

    public long PositionTicks { get; }
}

public sealed class SeekPreviewSession
{
    private readonly TimeSpan _autoCommitDelay;
    private readonly long _commitThresholdTicks;
    private readonly double _thumbstickDeadZone;
    private readonly long _thumbstickStepTicks;

    public SeekPreviewSession(TimeSpan autoCommitDelay, TimeSpan thumbstickStep, double thumbstickDeadZone)
    {
        if (autoCommitDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(autoCommitDelay));
        }

        if (thumbstickStep < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(thumbstickStep));
        }

        if (thumbstickDeadZone < 0 || thumbstickDeadZone > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(thumbstickDeadZone));
        }

        _autoCommitDelay = autoCommitDelay;
        _commitThresholdTicks = ToPositionTicks(thumbstickStep);
        _thumbstickDeadZone = thumbstickDeadZone;
        _thumbstickStepTicks = ToPositionTicks(thumbstickStep);
    }

    public bool IsActive { get; private set; }

    public long OriginalTicks { get; private set; }

    public long TargetTicks { get; private set; }

    public TimeSpan AutoCommitAt { get; private set; }

    public void Begin(long currentTicks, TimeSpan now)
    {
        OriginalTicks = Math.Max(0, currentTicks);
        TargetTicks = OriginalTicks;
        AutoCommitAt = now + _autoCommitDelay;
        IsActive = true;
    }

    public bool BeginFromThumbstick(long currentTicks, double thumbstickX, TimeSpan now)
    {
        if (Math.Abs(thumbstickX) < _thumbstickDeadZone)
        {
            return false;
        }

        Begin(currentTicks, now);
        MoveByTicks(thumbstickX < 0 ? -_thumbstickStepTicks : _thumbstickStepTicks, now);
        return true;
    }

    public void MoveBy(TimeSpan offset, TimeSpan now)
    {
        if (!IsActive)
        {
            return;
        }

        MoveByTicks(ToPositionTicks(offset), now);
    }

    public SeekPreviewDecision Confirm()
    {
        if (!IsActive)
        {
            return new SeekPreviewDecision(SeekPreviewDecisionKind.None, 0);
        }

        var targetTicks = TargetTicks;
        IsActive = false;
        return new SeekPreviewDecision(SeekPreviewDecisionKind.Commit, targetTicks);
    }

    public SeekPreviewDecision Cancel()
    {
        if (!IsActive)
        {
            return new SeekPreviewDecision(SeekPreviewDecisionKind.None, 0);
        }

        var originalTicks = OriginalTicks;
        IsActive = false;
        return new SeekPreviewDecision(SeekPreviewDecisionKind.Cancel, originalTicks);
    }

    public SeekPreviewDecision DecideTimeout(TimeSpan now)
    {
        if (!IsActive || now < AutoCommitAt)
        {
            return new SeekPreviewDecision(SeekPreviewDecisionKind.None, TargetTicks);
        }

        var driftTicks = Math.Abs(TargetTicks - OriginalTicks);
        return driftTicks < _commitThresholdTicks ? Cancel() : Confirm();
    }

    private void MoveByTicks(long offsetTicks, TimeSpan now)
    {
        TargetTicks = Math.Max(0, TargetTicks + offsetTicks);
        AutoCommitAt = now + _autoCommitDelay;
    }

    private static long ToPositionTicks(TimeSpan value)
    {
        return Convert.ToInt64(value.TotalMilliseconds);
    }
}
