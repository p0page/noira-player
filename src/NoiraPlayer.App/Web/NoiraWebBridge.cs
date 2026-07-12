using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NoiraPlayer.App.Navigation;
using NoiraPlayer.App.Services;
using NoiraPlayer.App.Storage;
using NoiraPlayer.App.ViewModels;
using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Storage;

namespace NoiraPlayer.App.Web
{
    internal sealed class NoiraWebBridge
    {
        private readonly ISessionStore _sessionStore;
        private readonly EmbyMetadataTransport _metadataTransport;

        public NoiraWebBridge()
            : this(new ApplicationDataSessionStore(), EmbyMetadataTransport.CreateDefault())
        {
        }

        internal NoiraWebBridge(ISessionStore sessionStore)
            : this(sessionStore, EmbyMetadataTransport.CreateDefault())
        {
        }

        internal NoiraWebBridge(
            ISessionStore sessionStore,
            EmbyMetadataTransport metadataTransport)
        {
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            _metadataTransport = metadataTransport ?? throw new ArgumentNullException(nameof(metadataTransport));
        }

        public async Task<NoiraWebBridgeResult> HandleAsync(
            string messageSource,
            Uri allowedPageSource,
            string messageJson)
        {
            if (!IsAllowedSource(messageSource, allowedPageSource))
            {
                return Result(Error("", "disallowed-origin", "The bridge message origin is not allowed."));
            }

            var responseId = "";
            try
            {
                using var document = JsonDocument.Parse(messageJson);
                var root = document.RootElement;
                var id = ReadString(root, "id");
                responseId = id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    return Result(Error("", "invalid-message", "Bridge messages require an id."));
                }

                switch (ReadString(root, "type"))
                {
                    case "auth.bootstrap":
                        return Result(Ok(id, CreateBootstrapJson(await _sessionStore.LoadAsync())));

                    case "auth.login":
                        return await LoginAsync(id, root);

                    case "auth.logout":
                        await _sessionStore.ClearAsync();
                        return Result(Ok(id, "{\"signedOut\":true}"));

                    case "emby.get":
                        return await GetEmbyAsync(id, root);

                    case "playback.nativePlayItem":
                        return CreatePlaybackResult(id, root);

                    default:
                        return Result(Error(id, "unknown-command", "Unknown bridge command."));
                }
            }
            catch (JsonException)
            {
                return Result(Error("", "invalid-message", "The bridge message is not valid JSON."));
            }
            catch (Exception)
            {
                return Result(Error(responseId, "bridge-failed", "The native bridge could not complete the request."));
            }
        }

        internal static bool IsAllowedSource(string source, Uri allowedPageSource)
        {
            if (allowedPageSource == null ||
                !Uri.TryCreate(source, UriKind.Absolute, out var sourceUri))
            {
                return false;
            }

            return Uri.Compare(
                sourceUri,
                allowedPageSource,
                UriComponents.SchemeAndServer,
                UriFormat.SafeUnescaped,
                StringComparison.OrdinalIgnoreCase) == 0;
        }

        private async Task<NoiraWebBridgeResult> LoginAsync(string id, JsonElement root)
        {
            var login = new LoginViewModel(_sessionStore)
            {
                ServerUrl = ReadPayloadString(root, "serverUrl", ""),
                UserName = ReadPayloadString(root, "username", ""),
                Password = ReadPayloadString(root, "password", "")
            };

            if (!await login.ConnectAsync())
            {
                return Result(Error(id, "login-failed", login.Status));
            }

            var session = await _sessionStore.LoadAsync();
            if (session == null)
            {
                return Result(Error(id, "session-unavailable", "Login succeeded, but the saved session is unavailable."));
            }

            return Result(Ok(id, CreateBootstrapJson(session)));
        }

