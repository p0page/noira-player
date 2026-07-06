using System;

namespace NextGenEmby.Core.Input
{
    public enum LibraryItemActivationRoute
    {
        Details,
        PhotoViewer,
        BrowseFolder
    }

    public static class LibraryItemActivationPolicy
    {
        public static LibraryItemActivationRoute ChooseRoute(string itemType)
        {
            if (string.Equals(itemType, "Photo", StringComparison.OrdinalIgnoreCase))
            {
                return LibraryItemActivationRoute.PhotoViewer;
            }

            return string.Equals(itemType, "Folder", StringComparison.OrdinalIgnoreCase)
                ? LibraryItemActivationRoute.BrowseFolder
                : LibraryItemActivationRoute.Details;
        }
    }
}
