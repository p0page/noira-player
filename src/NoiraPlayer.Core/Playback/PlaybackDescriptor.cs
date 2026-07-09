using System;
using System.Collections.Generic;
using NoiraPlayer.Core.Emby;

namespace NoiraPlayer.Core.Playback
{
    public sealed class PlaybackDescriptor
    {
        public PlaybackDescriptor(
            string itemId,
            EmbyMediaSource mediaSource,
            IReadOnlyList<EmbyMediaSource> availableSources,
            long startPositionTicks,
            int? audioStreamIndex = null,
            int? subtitleStreamIndex = null)
        {
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            MediaSource = mediaSource ?? throw new ArgumentNullException(nameof(mediaSource));
            AvailableSources = availableSources ?? throw new ArgumentNullException(nameof(availableSources));
            StartPositionTicks = startPositionTicks;
            AudioStreamIndex = audioStreamIndex;
            SubtitleStreamIndex = subtitleStreamIndex;
        }

        public string ItemId { get; }
        public EmbyMediaSource MediaSource { get; }
        public IReadOnlyList<EmbyMediaSource> AvailableSources { get; }
        public long StartPositionTicks { get; }
        public int? AudioStreamIndex { get; }
        public int? SubtitleStreamIndex { get; }
    }
}
