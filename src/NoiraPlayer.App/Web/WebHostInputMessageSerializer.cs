using System;
using System.Globalization;
using NoiraPlayer.Core.Input;

namespace NoiraPlayer.App.Web
{
    internal static class WebHostInputMessageSerializer
    {
        public static string Serialize(InputEnvelope input)
        {
            return
                "{\"type\":\"host.input\",\"version\":1,\"sequence\":" +
                input.Sequence.ToString(CultureInfo.InvariantCulture) +
                ",\"command\":\"" + Command(input.Command) +
                "\",\"phase\":\"" + Phase(input.Phase) +
                "\",\"source\":\"" + Source(input.DeviceKind) +
                "\",\"timestamp\":" +
                input.TimestampMilliseconds.ToString(CultureInfo.InvariantCulture) + "}";
        }

        private static string Command(InputCommand command) => command switch
        {
            InputCommand.MoveUp => "moveUp",
            InputCommand.MoveDown => "moveDown",
            InputCommand.MoveLeft => "moveLeft",
            InputCommand.MoveRight => "moveRight",
            InputCommand.Accept => "accept",
            InputCommand.Back => "back",
            InputCommand.Menu => "menu",
            InputCommand.View => "view",
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        private static string Phase(InputPhase phase) => phase switch
        {
            InputPhase.Pressed => "pressed",
            InputPhase.Released => "released",
            InputPhase.Repeated => "repeated",
            _ => throw new ArgumentOutOfRangeException(nameof(phase)),
        };

        private static string Source(InputDeviceKind source) => source switch
        {
            InputDeviceKind.Gamepad => "gamepad",
            InputDeviceKind.Remote => "remote",
            InputDeviceKind.Keyboard => "keyboard",
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };
    }
}
