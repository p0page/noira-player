using System.Collections.Generic;
using System.Linq;

namespace NoiraPlayer.Core.Input;

public sealed class InputRepeatState
{
    public const long InitialRepeatDelayMilliseconds = 400;
    public const long RepeatIntervalMilliseconds = 120;

    private static readonly InputCommand[] OrderedCommands =
    [
        InputCommand.MoveUp,
        InputCommand.MoveDown,
        InputCommand.MoveLeft,
        InputCommand.MoveRight,
        InputCommand.Accept,
        InputCommand.Back,
        InputCommand.Menu,
        InputCommand.View,
    ];

    private readonly HashSet<InputCommand> _held = [];
    private readonly Dictionary<InputCommand, InputControlKind> _heldControls = [];
    private readonly Dictionary<InputCommand, long> _nextRepeatAt = [];

    public IReadOnlyList<InputTransition> Update(
        InputButtonState buttons,
        long timestampMilliseconds)
    {
        var transitions = new List<InputTransition>();
        foreach (var command in OrderedCommands)
        {
            var pressed = TryGetPressedControl(command, buttons, out var pressedControl);
            var held = _held.Contains(command);
            if (pressed && !held)
            {
                _held.Add(command);
                _heldControls[command] = pressedControl;
                transitions.Add(new InputTransition(
                    command,
                    InputPhase.Pressed,
                    pressedControl));
                if (CanRepeat(command))
                {
                    _nextRepeatAt[command] =
                        timestampMilliseconds + InitialRepeatDelayMilliseconds;
                }
            }
            else if (
                pressed &&
                held &&
                _heldControls[command] != pressedControl)
            {
                transitions.Add(new InputTransition(
                    command,
                    InputPhase.Released,
                    _heldControls[command]));
                _heldControls[command] = pressedControl;
                transitions.Add(new InputTransition(
                    command,
                    InputPhase.Pressed,
                    pressedControl));
                if (CanRepeat(command))
                {
                    _nextRepeatAt[command] =
                        timestampMilliseconds + InitialRepeatDelayMilliseconds;
                }
            }
            else if (
                pressed &&
                CanRepeat(command) &&
                _nextRepeatAt.TryGetValue(command, out var nextRepeatAt) &&
                timestampMilliseconds >= nextRepeatAt)
            {
                transitions.Add(new InputTransition(
                    command,
                    InputPhase.Repeated,
                    _heldControls[command]));
                _nextRepeatAt[command] =
                    timestampMilliseconds + RepeatIntervalMilliseconds;
            }
            else if (!pressed && held)
            {
                _held.Remove(command);
                var releasedControl = _heldControls[command];
                _heldControls.Remove(command);
                _nextRepeatAt.Remove(command);
                transitions.Add(new InputTransition(
                    command,
                    InputPhase.Released,
                    releasedControl));
            }
        }

        return transitions;
    }

    public IReadOnlyList<InputTransition> Reset()
    {
        if (_held.Count == 0)
        {
            return [];
        }

        var transitions = OrderedCommands
            .Where(_held.Contains)
            .Select(command => new InputTransition(
                command,
                InputPhase.Released,
                _heldControls[command]))
            .ToArray();
        _held.Clear();
        _heldControls.Clear();
        _nextRepeatAt.Clear();
        return transitions;
    }

    private static bool CanRepeat(InputCommand command) =>
        command is InputCommand.MoveUp or
            InputCommand.MoveDown or
            InputCommand.MoveLeft or
            InputCommand.MoveRight;

    private static bool TryGetPressedControl(
        InputCommand command,
        InputButtonState buttons,
        out InputControlKind control)
    {
        var dpadButton = command switch
        {
            InputCommand.MoveUp => InputButtonState.MoveUp,
            InputCommand.MoveDown => InputButtonState.MoveDown,
            InputCommand.MoveLeft => InputButtonState.MoveLeft,
            InputCommand.MoveRight => InputButtonState.MoveRight,
            _ => InputButtonState.None,
        };
        if (dpadButton != InputButtonState.None && buttons.HasFlag(dpadButton))
        {
            control = InputControlKind.DPad;
            return true;
        }

        var thumbstickButton = command switch
        {
            InputCommand.MoveUp => InputButtonState.ThumbstickUp,
            InputCommand.MoveDown => InputButtonState.ThumbstickDown,
            InputCommand.MoveLeft => InputButtonState.ThumbstickLeft,
            InputCommand.MoveRight => InputButtonState.ThumbstickRight,
            _ => InputButtonState.None,
        };
        if (thumbstickButton != InputButtonState.None && buttons.HasFlag(thumbstickButton))
        {
            control = InputControlKind.LeftThumbstick;
            return true;
        }

        var actionButton = command switch
        {
            InputCommand.Accept => InputButtonState.Accept,
            InputCommand.Back => InputButtonState.Back,
            InputCommand.Menu => InputButtonState.Menu,
            InputCommand.View => InputButtonState.View,
            _ => InputButtonState.None,
        };
        control = InputControlKind.Button;
        return actionButton != InputButtonState.None && buttons.HasFlag(actionButton);
    }
}
