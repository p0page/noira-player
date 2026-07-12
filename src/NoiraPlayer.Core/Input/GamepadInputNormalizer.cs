using System;

namespace NoiraPlayer.Core.Input;

public readonly record struct GamepadPhysicalState(
    bool DPadUp,
    bool DPadDown,
    bool DPadLeft,
    bool DPadRight,
    bool Accept,
    bool Back,
    bool Menu,
    bool View,
    double LeftThumbstickX,
    double LeftThumbstickY);

public static class GamepadInputNormalizer
{
    public const double ThumbstickThreshold = 0.55;

    public static InputButtonState Normalize(GamepadPhysicalState state)
    {
        var result = InputButtonState.None;
        if (state.Accept)
        {
            result |= InputButtonState.Accept;
        }

        if (state.Back)
        {
            result |= InputButtonState.Back;
        }

        if (state.Menu)
        {
            result |= InputButtonState.Menu;
        }

        if (state.View)
        {
            result |= InputButtonState.View;
        }

        if (state.DPadUp || state.DPadDown || state.DPadLeft || state.DPadRight)
        {
            if (state.DPadUp)
            {
                result |= InputButtonState.MoveUp;
            }
            if (state.DPadDown)
            {
                result |= InputButtonState.MoveDown;
            }
            if (state.DPadLeft)
            {
                result |= InputButtonState.MoveLeft;
            }
            if (state.DPadRight)
            {
                result |= InputButtonState.MoveRight;
            }
            return result;
        }

        var horizontal = Math.Abs(state.LeftThumbstickX);
        var vertical = Math.Abs(state.LeftThumbstickY);
        if (horizontal < ThumbstickThreshold && vertical < ThumbstickThreshold)
        {
            return result;
        }

        if (horizontal >= vertical)
        {
            result |= state.LeftThumbstickX < 0
                ? InputButtonState.ThumbstickLeft
                : InputButtonState.ThumbstickRight;
        }
        else
        {
            result |= state.LeftThumbstickY < 0
                ? InputButtonState.ThumbstickDown
                : InputButtonState.ThumbstickUp;
        }

        return result;
    }
}

public sealed class GamepadInputNormalizerState
{
    public const double ThumbstickReleaseThreshold = 0.45;

    private const InputButtonState DirectionMask =
        InputButtonState.MoveUp |
        InputButtonState.MoveDown |
        InputButtonState.MoveLeft |
        InputButtonState.MoveRight;
    private const InputButtonState ThumbstickMask =
        InputButtonState.ThumbstickUp |
        InputButtonState.ThumbstickDown |
        InputButtonState.ThumbstickLeft |
        InputButtonState.ThumbstickRight;
    private InputButtonState _thumbstickDirection;

    public InputButtonState Normalize(GamepadPhysicalState state)
    {
        var normalized = GamepadInputNormalizer.Normalize(state);
        if ((normalized & DirectionMask) != InputButtonState.None)
        {
            return normalized;
        }

        var actions = normalized & ~ThumbstickMask;
        if (
            _thumbstickDirection != InputButtonState.None &&
            IsDirectionStillHeld(_thumbstickDirection, state))
        {
            return actions | _thumbstickDirection;
        }

        _thumbstickDirection = normalized & ThumbstickMask;
        return actions | _thumbstickDirection;
    }

    private static bool IsDirectionStillHeld(
        InputButtonState direction,
        GamepadPhysicalState state) => direction switch
        {
            InputButtonState.ThumbstickLeft =>
                state.LeftThumbstickX <= -ThumbstickReleaseThreshold,
            InputButtonState.ThumbstickRight =>
                state.LeftThumbstickX >= ThumbstickReleaseThreshold,
            InputButtonState.ThumbstickUp =>
                state.LeftThumbstickY >= ThumbstickReleaseThreshold,
            InputButtonState.ThumbstickDown =>
                state.LeftThumbstickY <= -ThumbstickReleaseThreshold,
            _ => false,
        };
}
