using System.Collections.Generic;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Diagnostics
{
    public sealed class DevelopmentDetailsFixtureSnapshot
    {
        public DevelopmentDetailsFixtureSnapshot(
            EmbyMediaItem item,
            IReadOnlyList<EmbyMediaSource> mediaSources,
            IReadOnlyList<EmbyMediaItem> organizeAncestors,
            IReadOnlyList<EmbyMediaItem> collectionTargets,
            IReadOnlyList<EmbyMediaItem> playlistTargets,
            IReadOnlyList<EmbyMediaItem> similarItems,
            IReadOnlyDictionary<string, string> artworkUris)
        {
            Item = item;
            MediaSources = mediaSources;
            OrganizeAncestors = organizeAncestors;
            CollectionTargets = collectionTargets;
            PlaylistTargets = playlistTargets;
            SimilarItems = similarItems;
            ArtworkUris = artworkUris;
        }

        public EmbyMediaItem Item { get; }

        public IReadOnlyList<EmbyMediaSource> MediaSources { get; }

        public IReadOnlyList<EmbyMediaItem> OrganizeAncestors { get; }

        public IReadOnlyList<EmbyMediaItem> CollectionTargets { get; }

        public IReadOnlyList<EmbyMediaItem> PlaylistTargets { get; }

        public IReadOnlyList<EmbyMediaItem> SimilarItems { get; }

        public IReadOnlyDictionary<string, string> ArtworkUris { get; }
    }
}
