namespace NoiraPlayer.App.Navigation
{
    internal sealed class PhotoViewerNavigationRequest
    {
        public PhotoViewerNavigationRequest(string itemId, string itemName, string developmentImageUri = "")
        {
            ItemId = itemId ?? "";
            ItemName = itemName ?? "";
            DevelopmentImageUri = developmentImageUri ?? "";
        }

        public string ItemId { get; }

        public string ItemName { get; }

        public string DevelopmentImageUri { get; }
    }
}
