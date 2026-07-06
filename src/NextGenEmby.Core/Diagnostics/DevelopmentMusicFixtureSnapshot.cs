using System;
using System.Collections.Generic;
using NextGenEmby.Core.Emby;

namespace NextGenEmby.Core.Diagnostics
{
    public sealed class DevelopmentMusicFixtureSnapshot
    {
        public DevelopmentMusicFixtureSnapshot(
            IReadOnlyList<EmbyMediaItem> artists,
            IReadOnlyList<EmbyMediaItem> albums,
            IReadOnlyList<EmbyMediaItem> songs,
            IReadOnlyDictionary<string, string> artworkUris)
        {
            Artists = artists ?? Array.Empty<EmbyMediaItem>();
            Albums = albums ?? Array.Empty<EmbyMediaItem>();
            Songs = songs ?? Array.Empty<EmbyMediaItem>();
            ArtworkUris = artworkUris ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public IReadOnlyList<EmbyMediaItem> Artists { get; }

        public IReadOnlyList<EmbyMediaItem> Albums { get; }

        public IReadOnlyList<EmbyMediaItem> Songs { get; }

        public IReadOnlyDictionary<string, string> ArtworkUris { get; }
    }
}
