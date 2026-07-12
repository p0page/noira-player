using System;
using System.Collections.Generic;
using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class GamepadInputPumpTests
{
    [Fact]
    public void Poll_Is_Inert_Until_Started_And_Stop_Is_Idempotent()
    {
        var emitted = new List<InputEnvelope>();
        var pump = CreatePump(_ => InputButtonState.MoveRight, emitted.Add);

        pump.Poll(["one"]);
        pump.Start();
        pump.Poll(["one"]);
        pump.Stop();
        pump.Stop();

        Assert.Collection(
            emitted,
            input => Assert.Equal(InputPhase.Pressed, input.Phase),
            input => Assert.Equal(InputPhase.Released, input.Phase));
    }

    [Fact]
    public void Device_Read_Failure_Does_Not_Block_Another_Controller()
    {
        var emitted = new List<InputEnvelope>();
        var failures = new List<GamepadInputFailure<string>>();
        var pump = new GamepadInputPump<string>(
            device => device == "broken"
                ? throw new InvalidOperationException("read")
                : InputButtonState.Accept,
            () => 10,
            emitted.Add,
            failures.Add);

        pump.Start();
        pump.Poll(["broken", "healthy"]);

        Assert.Equal(InputCommand.Accept, Assert.Single(emitted).Command);
        var failure = Assert.Single(failures);
        Assert.Equal(GamepadInputFailureStage.DeviceReading, failure.Stage);
        Assert.Equal("broken", failure.Device);
    }

    [Fact]
    public void State_Transition_Failure_Resets_The_State_Machine()
    {
        var stateMachine = new ThrowingStateMachine();
        var failures = new List<GamepadInputFailure<string>>();
        var pump = new GamepadInputPump<string>(
            _ => InputButtonState.Accept,
            () => 20,
            _ => { },
            failures.Add,
            stateMachine);

        pump.Start();
        pump.Poll(["one"]);

        Assert.Equal(1, stateMachine.ResetCount);
        Assert.Equal(GamepadInputFailureStage.StateTransition, Assert.Single(failures).Stage);
    }

    [Fact]
    public void Consumer_Failure_Does_Not_Reset_Or_Stop_Device_State()
    {
        var failures = new List<GamepadInputFailure<string>>();
        var shouldThrow = true;
        var pump = new GamepadInputPump<string>(
            _ => InputButtonState.Accept,
            () => 30,
            _ =>
            {
                if (shouldThrow)
                {
                    shouldThrow = false;
                    throw new InvalidOperationException("sink");
                }
            },
            failures.Add);

        pump.Start();
        pump.Poll(["one"]);
        pump.Poll([]);

        Assert.Equal(GamepadInputFailureStage.Consumer, Assert.Single(failures).Stage);
    }

    [Fact]
    public void Active_Device_Read_Failure_Does_Not_Replay_A_Held_Button_On_Recovery()
    {
        var emitted = new List<InputEnvelope>();
        var reading = InputButtonState.Accept;
        var readFails = false;
        var pump = new GamepadInputPump<string>(
            _ => readFails ? throw new InvalidOperationException("read") : reading,
            () => 40,
            emitted.Add);

        pump.Start();
        pump.Poll(["one"]);
        readFails = true;
        pump.Poll(["one"]);
        readFails = false;
        pump.Poll(["one"]);
        reading = InputButtonState.None;
        pump.Poll(["one"]);

        Assert.Collection(
            emitted,
            input => Assert.Equal(InputPhase.Pressed, input.Phase),
            input => Assert.Equal(InputPhase.Released, input.Phase));
    }

    [Fact]
    public void Active_Device_Read_Failure_Freezes_Directional_Repeat_Time()
    {
        var emitted = new List<InputEnvelope>();
        var now = 0L;
        var readFails = false;
        var pump = new GamepadInputPump<string>(
            _ => readFails
                ? throw new InvalidOperationException("read")
                : InputButtonState.MoveRight,
            () => now,
            emitted.Add);

        pump.Start();
        pump.Poll(["one"]);
        readFails = true;
        now = 500;
        pump.Poll(["one"]);
        now = 1000;
        pump.Poll(["one"]);
        readFails = false;
        pump.Poll(["one"]);
        now = 1399;
        pump.Poll(["one"]);

        Assert.Single(emitted);
        now = 1400;
        pump.Poll(["one"]);
        Assert.Equal(InputPhase.Repeated, emitted[1].Phase);
    }

    private static GamepadInputPump<string> CreatePump(
        Func<string, InputButtonState> read,
        Action<InputEnvelope> sink) => new(read, () => 0, sink);

    private sealed class ThrowingStateMachine : IGamepadInputStateMachine<string>
    {
        public bool HasActiveDevice => false;

        public string? ActiveDevice => null;

        public int ResetCount { get; private set; }

        public IReadOnlyList<InputEnvelope> Update(
            IReadOnlyList<GamepadDeviceSnapshot<string>> snapshots,
            long timestampMilliseconds) => throw new InvalidOperationException("transition");

        public IReadOnlyList<InputEnvelope> Reset(long timestampMilliseconds)
        {
            ResetCount++;
            return [];
        }
    }
}
