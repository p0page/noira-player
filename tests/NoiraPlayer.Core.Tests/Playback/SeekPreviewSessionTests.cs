using System;
using NoiraPlayer.Core.Playback;
using Xunit;

namespace NoiraPlayer.Core.Tests.Playback;

public sealed class SeekPreviewSessionTests
{
    [Fact]
    public void Begin_Ignores_Thumbstick_Input_Below_DeadZone()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);

        var changed = session.BeginFromThumbstick(TimeSpan.FromSeconds(1).Ticks, 0.2, TimeSpan.FromSeconds(10));

        Assert.False(changed);
        Assert.False(session.IsActive);
        Assert.Equal(0, session.TargetTicks);
    }

    [Fact]
    public void Begin_Starts_Thumbstick_Input_At_DeadZone_Boundary()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);

        var changed = session.BeginFromThumbstick(TimeSpan.FromSeconds(1).Ticks, 0.55, TimeSpan.FromSeconds(10));

        Assert.True(changed);
        Assert.True(session.IsActive);
        Assert.Equal(TimeSpan.FromSeconds(6).Ticks, session.TargetTicks);
    }

    [Fact]
    public void Begin_Ignores_Thumbstick_NaN()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);

        var changed = session.BeginFromThumbstick(TimeSpan.FromSeconds(1).Ticks, double.NaN, TimeSpan.FromSeconds(10));

        Assert.False(changed);
        Assert.False(session.IsActive);
        Assert.Equal(0, session.TargetTicks);
    }

    [Fact]
    public void Constructor_Rejects_Thumbstick_DeadZone_NaN()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), double.NaN));
    }

    [Fact]
    public void Move_Updates_Target_And_Reset_Deadline()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);

        Assert.True(session.BeginFromThumbstick(TimeSpan.FromSeconds(1).Ticks, 0.9, TimeSpan.FromSeconds(10)));
        Assert.Equal(TimeSpan.FromSeconds(1).Ticks, session.OriginalTicks);
        Assert.Equal(TimeSpan.FromSeconds(6).Ticks, session.TargetTicks);
        Assert.Equal(TimeSpan.FromSeconds(11.8), session.AutoCommitAt);

        session.MoveBy(TimeSpan.FromSeconds(-3), TimeSpan.FromSeconds(11));

        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, session.TargetTicks);
        Assert.Equal(TimeSpan.FromSeconds(12.8), session.AutoCommitAt);
    }

    [Fact]
    public void Move_Clamps_Target_To_Zero()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        session.Begin(TimeSpan.FromSeconds(-1).Ticks, TimeSpan.Zero);

        session.MoveBy(TimeSpan.FromSeconds(-30), TimeSpan.FromSeconds(1));

        Assert.Equal(0, session.OriginalTicks);
        Assert.Equal(0, session.TargetTicks);
    }

    [Fact]
    public void MoveTo_Updates_Target_To_Absolute_Position_And_Reset_Deadline()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        session.Begin(TimeSpan.FromSeconds(10).Ticks, TimeSpan.Zero);

        session.MoveTo(TimeSpan.FromMinutes(42).Ticks, TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromMinutes(42).Ticks, session.TargetTicks);
        Assert.Equal(TimeSpan.FromSeconds(3.8), session.AutoCommitAt);
    }

    [Fact]
    public void Timeout_Before_Deadline_Returns_None_And_Keeps_Pending()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        session.Begin(TimeSpan.FromSeconds(10).Ticks, TimeSpan.Zero);
        session.MoveBy(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));

        var decision = session.DecideTimeout(TimeSpan.FromSeconds(2.7));

        Assert.Equal(SeekPreviewDecisionKind.None, decision.Kind);
        Assert.Equal(TimeSpan.FromSeconds(40).Ticks, decision.PositionTicks);
        Assert.True(session.IsActive);
    }

    [Fact]
    public void AutoCommit_Cancels_Tiny_Drift_Without_Explicit_A()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        session.Begin(TimeSpan.FromSeconds(10).Ticks, TimeSpan.Zero);
        session.MoveBy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));

        var decision = session.DecideTimeout(TimeSpan.FromSeconds(2.8));

        Assert.Equal(SeekPreviewDecisionKind.Cancel, decision.Kind);
        Assert.Equal(TimeSpan.FromSeconds(10).Ticks, decision.PositionTicks);
        Assert.False(session.IsActive);
    }

    [Fact]
    public void AutoCommit_Commits_Larger_Movement_After_Deadline()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        session.Begin(TimeSpan.FromSeconds(10).Ticks, TimeSpan.Zero);
        session.MoveBy(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));

        var decision = session.DecideTimeout(TimeSpan.FromSeconds(2.8));

        Assert.Equal(SeekPreviewDecisionKind.Commit, decision.Kind);
        Assert.Equal(TimeSpan.FromSeconds(40).Ticks, decision.PositionTicks);
        Assert.False(session.IsActive);
    }

    [Fact]
    public void Confirm_Commits_Target_Immediately()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        session.Begin(TimeSpan.FromSeconds(10).Ticks, TimeSpan.Zero);
        session.MoveBy(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));

        var decision = session.Confirm();

        Assert.Equal(SeekPreviewDecisionKind.Commit, decision.Kind);
        Assert.Equal(TimeSpan.FromSeconds(40).Ticks, decision.PositionTicks);
        Assert.False(session.IsActive);
    }

    [Fact]
    public void Confirm_Uses_TimeSpan_Ticks_For_Target_Position()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        session.Begin(TimeSpan.FromMinutes(1).Ticks, TimeSpan.Zero);
        session.MoveBy(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));

        var decision = session.Confirm();

        Assert.Equal(SeekPreviewDecisionKind.Commit, decision.Kind);
        Assert.Equal(TimeSpan.FromSeconds(90).Ticks, decision.PositionTicks);
    }

    [Fact]
    public void Cancel_Returns_Original_Position()
    {
        var session = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        session.Begin(TimeSpan.FromSeconds(10).Ticks, TimeSpan.Zero);
        session.MoveBy(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));

        var decision = session.Cancel();

        Assert.Equal(SeekPreviewDecisionKind.Cancel, decision.Kind);
        Assert.Equal(TimeSpan.FromSeconds(10).Ticks, decision.PositionTicks);
        Assert.False(session.IsActive);
    }
}
