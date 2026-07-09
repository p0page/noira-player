namespace NoiraPlayer.Core.Playback
{
    public sealed class ManualDirectStreamLaunchOptions
    {
        public ManualDirectStreamLaunchOptions(string streamUrl = "", bool autoStart = false)
        {
            StreamUrl = streamUrl == null ? "" : streamUrl.Trim();
            AutoStart = autoStart && StreamUrl.Length > 0;
        }

        public string StreamUrl { get; }

        public bool AutoStart { get; }
    }
}
