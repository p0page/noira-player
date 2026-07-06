namespace NextGenEmby.App.Navigation
{
    internal sealed class MusicNavigationRequest
    {
        public MusicNavigationRequest(string unsupportedSongName = "", bool useDevelopmentFixture = false)
        {
            UnsupportedSongName = unsupportedSongName ?? "";
            UseDevelopmentFixture = useDevelopmentFixture;
        }

        public string UnsupportedSongName { get; }

        public bool UseDevelopmentFixture { get; }
    }
}
