namespace NoiraPlayer.Core.Emby
{
    public sealed class EmbySession
    {
        public string ServerUrl { get; set; } = "";
        public string UserId { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string UserName { get; set; } = "";
    }
}
