using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class GamepadInputNormalizerTests
{
    [Fact]
    public void Dpad_Wins_Over_Thumbstick_And_Preserves_Action_Buttons()
    {
        var normalized = GamepadInputNormalizer.Normalize(new GamepadPhysicalState(
            DPadUp: true,
            DPadDown: false,
            DPadLeft: false,
            DPadRight: false,
            Accept: true,
            Back: false,
            Menu: true,
            View: false,
            LeftThumbstickX: 1,
            LeftThumbstickY: -1));

        Assert.Equal(
            InputButtonState.MoveUp | InputButtonState.Accept | InputButtonState.Menu,
            normalized);
    }

    [Theory]
    [InlineData(0.54, 0.54, InputButtonState.None)]
    [InlineData(-0.80, 0.60, InputButtonState.ThumbstickLeft)]
    [InlineData(0.60, 0.80, InputButtonState.ThumbstickUp)]
    [InlineData(0.60, -0.80, InputButtonState.ThumbstickDown)]
    public void Thumbstick_Uses_Deadzone_And_Dominant_Axis(
        double x,
        double y,
        InputButtonState expected)
    {
        var normalized = GamepadInputNormalizer.Normalize(new GamepadPhysicalState(
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            x,
            y));

        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Stateful_Normalizer_Uses_Hysteresis_And_Locks_The_Active_Axis()
    {
        var normalizer = new GamepadInputNormalizerState();

        Assert.Equal(
            InputButtonState.ThumbstickRight,
            normalizer.Normalize(Physical(x: 0.56, y: 0)));
        Assert.Equal(
            InputButtonState.ThumbstickRight,
            normalizer.Normalize(Physical(x: 0.53, y: 0.54)));
        Assert.Equal(
            InputButtonState.None,
            normalizer.Normalize(Physical(x: 0.44, y: 0.54)));
        Assert.Equal(
            InputButtonState.ThumbstickUp,
            normalizer.Normalize(Physical(x: 0.57, y: 0.58)));
        Assert.Equal(
            InputButtonState.ThumbstickUp,
            normalizer.Normalize(Physical(x: 0.59, y: 0.57)));
    }

    private static GamepadPhysicalState Physical(double x, double y) => new(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        x,
        y);
}
