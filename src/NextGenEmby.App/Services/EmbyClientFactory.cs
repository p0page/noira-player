using System.Net.Http;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.App.Services
{
    internal static class EmbyClientFactory
    {
        public static EmbyClientOptions CreateOptions(EmbySession session)
        {
            return new EmbyClientOptions
            {
                ServerUrl = session.ServerUrl,
                ClientName = "Next Gen Xbox Emby",
                ClientVersion = "0.1.0",
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
