namespace NextGenEmby.App.Navigation
{
    internal sealed class PhotoViewerNavigationRequest
    {
        public PhotoViewerNavigationRequest(string itemId, string itemName)
        {
            ItemId = itemId ?? "";
            ItemName = itemName ?? "";
        }

        public string ItemId { get; }

        public string ItemName { get; }
    }
}