        private async Task<NoiraWebBridgeResult> GetEmbyAsync(string id, JsonElement root)
        {
            var session = await _sessionStore.LoadAsync();
            if (session == null)
            {
                return Result(Error(id, "not-authenticated", "Sign in before requesting Emby data."));
            }

            var path = ReadPayloadString(root, "path", "");
            if (!EmbyWebPathPolicy.IsAllowed(session, path))
            {
                return Result(Error(id, "invalid-emby-path", "The requested Emby path is not allowed."));
            }

            var response = await _metadataTransport.GetAsync(
                new Uri(session.ServerUrl.TrimEnd('/') + "/" + path),
                EmbyClientFactory.CreateOptions(session),
                session);
            var resultJson =
                "{\"status\":" + response.StatusCode.ToString(CultureInfo.InvariantCulture) + "," +
                "\"statusText\":" + Quote(response.ReasonPhrase) + "," +
                "\"body\":" + Quote(response.Body) + "," +
                "\"timing\":{" +
                "\"networkMs\":" + response.NetworkDurationMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                "\"bodyBytes\":" + response.BodyLengthBytes.ToString(CultureInfo.InvariantCulture) + "}}";
            return Result(Ok(id, resultJson));
        }

        private static NoiraWebBridgeResult CreatePlaybackResult(string id, JsonElement root)
        {
            var itemId = ReadPayloadString(root, "itemId", "");
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return Result(Error(id, "invalid-playback-request", "Playback requires an item id."));
            }

            var request = new PlaybackLaunchRequest(
                itemId,
                ReadPayloadString(root, "itemName", ""),
                ReadPayloadLong(root, "startPositionTicks", 0),
                ReadPayloadString(root, "mediaSourceId", ""),
                ReadPayloadLong(root, "runtimeTicks", 0));
            return new NoiraWebBridgeResult(
                Ok(id, "{\"started\":true,\"surface\":\"native\"}"),
                request,
                Error(
                    id,
                    "playback-navigation-failed",
                    "The native playback page could not be opened."));
        }

        private static string CreateBootstrapJson(EmbySession? session)
        {
            if (session == null)
            {
                return "{\"session\":null}";
            }

            var options = EmbyClientFactory.CreateOptions(session);
            return
                "{\"session\":{" +
                "\"serverUrl\":" + Quote(session.ServerUrl) + "," +
                "\"userId\":" + Quote(session.UserId) + "," +
                "\"userName\":" + Quote(session.UserName) + "," +
                "\"accessToken\":" + Quote(session.AccessToken) + "," +
                "\"authorization\":" + Quote(EmbyAuthorization.CreateHeaderValue(options, session)) +
                "}}";
        }

        private static string ReadPayloadString(JsonElement root, string propertyName, string fallback)
        {
            if (root.TryGetProperty("payload", out var payload) &&
                payload.ValueKind == JsonValueKind.Object)
            {
                var value = ReadString(payload, propertyName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return fallback;
        }

        private static long ReadPayloadLong(JsonElement root, string propertyName, long fallback)
        {
            if (!root.TryGetProperty("payload", out var payload) ||
                payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty(propertyName, out var value))
            {
                return fallback;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var numericValue))
            {
                return numericValue;
            }

            if (value.ValueKind == JsonValueKind.String &&
                long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stringValue))
            {
                return stringValue;
            }

            return fallback;
        }

        private static string ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
        }

        private static NoiraWebBridgeResult Result(string responseJson)
        {
            return new NoiraWebBridgeResult(responseJson);
        }

        private static string Ok(string id, string resultJson)
        {
            return "{\"id\":" + Quote(id) + ",\"ok\":true,\"result\":" + resultJson + "}";
        }

        private static string Error(string id, string code, string message)
        {
            return
                "{\"id\":" + Quote(id) +
                ",\"ok\":false,\"error\":{\"code\":" + Quote(code) +
                ",\"message\":" + Quote(message) + "}}";
        }

        private static string Quote(string value)
        {
            var builder = new StringBuilder((value == null ? 0 : value.Length) + 2);
            builder.Append('"');
            if (value != null)
            {
                foreach (var character in value)
                {
                    switch (character)
                    {
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            if (character < ' ')
                            {
                                builder.Append("\\u");
                                builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                builder.Append(character);
                            }

                            break;
                    }
                }
            }

            builder.Append('"');
            return builder.ToString();
        }
    }
}
