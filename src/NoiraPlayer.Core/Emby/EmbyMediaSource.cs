using System.Collections.Generic;
using System.Linq;
using NoiraPlayer.Core.Playback;

namespace NoiraPlayer.Core.Emby
{
    public sealed class EmbyMediaSource
    {
        private HdrPlaybackProfile _hdrProfile = HdrPlaybackProfile.Sdr();

        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Container { get; set; } = "";
        public long Bitrate { get; set; }
        public long RunTimeTicks { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double VideoFrameRate { get; set; }
        public HdrPlaybackProfile HdrProfile
        {
            get { return _hdrProfile; }
            set { _hdrProfile = value ?? HdrPlaybackProfile.Sdr(); }
        }

        public bool IsHdr
        {
            get { return HdrProfile.IsHdr; }
            set
            {
                HdrProfile = value
                    ? HdrPlaybackProfile.LegacyHdr()
                    : HdrPlaybackProfile.Sdr();
            }
        }

        public string DirectStreamUrl { get; set; } = "";
        public string PlaySessionId { get; set; } = "";
        public bool HasChapterMetadata { get; set; }
        public List<EmbyMediaStream> Streams { get; } = new List<EmbyMediaStream>();
        public List<EmbyChapter> Chapters { get; } = new List<EmbyChapter>();

        public IEnumerable<EmbyMediaStream> VideoStreams => Streams.Where(s => s.Kind == EmbyStreamKind.Video);
        public IEnumerable<EmbyMediaStream> AudioStreams => Streams.Where(s => s.Kind == EmbyStreamKind.Audio);
        public IEnumerable<EmbyMediaStream> SubtitleStreams => Streams.Where(s => s.Kind == EmbyStreamKind.Subtitle);
    }
}
