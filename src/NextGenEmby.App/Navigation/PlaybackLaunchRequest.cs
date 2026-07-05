namespace NextGenEmby.App.Navigation
{
    internal sealed class PlaybackLaunchRequest
    {
        public PlaybackLaunchRequest(
            string itemId,
            string itemName = "",
            long startPositionTicks = 0,
            string mediaSourceId = "")
        {
            ItemId = itemId ?? "";
            ItemName = itemName ?? "";
            StartPositionTicks = startPositionTicks < 0 ? 0 : startPositionTicks;
            MediaSourceId = mediaSourceId ?? "";
        }

        public string ItemId { get; }

        public string ItemName { get; }

        public long StartPositionTicks { get; }

        public string MediaSourceId { get; }
    }
}
