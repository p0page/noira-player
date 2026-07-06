using System.Net.Http;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.App.Services
{
    internal static class EmbyClientFactory
    {
        public const string ClientVersion = "0.1.0";

        public static EmbyClientOptions CreateOptions(EmbySession session)
        {
            return new EmbyClientOptions
            {
                ServerUrl = session.ServerUrl,
                ClientName = "Next Gen Xbox Emby",
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
