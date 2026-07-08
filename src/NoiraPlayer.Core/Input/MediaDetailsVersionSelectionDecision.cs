namespace NoiraPlayer.Core.Input
{
    public sealed class MediaDetailsVersionSelectionDecision
    {
        public string SelectedMediaSourceId { get; set; } = "";

        public string StatusMessage { get; set; } = "";

        public bool StartPlayback { get; set; }
    }
}
