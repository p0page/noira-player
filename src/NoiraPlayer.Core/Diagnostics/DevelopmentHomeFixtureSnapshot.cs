using System;
using System.Collections.Generic;
using NoiraPlayer.Core.Emby;

namespace NoiraPlayer.Core.Diagnostics
{
    public sealed class DevelopmentHomeFixtureSnapshot
    {
        public DevelopmentHomeFixtureSnapshot(
            IReadOnlyList<EmbyMediaItem> continueItems,
            IReadOnlyList<EmbyMediaItem> nextUpItems,
            IReadOnlyList<EmbyMediaItem> latestItems,
            IReadOnlyList<EmbyLibraryView> libraryViews,
            IReadOnlyDictionary<string, IReadOnlyList<EmbyMediaItem>> libraryPreviews,
            IReadOnlyList<DevelopmentHomeMediaRow> configuredRows,
            IReadOnlyList<DevelopmentHomeMediaRow> popularRows,
            IReadOnlyDictionary<string, string> artworkUris)
        {
            ContinueItems = continueItems ?? Array.Empty<EmbyMediaItem>();
            NextUpItems = nextUpItems ?? Array.Empty<EmbyMediaItem>();
            LatestItems = latestItems ?? Array.Empty<EmbyMediaItem>();
            LibraryViews = libraryViews ?? Array.Empty<EmbyLibraryView>();
            LibraryPreviews = libraryPreviews ?? new Dictionary<string, IReadOnlyList<EmbyMediaItem>>(StringComparer.Ordinal);
            ConfiguredRows = configuredRows ?? Array.Empty<DevelopmentHomeMediaRow>();
            PopularRows = popularRows ?? Array.Empty<DevelopmentHomeMediaRow>();
            ArtworkUris = artworkUris ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public IReadOnlyList<EmbyMediaItem> ContinueItems { get; }

        public IReadOnlyList<EmbyMediaItem> NextUpItems { get; }

        public IReadOnlyList<EmbyMediaItem> LatestItems { get; }

        public IReadOnlyList<EmbyLibraryView> LibraryViews { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<EmbyMediaItem>> LibraryPreviews { get; }

        public IReadOnlyList<DevelopmentHomeMediaRow> ConfiguredRows { get; }

        public IReadOnlyList<DevelopmentHomeMediaRow> PopularRows { get; }

        public IReadOnlyDictionary<string, string> ArtworkUris { get; }
    }
}
