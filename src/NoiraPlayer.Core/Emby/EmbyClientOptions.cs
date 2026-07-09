namespace NoiraPlayer.Core.Emby
{
    public sealed class EmbyClientOptions
    {
        public string ServerUrl { get; set; } = "";
        public string ClientName { get; set; } = "Noira";
        public string ClientVersion { get; set; } = "0.1.0";
        public string DeviceName { get; set; } = "Xbox";
        public string DeviceId { get; set; } = "";
    }
}
