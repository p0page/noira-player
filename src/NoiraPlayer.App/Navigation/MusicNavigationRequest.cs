namespace NoiraPlayer.App.Navigation
{
    internal sealed class MusicNavigationRequest
    {
        public MusicNavigationRequest(string unsupportedSongName = "")
        {
            UnsupportedSongName = unsupportedSongName ?? "";
        }

        public string UnsupportedSongName { get; }

    }
}
