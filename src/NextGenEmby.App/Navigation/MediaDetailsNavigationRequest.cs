namespace NextGenEmby.App.Navigation
{
    public sealed class MediaDetailsNavigationRequest
    {
        public MediaDetailsNavigationRequest(
            string itemId,
            string itemName,
            bool useDevelopmentFixture = false)
        {
            ItemId = itemId ?? "";
            ItemName = itemName ?? "";
            UseDevelopmentFixture = useDevelopmentFixture;
        }

        public string ItemId { get; }

        public string ItemName { get; }

        public bool UseDevelopmentFixture { get; }
    }
}
