using System;
using System.Collections.Generic;

namespace NoiraPlayer.Core.Input;

public enum GamepadInputFailureStage
{
    DeviceReading,
    StateTransition,
    Consumer,
}

public readonly record struct GamepadInputFailure<TDevice>(
    GamepadInputFailureStage Stage,
    TDevice? Device,
    Exception Error)
    where TDevice : notnull;

public sealed class GamepadInputPump<TDevice>
    where TDevice : notnull
{
    private readonly Action<GamepadInputFailure<TDevice>>? _failureSink;
    private readonly Func<long> _getTimestampMilliseconds;
    private readonly Func<TDevice, InputButtonState> _readDevice;
    private readonly Action<InputEnvelope> _sink;
    private readonly IGamepadInputStateMachine<TDevice> _stateMachine;
    private readonly HashSet<TDevice> _failedDevices = [];
    private readonly Dictionary<TDevice, InputButtonState> _lastSuccessfulButtons = [];
    private long? _activeReadFailureBaselineRawTimestamp;
    private long _clockOffsetMilliseconds;
    private long? _lastStateUpdateRawTimestamp;
    private bool _started;

    public GamepadInputPump(
        Func<TDevice, InputButtonState> readDevice,
        Func<long> getTimestampMilliseconds,
        Action<InputEnvelope> sink,
        Action<GamepadInputFailure<TDevice>>? failureSink = null,
        IGamepadInputStateMachine<TDevice>? stateMachine = null)
    {
        _readDevice = readDevice ?? throw new ArgumentNullException(nameof(readDevice));
        _getTimestampMilliseconds = getTimestampMilliseconds ??
            throw new ArgumentNullException(nameof(getTimestampMilliseconds));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _failureSink = failureSink;
        _stateMachine = stateMachine ?? new GamepadInputCoordinator<TDevice>();
    }

    public TDevice? ActiveDevice => _stateMachine.ActiveDevice;

    public void Start()
    {
        _activeReadFailureBaselineRawTimestamp = null;
        _clockOffsetMilliseconds = 0;
        _lastStateUpdateRawTimestamp = null;
        _started = true;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        Reset();
        _failedDevices.Clear();
        _lastSuccessfulButtons.Clear();
    }

    public void Poll(IReadOnlyList<TDevice> devices)
    {
        if (!_started)
        {
            return;
        }

        var rawTimestamp = _getTimestampMilliseconds();
        var snapshots = new List<GamepadDeviceSnapshot<TDevice>>(devices.Count);
        var failedThisPoll = new HashSet<TDevice>();
        var presentDevices = new HashSet<TDevice>();
        foreach (var device in devices)
        {
            presentDevices.Add(device);
            try
            {
                var buttons = _readDevice(device);
                _lastSuccessfulButtons[device] = buttons;
                snapshots.Add(new GamepadDeviceSnapshot<TDevice>(device, buttons));
                _failedDevices.Remove(device);
            }
            catch (Exception error)
            {
                failedThisPoll.Add(device);
                if (_lastSuccessfulButtons.TryGetValue(device, out var lastButtons))
                {
                    snapshots.Add(new GamepadDeviceSnapshot<TDevice>(device, lastButtons));
                }
                if (_failedDevices.Add(device))
                {
                    ReportFailure(new GamepadInputFailure<TDevice>(
                        GamepadInputFailureStage.DeviceReading,
                        device,
                        error));
                }
            }
        }

        foreach (var knownDevice in new List<TDevice>(_lastSuccessfulButtons.Keys))
        {
            if (!presentDevices.Contains(knownDevice))
            {
                _lastSuccessfulButtons.Remove(knownDevice);
                _failedDevices.Remove(knownDevice);
            }
        }

        if (
            _stateMachine.HasActiveDevice &&
            _stateMachine.ActiveDevice is { } activeDevice &&
            failedThisPoll.Contains(activeDevice))
        {
            _activeReadFailureBaselineRawTimestamp ??=
                _lastStateUpdateRawTimestamp ?? rawTimestamp;
            return;
        }

        if (_activeReadFailureBaselineRawTimestamp.HasValue)
        {
            _clockOffsetMilliseconds += Math.Max(
                0,
                rawTimestamp - _activeReadFailureBaselineRawTimestamp.Value);
            _activeReadFailureBaselineRawTimestamp = null;
        }

        var timestamp = rawTimestamp - _clockOffsetMilliseconds;

        try
        {
            Publish(_stateMachine.Update(snapshots, timestamp));
            _lastStateUpdateRawTimestamp = rawTimestamp;
        }
        catch (Exception error)
        {
            ReportFailure(new GamepadInputFailure<TDevice>(
                GamepadInputFailureStage.StateTransition,
                default,
                error));
            ResetStateMachine(timestamp);
        }
    }

    public void Reset()
    {
        var rawTimestamp = _getTimestampMilliseconds();
        ResetStateMachine(rawTimestamp - _clockOffsetMilliseconds);
        _activeReadFailureBaselineRawTimestamp = null;
        _lastStateUpdateRawTimestamp = rawTimestamp;
    }

    private void ResetStateMachine(long timestamp)
    {
        try
        {
            Publish(_stateMachine.Reset(timestamp));
        }
        catch
        {
            // Reset is the final containment boundary and must not recurse.
        }
    }

    private void Publish(IReadOnlyList<InputEnvelope> inputs)
    {
        foreach (var input in inputs)
        {
            try
            {
                _sink(input);
            }
            catch (Exception error)
            {
                ReportFailure(new GamepadInputFailure<TDevice>(
                    GamepadInputFailureStage.Consumer,
                    default,
                    error));
            }
        }
    }

    private void ReportFailure(GamepadInputFailure<TDevice> failure)
    {
        try
        {
            _failureSink?.Invoke(failure);
        }
        catch
        {
            // Diagnostics cannot be allowed to escape an input boundary.
        }
    }
}
