namespace NextGenEmby.App.Navigation
{
    internal sealed class PlaybackLaunchRequest
    {
        public PlaybackLaunchRequest(
            string itemId,
            string itemName = "",
            long startPositionTicks = 0,
            string mediaSourceId = "",
            long runtimeTicks = 0,
            bool forceSdrOutput = false)
        {
            ItemId = itemId ?? "";
            ItemName = itemName ?? "";
            StartPositionTicks = startPositionTicks < 0 ? 0 : startPositionTicks;
            MediaSourceId = mediaSourceId ?? "";
            RuntimeTicks = runtimeTicks < 0 ? 0 : runtimeTicks;
            ForceSdrOutput = forceSdrOutput;
        }

        public string ItemId { get; }

        public string ItemName { get; }

        public long StartPositionTicks { get; }

        public string MediaSourceId { get; }

        public long RuntimeTicks { get; }

        public bool ForceSdrOutput { get; }
    }
}
