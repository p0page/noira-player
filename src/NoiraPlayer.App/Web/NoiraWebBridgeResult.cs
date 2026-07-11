using NoiraPlayer.App.Navigation;

namespace NoiraPlayer.App.Web
{
    internal sealed class NoiraWebBridgeResult
    {
        public NoiraWebBridgeResult(string responseJson, PlaybackLaunchRequest? playbackRequest = null)
        {
            ResponseJson = responseJson ?? "";
            PlaybackRequest = playbackRequest;
        }

        public string ResponseJson { get; }

        public PlaybackLaunchRequest? PlaybackRequest { get; }
    }
}
