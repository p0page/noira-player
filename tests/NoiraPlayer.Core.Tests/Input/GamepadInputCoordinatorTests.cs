using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class GamepadInputCoordinatorTests
{
    [Fact]
    public void First_Meaningful_Edge_Claims_The_Active_Controller()
    {
        var coordinator = new GamepadInputCoordinator<string>();

        var events = coordinator.Update(
            [
                new GamepadDeviceSnapshot<string>("one", InputButtonState.None),
                new GamepadDeviceSnapshot<string>("two", InputButtonState.Accept),
            ],
            10);

        Assert.Equal("two", coordinator.ActiveDevice);
        Assert.Equal(InputCommand.Accept, Assert.Single(events).Command);
        Assert.Equal(InputControlKind.Button, events[0].ControlKind);
        Assert.Equal(1, events[0].Sequence);
    }

    [Fact]
    public void Another_Controller_Cannot_Take_Over_Until_Active_Is_Neutral()
    {
        var coordinator = new GamepadInputCoordinator<string>();
        coordinator.Update(
            [new GamepadDeviceSnapshot<string>("one", InputButtonState.MoveRight)],
            0);

        Assert.Empty(coordinator.Update(
            [
                new GamepadDeviceSnapshot<string>("one", InputButtonState.MoveRight),
                new GamepadDeviceSnapshot<string>("two", InputButtonState.Accept),
            ],
            20));
        Assert.Equal("one", coordinator.ActiveDevice);

        coordinator.Update(
            [
                new GamepadDeviceSnapshot<string>("one", InputButtonState.None),
                new GamepadDeviceSnapshot<string>("two", InputButtonState.None),
            ],
            40);
        var takeover = coordinator.Update(
            [
                new GamepadDeviceSnapshot<string>("one", InputButtonState.None),
                new GamepadDeviceSnapshot<string>("two", InputButtonState.Accept),
            ],
            60);

        Assert.Equal("two", coordinator.ActiveDevice);
        Assert.Equal(InputCommand.Accept, Assert.Single(takeover).Command);
    }

    [Fact]
    public void Disconnect_Releases_Held_Commands_And_Clears_Active_Device()
    {
        var coordinator = new GamepadInputCoordinator<string>();
        coordinator.Update(
            [new GamepadDeviceSnapshot<string>("one", InputButtonState.MoveDown)],
            0);

        var released = coordinator.Update([], 10);

        Assert.Null(coordinator.ActiveDevice);
        Assert.Equal(InputPhase.Released, Assert.Single(released).Phase);
        Assert.Equal(2, released[0].Sequence);
    }

    [Fact]
    public void Reset_Releases_State_Without_Resetting_Sequence()
    {
        var coordinator = new GamepadInputCoordinator<string>();
        coordinator.Update(
            [new GamepadDeviceSnapshot<string>("one", InputButtonState.Accept)],
            0);

        var released = coordinator.Reset(10);
        coordinator.Update(
            [new GamepadDeviceSnapshot<string>("one", InputButtonState.None)],
            15);
        var next = coordinator.Update(
            [new GamepadDeviceSnapshot<string>("one", InputButtonState.Menu)],
            20);

        Assert.Equal(2, Assert.Single(released).Sequence);
        Assert.Equal(3, Assert.Single(next).Sequence);
    }

    [Fact]
    public void Reset_Requires_Neutral_Before_A_Held_Button_Can_Be_Claimed_Again()
    {
        var coordinator = new GamepadInputCoordinator<string>();
        coordinator.Update(
            [new GamepadDeviceSnapshot<string>("one", InputButtonState.Accept)],
            0);

        coordinator.Reset(10);

        Assert.Empty(coordinator.Update(
            [new GamepadDeviceSnapshot<string>("one", InputButtonState.Accept)],
            20));
        Assert.Empty(coordinator.Update(
            [new GamepadDeviceSnapshot<string>("one", InputButtonState.None)],
            30));
        var nextPress = coordinator.Update(
            [new GamepadDeviceSnapshot<string>("one", InputButtonState.Accept)],
            40);

        Assert.Equal(InputCommand.Accept, Assert.Single(nextPress).Command);
    }

    [Fact]
    public void Active_Controller_Wins_When_Two_Controllers_Press_After_Neutral()
    {
        var coordinator = new GamepadInputCoordinator<string>();
        coordinator.Update(
            [new GamepadDeviceSnapshot<string>("one", InputButtonState.Accept)],
            0);
        coordinator.Update(
            [
                new GamepadDeviceSnapshot<string>("one", InputButtonState.None),
                new GamepadDeviceSnapshot<string>("two", InputButtonState.None),
            ],
            10);

        var next = coordinator.Update(
            [
                new GamepadDeviceSnapshot<string>("one", InputButtonState.Menu),
                new GamepadDeviceSnapshot<string>("two", InputButtonState.Back),
            ],
            20);

        Assert.Equal("one", coordinator.ActiveDevice);
        Assert.Equal(InputCommand.Menu, Assert.Single(next).Command);
    }
}
