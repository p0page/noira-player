using System.Net.Http;
using NoiraPlayer.App.Storage;
using NoiraPlayer.Core.Emby;

namespace NoiraPlayer.App.Services
{
    internal static class EmbyClientFactory
    {
        public const string ClientVersion = "0.1.0";

        public static EmbyClientOptions CreateOptions(EmbySession session)
        {
            return new EmbyClientOptions
            {
                ServerUrl = session.ServerUrl,
                ClientName = "Noira",
                ClientVersion = ClientVersion,
                DeviceName = "Xbox",
                DeviceId = new ApplicationDataDeviceIdProvider().GetOrCreate()
            };
        }

        public static EmbyApiClient Create(HttpClient httpClient, EmbySession session)
        {
            httpClient.Timeout = EmbyRequestTimeoutPolicy.InteractiveRequestTimeout;
            return new EmbyApiClient(httpClient, CreateOptions(session));
        }
    }
}
