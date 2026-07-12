using NoiraPlayer.App.Navigation;

namespace NoiraPlayer.App.Web
{
    internal sealed class NoiraWebBridgeResult
    {
        public NoiraWebBridgeResult(
            string responseJson,
            PlaybackLaunchRequest? playbackRequest = null,
            string playbackNavigationFailedResponseJson = "")
        {
            ResponseJson = responseJson ?? "";
            PlaybackRequest = playbackRequest;
            PlaybackNavigationFailedResponseJson = playbackNavigationFailedResponseJson ?? "";
        }

        public string ResponseJson { get; }

        public PlaybackLaunchRequest? PlaybackRequest { get; }

        public string PlaybackNavigationFailedResponseJson { get; }
    }
}
