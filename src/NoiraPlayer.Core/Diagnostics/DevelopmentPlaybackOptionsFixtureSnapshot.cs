using System.Collections.Generic;
using NoiraPlayer.Core.Emby;

namespace NoiraPlayer.Core.Diagnostics
{
    public sealed class DevelopmentPlaybackOptionsFixtureSnapshot
    {
        public DevelopmentPlaybackOptionsFixtureSnapshot(
            string itemName,
            IReadOnlyList<EmbyMediaSource> mediaSources,
            string defaultMediaSourceId,
            int? defaultAudioStreamIndex,
            int? defaultSubtitleStreamIndex,
            long runtimeTicks,
            long startPositionTicks)
        {
            ItemName = itemName ?? "";
            MediaSources = mediaSources ?? new List<EmbyMediaSource>();
            DefaultMediaSourceId = defaultMediaSourceId ?? "";
            DefaultAudioStreamIndex = defaultAudioStreamIndex;
            DefaultSubtitleStreamIndex = defaultSubtitleStreamIndex;
            RuntimeTicks = runtimeTicks;
            StartPositionTicks = startPositionTicks;
        }

        public string ItemName { get; }

        public IReadOnlyList<EmbyMediaSource> MediaSources { get; }

        public string DefaultMediaSourceId { get; }

        public int? DefaultAudioStreamIndex { get; }

        public int? DefaultSubtitleStreamIndex { get; }

        public long RuntimeTicks { get; }

        public long StartPositionTicks { get; }
    }
}
