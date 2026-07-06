using System.Collections.Generic;

namespace NextGenEmby.Core.Emby
{
    public static class MusicBrowseItemPolicy
    {
        public static IReadOnlyList<EmbyMediaItem> KeepAlbums(IReadOnlyList<EmbyMediaItem> items)
        {
            return EmbyLibraryItemTypePolicy.KeepIncludedItemTypes(items, "MusicAlbum");
        }

        public static IReadOnlyList<EmbyMediaItem> KeepSongs(IReadOnlyList<EmbyMediaItem> items)
        {
            return EmbyLibraryItemTypePolicy.KeepIncludedItemTypes(items, "Audio");
        }
    }
}
