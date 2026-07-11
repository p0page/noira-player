using System.Net.Http;
using System.Net.Http.Headers;

namespace NoiraPlayer.Core.Emby
{
    public static class EmbyAuthorization
    {
        public static string CreateHeaderValue(EmbyClientOptions options, EmbySession? session = null)
        {
            return "Emby " + CreateHeaderParameter(options, session);
        }

        public static void Apply(HttpRequestMessage request, EmbyClientOptions options, EmbySession? session = null)
        {
            var accessToken = session?.AccessToken;

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Emby",
                CreateHeaderParameter(options, session));
            request.Headers.Remove("X-Emby-Token");

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Add("X-Emby-Token", accessToken);
            }
        }

        private static string CreateHeaderParameter(EmbyClientOptions options, EmbySession? session)
        {
            var userId = session?.UserId;
            return string.IsNullOrWhiteSpace(userId)
                ? FormatClientIdentity(options)
                : $"UserId=\"{userId}\", {FormatClientIdentity(options)}";
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
