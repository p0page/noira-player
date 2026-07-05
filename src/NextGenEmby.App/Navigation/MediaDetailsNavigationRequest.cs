namespace NextGenEmby.App.Navigation
{
    public sealed class MediaDetailsNavigationRequest
    {
        public MediaDetailsNavigationRequest(string itemId, string itemName)
        {
            ItemId = itemId ?? "";
            ItemName = itemName ?? "";
        }

        public string ItemId { get; }

        public string ItemName { get; }
    }
}
