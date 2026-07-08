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

            return IsBrowseContainer(itemType)
                ? LibraryItemActivationRoute.BrowseFolder
                : LibraryItemActivationRoute.Details;
        }

        private static bool IsBrowseContainer(string itemType)
        {
            return string.Equals(itemType, "Folder", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(itemType, "BoxSet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(itemType, "Playlist", StringComparison.OrdinalIgnoreCase);
        }
    }
}
