using System.Collections.Generic;

namespace NoiraPlayer.Core.Input;

public readonly record struct GamepadDeviceSnapshot<TDevice>(
    TDevice Device,
    InputButtonState Buttons)
    where TDevice : notnull;

public interface IGamepadInputStateMachine<TDevice>
    where TDevice : notnull
{
    bool HasActiveDevice { get; }

    TDevice? ActiveDevice { get; }

    IReadOnlyList<InputEnvelope> Update(
        IReadOnlyList<GamepadDeviceSnapshot<TDevice>> snapshots,
        long timestampMilliseconds);

    IReadOnlyList<InputEnvelope> Reset(long timestampMilliseconds);
}

public sealed class GamepadInputCoordinator<TDevice> : IGamepadInputStateMachine<TDevice>
    where TDevice : notnull
{
    private readonly EqualityComparer<TDevice> _comparer = EqualityComparer<TDevice>.Default;
    private readonly Dictionary<TDevice, InputButtonState> _previousButtons = [];
    private readonly InputRepeatState _repeatState = new();
    private TDevice? _activeDevice;
    private bool _hasActiveDevice;
    private bool _activeWasNeutral;
    private bool _requiresNeutral;
    private long _sequence;

    public bool HasActiveDevice => _hasActiveDevice;

    public TDevice? ActiveDevice => _hasActiveDevice ? _activeDevice : default;

    public IReadOnlyList<InputEnvelope> Update(
        IReadOnlyList<GamepadDeviceSnapshot<TDevice>> snapshots,
        long timestampMilliseconds)
    {
        var envelopes = new List<InputEnvelope>();
        var current = new Dictionary<TDevice, InputButtonState>(_comparer);
        foreach (var snapshot in snapshots)
        {
            current[snapshot.Device] = snapshot.Buttons;
        }

        if (_requiresNeutral)
        {
            var allNeutral = true;
            foreach (var buttons in current.Values)
            {
                if (buttons != InputButtonState.None)
                {
                    allNeutral = false;
                    break;
                }
            }

            Remember(current);
            _requiresNeutral = !allNeutral;
            return envelopes;
        }

        if (_hasActiveDevice &&
            (_activeDevice is null || !current.ContainsKey(_activeDevice)))
        {
            Append(envelopes, _repeatState.Reset(), timestampMilliseconds);
            ClearActiveDevice();
        }

        if (!_hasActiveDevice)
        {
            TryClaimFirstEdge(snapshots);
        }
        else if (_activeWasNeutral)
        {
            TryClaimAnotherEdge(snapshots);
        }

        if (_hasActiveDevice && _activeDevice is not null)
        {
            var buttons = current[_activeDevice];
            Append(envelopes, _repeatState.Update(buttons, timestampMilliseconds), timestampMilliseconds);
            _activeWasNeutral = buttons == InputButtonState.None;
        }

        Remember(current);

        return envelopes;
    }

    public IReadOnlyList<InputEnvelope> Reset(long timestampMilliseconds)
    {
        var envelopes = new List<InputEnvelope>();
        Append(envelopes, _repeatState.Reset(), timestampMilliseconds);
        _previousButtons.Clear();
        ClearActiveDevice();
        _requiresNeutral = true;
        return envelopes;
    }

    private void TryClaimFirstEdge(IReadOnlyList<GamepadDeviceSnapshot<TDevice>> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (HasNewPress(snapshot.Device, snapshot.Buttons))
            {
                SetActiveDevice(snapshot.Device);
                return;
            }
        }
    }

    private void TryClaimAnotherEdge(IReadOnlyList<GamepadDeviceSnapshot<TDevice>> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (
                _activeDevice is not null &&
                _comparer.Equals(snapshot.Device, _activeDevice) &&
                HasNewPress(snapshot.Device, snapshot.Buttons))
            {
                return;
            }
        }

        foreach (var snapshot in snapshots)
        {
            if (
                _activeDevice is not null &&
                _comparer.Equals(snapshot.Device, _activeDevice))
            {
                continue;
            }

            if (HasNewPress(snapshot.Device, snapshot.Buttons))
            {
                _repeatState.Reset();
                SetActiveDevice(snapshot.Device);
                return;
            }
        }
    }

    private bool HasNewPress(TDevice device, InputButtonState buttons)
    {
        _previousButtons.TryGetValue(device, out var previous);
        return buttons != InputButtonState.None && (buttons & ~previous) != 0;
    }

    private void SetActiveDevice(TDevice device)
    {
        _activeDevice = device;
        _hasActiveDevice = true;
        _activeWasNeutral = false;
    }

    private void ClearActiveDevice()
    {
        _activeDevice = default;
        _hasActiveDevice = false;
        _activeWasNeutral = false;
    }

    private void Remember(IReadOnlyDictionary<TDevice, InputButtonState> current)
    {
        _previousButtons.Clear();
        foreach (var pair in current)
        {
            _previousButtons[pair.Key] = pair.Value;
        }
    }

    private void Append(
        ICollection<InputEnvelope> target,
        IReadOnlyList<InputTransition> transitions,
        long timestampMilliseconds)
    {
        foreach (var transition in transitions)
        {
            target.Add(new InputEnvelope(
                ++_sequence,
                transition.Command,
                transition.Phase,
                InputDeviceKind.Gamepad,
                timestampMilliseconds,
                transition.ControlKind));
        }
    }
}
