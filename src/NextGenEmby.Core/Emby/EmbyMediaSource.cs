using System.Collections.Generic;
using System.Linq;

namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyMediaSource
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Container { get; set; } = "";
        public long Bitrate { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsHdr { get; set; }
        public string DirectStreamUrl { get; set; } = "";
        public string PlaySessionId { get; set; } = "";
        public List<EmbyMediaStream> Streams { get; } = new List<EmbyMediaStream>();

        public IEnumerable<EmbyMediaStream> VideoStreams => Streams.Where(s => s.Kind == EmbyStreamKind.Video);
        public IEnumerable<EmbyMediaStream> AudioStreams => Streams.Where(s => s.Kind == EmbyStreamKind.Audio);
        public IEnumerable<EmbyMediaStream> SubtitleStreams => Streams.Where(s => s.Kind == EmbyStreamKind.Subtitle);
    }
}
