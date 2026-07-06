namespace NextGenEmby.App.Navigation
{
    internal sealed class LiveTvNavigationRequest
    {
        public LiveTvNavigationRequest(
            string unsupportedChannelName = "",
            bool useDevelopmentFixture = false)
        {
            UnsupportedChannelName = unsupportedChannelName ?? "";
            UseDevelopmentFixture = useDevelopmentFixture;
        }

        public string UnsupportedChannelName { get; }

        public bool UseDevelopmentFixture { get; }
    }
}
