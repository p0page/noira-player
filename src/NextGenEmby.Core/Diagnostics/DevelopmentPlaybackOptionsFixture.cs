using System;
using System.Collections.Generic;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.Diagnostics
{
    public static class DevelopmentPlaybackOptionsFixture
    {
        public static DevelopmentPlaybackOptionsFixtureSnapshot Create()
        {
            var source4k = CreateSource(
                "fixture-source-4k",
                "4K Direct",
                "mkv",
                3840,
                2160,
                22_000_000,
                HdrPlaybackProfile.Sdr(),
                "https://fixture.local/aurora/4k.mkv",
                new[]
                {
                    Audio(1, "English EAC3 5.1", "eng", "eac3", "5.1"),
                    Audio(2, "Japanese AAC Stereo", "jpn", "aac", "stereo"),
                    Audio(3, "Commentary AAC Stereo", "eng", "aac", "stereo")
                },
                new[]
                {
                    Subtitle(11, "English Full", "eng", false),
                    Subtitle(12, "Chinese Simplified", "chi", false),
                    Subtitle(13, "English SDH External", "eng", true)
                });

            var source1080 = CreateSource(
                "fixture-source-1080p",
                "1080p Compatibility",
                "mp4",
                1920,
                1080,
                8_500_000,
                HdrPlaybackProfile.Sdr(),
                "https://fixture.local/aurora/1080p.mp4",
                new[]
                {
                    Audio(21, "English AAC Stereo", "eng", "aac", "stereo"),
                    Audio(22, "Spanish AAC Stereo", "spa", "aac", "stereo")
                },
                new[]
                {
                    Subtitle(31, "Off-axis English Captions", "eng", false),
                    Subtitle(32, "Spanish Full", "spa", false)
                });

            return new DevelopmentPlaybackOptionsFixtureSnapshot(
                "Aurora Protocol",
                new[] { source4k, source1080 },
                source4k.Id,
                defaultAudioStreamIndex: 1,
                defaultSubtitleStreamIndex: 12,
                runtimeTicks: TimeSpan.FromMinutes(118).Ticks,
                startPositionTicks: TimeSpan.FromMinutes(24).Ticks);
        }

        private static EmbyMediaSource CreateSource(
            string id,
            string name,
            string container,
            int width,
            int height,
            long bitrate,
            HdrPlaybackProfile hdrProfile,
            string directStreamUrl,
            IEnumerable<EmbyMediaStream> audioStreams,
            IEnumerable<EmbyMediaStream> subtitleStreams)
        {
            var source = new EmbyMediaSource
            {
                Id = id,
                Name = name,
                Container = container,
                Width = width,
                Height = height,
                Bitrate = bitrate,
                HdrProfile = hdrProfile,
                DirectStreamUrl = directStreamUrl,
                PlaySessionId = "fixture-play-session"
            };

            source.Streams.Add(new EmbyMediaStream
            {
                Index = 0,
                Kind = EmbyStreamKind.Video,
                Codec = width >= 3840 ? "hevc" : "h264",
                DisplayTitle = width >= 3840 ? "HEVC Main 10" : "H264 High"
            });

            source.Streams.AddRange(audioStreams);
            source.Streams.AddRange(subtitleStreams);
            return source;
        }

        private static EmbyMediaStream Audio(
            int index,
            string displayTitle,
            string language,
            string codec,
            string channelLayout)
        {
            return new EmbyMediaStream
            {
                Index = index,
                Kind = EmbyStreamKind.Audio,
                DisplayTitle = displayTitle,
                Language = language,
                Codec = codec,
                ChannelLayout = channelLayout
            };
        }

        private static EmbyMediaStream Subtitle(
            int index,
            string displayTitle,
            string language,
            bool isExternal)
        {
            return new EmbyMediaStream
            {
                Index = index,
                Kind = EmbyStreamKind.Subtitle,
                DisplayTitle = displayTitle,
                Language = language,
                Codec = "srt",
                IsExternal = isExternal
            };
        }
    }
}
