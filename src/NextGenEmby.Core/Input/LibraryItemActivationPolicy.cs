using System;

namespace NextGenEmby.Core.Input
{
    public enum LibraryItemActivationRoute
    {
        Details,
        PhotoViewer
    }

    public static class LibraryItemActivationPolicy
    {
        public static LibraryItemActivationRoute ChooseRoute(string itemType)
        {
            return string.Equals(itemType, "Photo", StringComparison.OrdinalIgnoreCase)
                ? LibraryItemActivationRoute.PhotoViewer
                : LibraryItemActivationRoute.Details;
        }
    }
}
