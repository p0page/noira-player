using System.Text.Json;

namespace NoiraPlayer.App.Web
{
    internal enum WebHostControlCommand
    {
        None,
        Ready,
        NativeBack,
    }

    internal readonly record struct WebHostControlMessage(
        WebHostControlCommand Command,
        int InputVersion)
    {
        public static bool TryParse(string json, out WebHostControlMessage message)
        {
            message = default;
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (!root.TryGetProperty("type", out var type) ||
                    type.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                switch (type.GetString())
                {
                    case "host.ready":
                        var version = root.TryGetProperty("inputVersion", out var inputVersion) &&
                            inputVersion.TryGetInt32(out var parsedVersion)
                                ? parsedVersion
                                : 0;
                        message = new WebHostControlMessage(
                            WebHostControlCommand.Ready,
                            version);
                        return true;

                    case "host.nativeBack":
                        message = new WebHostControlMessage(
                            WebHostControlCommand.NativeBack,
                            0);
                        return true;

                    default:
                        return false;
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
