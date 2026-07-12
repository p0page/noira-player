using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class InputRepeatStateTests
{
    [Fact]
    public void Direction_Produces_Pressed_Repeated_And_Released()
    {
        var state = new InputRepeatState();

        Assert.Equal(
            [new InputTransition(InputCommand.MoveLeft, InputPhase.Pressed, InputControlKind.DPad)],
            state.Update(InputButtonState.MoveLeft, 0));
        Assert.Empty(state.Update(InputButtonState.MoveLeft, 399));
        Assert.Equal(
            [new InputTransition(InputCommand.MoveLeft, InputPhase.Repeated, InputControlKind.DPad)],
            state.Update(InputButtonState.MoveLeft, 400));
        Assert.Empty(state.Update(InputButtonState.MoveLeft, 519));
        Assert.Equal(
            [new InputTransition(InputCommand.MoveLeft, InputPhase.Repeated, InputControlKind.DPad)],
            state.Update(InputButtonState.MoveLeft, 520));
        Assert.Equal(
            [new InputTransition(InputCommand.MoveLeft, InputPhase.Released, InputControlKind.DPad)],
            state.Update(InputButtonState.None, 521));
    }

    [Fact]
    public void Non_Directional_Commands_Do_Not_Repeat()
    {
        var state = new InputRepeatState();
        var buttons = InputButtonState.Accept |
            InputButtonState.Back |
            InputButtonState.Menu |
            InputButtonState.View;

        Assert.Equal(4, state.Update(buttons, 0).Count);
        Assert.Empty(state.Update(buttons, 2000));
        Assert.All(state.Update(InputButtonState.None, 2001), transition =>
            Assert.Equal(InputPhase.Released, transition.Phase));
    }

    [Fact]
    public void Reset_Releases_All_Held_Commands()
    {
        var state = new InputRepeatState();
        state.Update(InputButtonState.MoveUp | InputButtonState.Accept, 0);

        Assert.Equal(
            [
                new InputTransition(InputCommand.MoveUp, InputPhase.Released, InputControlKind.DPad),
                new InputTransition(InputCommand.Accept, InputPhase.Released, InputControlKind.Button),
            ],
            state.Reset());
        Assert.Empty(state.Reset());
    }

    [Fact]
    public void DPad_Takes_Over_The_Same_Direction_From_The_Thumbstick()
    {
        var state = new InputRepeatState();
        state.Update(InputButtonState.ThumbstickRight, 0);

        var transitions = state.Update(InputButtonState.MoveRight, 10);

        Assert.Equal(
            [
                new InputTransition(InputCommand.MoveRight, InputPhase.Released, InputControlKind.LeftThumbstick),
                new InputTransition(InputCommand.MoveRight, InputPhase.Pressed, InputControlKind.DPad),
            ],
            transitions);
        Assert.Empty(state.Update(InputButtonState.MoveRight, 409));
        Assert.Equal(InputPhase.Repeated, Assert.Single(state.Update(InputButtonState.MoveRight, 410)).Phase);
    }
}
