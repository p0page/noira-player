namespace NoiraPlayer.Core.Emby
{
    public sealed class EmbyLiveTvChannel
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public string Number { get; set; } = "";

        public string ChannelType { get; set; } = "";

        public string PrimaryImageTag { get; set; } = "";

        public string ThumbImageTag { get; set; } = "";

        public string BackdropImageTag { get; set; } = "";

        public string BannerImageTag { get; set; } = "";

        public EmbyLiveTvProgram? CurrentProgram { get; set; }
    }
}
