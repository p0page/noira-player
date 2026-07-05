namespace NextGenEmby.App.Navigation
{
    internal sealed class PlaybackLaunchRequest
    {
        public PlaybackLaunchRequest(string itemId, string itemName = "", long startPositionTicks = 0)
        {
            ItemId = itemId ?? "";
            ItemName = itemName ?? "";
            StartPositionTicks = startPositionTicks < 0 ? 0 : startPositionTicks;
        }

        public string ItemId { get; }

        public string ItemName { get; }

        public long StartPositionTicks { get; }
    }
}
