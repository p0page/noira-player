using System;
using System.Collections.Generic;
using NoiraPlayer.Core.Emby;

namespace NoiraPlayer.Core.Diagnostics
{
    public sealed class DevelopmentLiveTvFixtureSnapshot
    {
        public DevelopmentLiveTvFixtureSnapshot(
            IReadOnlyList<EmbyLiveTvChannel> channels,
            IReadOnlyDictionary<string, string> artworkUris)
        {
            Channels = channels ?? Array.Empty<EmbyLiveTvChannel>();
            ArtworkUris = artworkUris ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public IReadOnlyList<EmbyLiveTvChannel> Channels { get; }

        public IReadOnlyDictionary<string, string> ArtworkUris { get; }
    }
}
