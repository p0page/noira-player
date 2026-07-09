namespace NoiraPlayer.App.Navigation
{
    internal sealed class LiveTvNavigationRequest
    {
        public LiveTvNavigationRequest(string unsupportedChannelName = "")
        {
            UnsupportedChannelName = unsupportedChannelName ?? "";
        }

        public string UnsupportedChannelName { get; }

    }
}
