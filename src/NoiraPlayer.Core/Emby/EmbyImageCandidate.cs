namespace NoiraPlayer.Core.Emby
{
    public sealed class EmbyImageCandidate
    {
        public EmbyImageCandidate(string itemId, string imageType, int maxWidth)
        {
            ItemId = itemId ?? "";
            ImageType = imageType ?? "";
            MaxWidth = maxWidth;
        }

        public string ItemId { get; }
        public string ImageType { get; }
        public int MaxWidth { get; }
    }
}
