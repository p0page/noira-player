using System.Net.Http;
using System.Net.Http.Headers;

namespace NoiraPlayer.Core.Emby
{
    internal static class EmbyAuthorization
    {
        public static void Apply(HttpRequestMessage request, EmbyClientOptions options, EmbySession? session = null)
        {
            var userId = session?.UserId;
            var accessToken = session?.AccessToken;
            var value = string.IsNullOrWhiteSpace(userId)
                ? FormatClientIdentity(options)
                : $"UserId=\"{userId}\", {FormatClientIdentity(options)}";

            request.Headers.Authorization = new AuthenticationHeaderValue("Emby", value);
            request.Headers.Remove("X-Emby-Token");

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Add("X-Emby-Token", accessToken);
            }
        }

        private static string FormatClientIdentity(EmbyClientOptions options)
        {
            return
                $"Client=\"{options.ClientName}\", " +
                $"Device=\"{options.DeviceName}\", " +
                $"DeviceId=\"{options.DeviceId}\", " +
                $"Version=\"{options.ClientVersion}\"";
        }
    }
}
