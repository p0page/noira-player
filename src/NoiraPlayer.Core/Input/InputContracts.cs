using System;

namespace NoiraPlayer.Core.Input;

public enum InputCommand
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Accept,
    Back,
    Menu,
    View,
}

public enum InputPhase
{
    Pressed,
    Released,
    Repeated,
}

public enum InputDeviceKind
{
    Gamepad,
    Remote,
    Keyboard,
}

public enum InputContext
{
    None,
    BrowseWeb,
    NativePlayback,
}

public enum InputControlKind
{
    Unknown,
    Button,
    DPad,
    LeftThumbstick,
}

public readonly record struct InputEnvelope(
    long Sequence,
    InputCommand Command,
    InputPhase Phase,
    InputDeviceKind DeviceKind,
    long TimestampMilliseconds,
    InputControlKind ControlKind = InputControlKind.Unknown);

public readonly record struct InputTransition(
    InputCommand Command,
    InputPhase Phase,
    InputControlKind ControlKind);

[Flags]
public enum InputButtonState
{
    None = 0,
    MoveUp = 1 << 0,
    MoveDown = 1 << 1,
    MoveLeft = 1 << 2,
    MoveRight = 1 << 3,
    Accept = 1 << 4,
    Back = 1 << 5,
    Menu = 1 << 6,
    View = 1 << 7,
    ThumbstickUp = 1 << 8,
    ThumbstickDown = 1 << 9,
    ThumbstickLeft = 1 << 10,
    ThumbstickRight = 1 << 11,
}
