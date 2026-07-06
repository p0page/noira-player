using System;
using System.Collections.Generic;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.Diagnostics
{
    public static class DevelopmentDetailsFixture
    {
        private const string ArtworkTag = "qa";
        private const long MinuteTicks = TimeSpan.TicksPerMinute;

        public static DevelopmentDetailsFixtureSnapshot Create()
        {
            var artworkUris = new Dictionary<string, string>(StringComparer.Ordinal);

            var item = MediaItem(
                artworkUris,
                "fixture-detail-aurora",
                "Aurora Protocol",
                "Movie",
                2026,
                118,
                "qa-poster-01.png",
                "qa-wide-01.png",
                resumeMinutes: 24);
            item.Overview =
                "A signal analyst follows a rogue broadcast through abandoned orbital relays and uncovers a quiet conspiracy hidden inside a forgotten media archive.";
            item.People = new[]
            {
                Person(artworkUris, "fixture-person-maya", "Maya Chen", "Lena Ortiz", "Actor", "qa-poster-09.png"),
                Person(artworkUris, "fixture-person-owen", "Owen Vale", "Director", "Director", "qa-poster-10.png"),
                Person(artworkUris, "fixture-person-iris", "Iris Nakamura", "Composer", "Composer", "qa-poster-11.png")
            };

            var mediaSources = new[]
            {
                MediaSource(
                    "fixture-source-4k",
                    "4K SDR Direct",
                    "mkv",
                    3840,
                    2160,
                    22_000_000,
                    videoCodec: "hevc",
                    audioOne: "English EAC3 5.1",
                    audioTwo: "Japanese AAC Stereo",
                    subtitleOne: "English Full",
                    subtitleTwo: "Chinese Simplified"),
                MediaSource(
                    "fixture-source-1080p",
                    "1080p fallback",
                    "mp4",
                    1920,
                    1080,
                    8_500_000,
                    videoCodec: "h264",
                    audioOne: "English AAC Stereo",
                    audioTwo: "",
                    subtitleOne: "English SDH",
                    subtitleTwo: "")
            };

            var organizeAncestors = new[]
            {
                Target(artworkUris, "fixture-collection-night", "Night City Collection", "BoxSet", 2026, 7, "qa-wide-06.png"),
                Target(artworkUris, "fixture-playlist-weekend", "Weekend Queue", "Playlist", 2026, 12, "qa-wide-08.png")
            };
            var collectionTargets = new[]
            {
                Target(artworkUris, "fixture-collection-night", "Night City Collection", "BoxSet", 2026, 7, "qa-wide-06.png"),
                Target(artworkUris, "fixture-collection-signal", "Signal Archives", "BoxSet", 2025, 5, "qa-wide-07.png")
            };
            var playlistTargets = new[]
            {
                Target(artworkUris, "fixture-playlist-weekend", "Weekend Queue", "Playlist", 2026, 12, "qa-wide-08.png"),
                Target(artworkUris, "fixture-playlist-late", "Late Night Watchlist", "Playlist", 2024, 18, "qa-wide-09.png")
            };
            var similarItems = new[]
            {
                MediaItem(artworkUris, "fixture-similar-midnight", "Midnight Signal", "Movie", 2025, 104, "qa-poster-02.png", "qa-wide-02.png"),
                MediaItem(artworkUris, "fixture-similar-harbor", "Harbor Run", "Movie", 2024, 96, "qa-poster-03.png", "qa-wide-03.png"),
                MediaItem(artworkUris, "fixture-similar-afterimage", "Afterimage", "Movie", 2026, 111, "qa-poster-04.png", "qa-wide-04.png"),
                MediaItem(artworkUris, "fixture-similar-orbit", "Quiet Orbit", "Movie", 2023, 126, "qa-poster-05.png", "qa-wide-05.png")
            };

            return new DevelopmentDetailsFixtureSnapshot(
                item,
                mediaSources,
                organizeAncestors,
                collectionTargets,
                playlistTargets,
                similarItems,
                artworkUris);
        }

        public static string ArtworkKey(string itemId, string imageType)
        {
            return (itemId ?? "") + "|" + (imageType ?? "");
        }

        private static EmbyMediaItem MediaItem(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            string type,
            int year,
            int runtimeMinutes,
            string posterAsset,
            string backdropAsset,
            int resumeMinutes = 0)
        {
            AddArtwork(artworkUris, id, "Primary", posterAsset);
            AddArtwork(artworkUris, id, "Backdrop", backdropAsset);
            AddArtwork(artworkUris, id, "Thumb", backdropAsset);

            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = type,
                ProductionYear = year,
                RunTimeTicks = runtimeMinutes * MinuteTicks,
                PrimaryImageTag = ArtworkTag,
                PrimaryImageItemId = id,
                BackdropImageTag = ArtworkTag,
                BackdropImageItemId = id,
                ThumbImageTag = ArtworkTag,
                ThumbImageItemId = id,
                UserData = new EmbyUserData
                {
                    PlaybackPositionTicks = Math.Max(0, resumeMinutes) * MinuteTicks,
                    PlayedPercentage = resumeMinutes <= 0 || runtimeMinutes <= 0
                        ? null
                        : (double)resumeMinutes / runtimeMinutes * 100d
                }
            };
        }

        private static EmbyMediaItem Target(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            string type,
            int year,
            int childCount,
            string thumbAsset)
        {
            AddArtwork(artworkUris, id, "Thumb", thumbAsset);
            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = type,
                ProductionYear = year,
                ChildCount = childCount,
                ThumbImageTag = ArtworkTag,
                ThumbImageItemId = id
            };
        }

        private static EmbyPerson Person(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            string role,
            string type,
            string primaryAsset)
        {
            AddArtwork(artworkUris, id, "Primary", primaryAsset);
            return new EmbyPerson
            {
                Id = id,
                Name = name,
                Role = role,
                Type = type,
                PrimaryImageTag = ArtworkTag
            };
        }

        private static EmbyMediaSource MediaSource(
            string id,
            string name,
            string container,
            int width,
            int height,
            long bitrate,
            string videoCodec,
            string audioOne,
            string audioTwo,
            string subtitleOne,
            string subtitleTwo)
        {
            var source = new EmbyMediaSource
            {
                Id = id,
                Name = name,
                Container = container,
                Width = width,
                Height = height,
                Bitrate = bitrate,
                HdrProfile = HdrPlaybackProfile.Sdr()
            };
            source.Streams.Add(new EmbyMediaStream
            {
                Index = 0,
                Kind = EmbyStreamKind.Video,
                Codec = videoCodec,
                AverageFrameRate = 23.976,
                RealFrameRate = 23.976
            });
            AddAudioStream(source, 1, audioOne);
            AddAudioStream(source, 2, audioTwo);
            AddSubtitleStream(source, 10, subtitleOne);
            AddSubtitleStream(source, 11, subtitleTwo);
            return source;
        }

        private static void AddAudioStream(EmbyMediaSource source, int index, string displayTitle)
        {
            if (string.IsNullOrWhiteSpace(displayTitle))
            {
                return;
            }

            source.Streams.Add(new EmbyMediaStream
            {
                Index = index,
                Kind = EmbyStreamKind.Audio,
                Codec = displayTitle.IndexOf("EAC3", StringComparison.OrdinalIgnoreCase) >= 0 ? "eac3" : "aac",
                Language = displayTitle.StartsWith("Japanese", StringComparison.OrdinalIgnoreCase) ? "jpn" : "eng",
                ChannelLayout = displayTitle.IndexOf("5.1", StringComparison.OrdinalIgnoreCase) >= 0 ? "5.1" : "stereo",
                DisplayTitle = displayTitle
            });
        }

        private static void AddSubtitleStream(EmbyMediaSource source, int index, string displayTitle)
        {
            if (string.IsNullOrWhiteSpace(displayTitle))
            {
                return;
            }

            source.Streams.Add(new EmbyMediaStream
            {
                Index = index,
                Kind = EmbyStreamKind.Subtitle,
                Codec = "srt",
                Language = displayTitle.StartsWith("Chinese", StringComparison.OrdinalIgnoreCase) ? "chi" : "eng",
                DisplayTitle = displayTitle,
                IsExternal = true
            });
        }

        private static void AddArtwork(
            IDictionary<string, string> artworkUris,
            string itemId,
            string imageType,
            string assetName)
        {
            artworkUris[ArtworkKey(itemId, imageType)] = "ms-appx:///Assets/QaHome/" + assetName;
        }
    }
}
