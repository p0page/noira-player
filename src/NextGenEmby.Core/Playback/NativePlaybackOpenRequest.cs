using System;

namespace NextGenEmby.Core.Playback
{
    public sealed class NativePlaybackOpenRequest
    {
        public NativePlaybackOpenRequest(
            string itemId,
            string mediaSourceId,
            string directStreamUrl,
            long startPositionTicks,
            int? audioStreamIndex,
            int? subtitleStreamIndex)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item id is required.", nameof(itemId));
            }

            if (string.IsNullOrWhiteSpace(mediaSourceId))
            {
                throw new ArgumentException("Media source id is required.", nameof(mediaSourceId));
            }

            if (!Uri.TryCreate(directStreamUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Direct stream URL must be an absolute HTTP or HTTPS URL.", nameof(directStreamUrl));
            }

            if (startPositionTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startPositionTicks), "Start position cannot be negative.");
            }

            ItemId = itemId;
            MediaSourceId = mediaSourceId;
            DirectStreamUrl = directStreamUrl;
            StartPositionTicks = startPositionTicks;
            AudioStreamIndex = audioStreamIndex;
            SubtitleStreamIndex = subtitleStreamIndex;
        }

        public string ItemId { get; }

        public string MediaSourceId { get; }

        public string DirectStreamUrl { get; }

        public long StartPositionTicks { get; }

        public int? AudioStreamIndex { get; }

        public int? SubtitleStreamIndex { get; }
    }
}
