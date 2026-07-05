using System;
using System.Text.Json;

namespace NextGenEmby.Core.Diagnostics
{
    public sealed class DevelopmentLoginCredentials
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public string ServerUrl { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";

        public static bool TryParseJson(
            string json,
            out DevelopmentLoginCredentials? credentials,
            out string error)
        {
            credentials = null;
            error = "";

            DevelopmentLoginCredentials? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<DevelopmentLoginCredentials>(json, JsonOptions);
            }
            catch (JsonException)
            {
                error = "dev-login.json must contain valid JSON.";
                return false;
            }

            if (parsed == null)
            {
                error = "dev-login.json must contain a JSON object.";
                return false;
            }

            parsed.ServerUrl = Normalize(parsed.ServerUrl);
            parsed.UserName = Normalize(parsed.UserName);
            parsed.Password = parsed.Password == null ? "" : parsed.Password.Trim();

            if (string.IsNullOrWhiteSpace(parsed.ServerUrl))
            {
                error = "dev-login.json is missing serverUrl.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(parsed.UserName))
            {
                error = "dev-login.json is missing username.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(parsed.Password))
            {
                error = "dev-login.json is missing password.";
                return false;
            }

            credentials = parsed;
            return true;
        }

        private static string Normalize(string value)
        {
            return value == null
                ? ""
                : value.Trim().TrimEnd('/');
        }
    }
}
