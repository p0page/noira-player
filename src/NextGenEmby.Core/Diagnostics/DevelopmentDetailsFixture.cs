using System;
using System.Collections.Generic;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.Diagnostics
{
    public static class DevelopmentDetailsFixture
    {
        private const string ArtworkTag = "";
        private const long MinuteTicks = TimeSpan.TicksPerMinute;

        public static DevelopmentDetailsFixtureSnapshot Create()
        {
            return CreateCore(ItemArtworkMode.Full, SourceLabelMode.Standard);
        }

        public static DevelopmentDetailsFixtureSnapshot CreateWithoutArtwork()
        {
            return CreateCore(ItemArtworkMode.None, SourceLabelMode.Standard);
        }

        public static DevelopmentDetailsFixtureSnapshot CreateWithPrimaryOnlyArtwork()
        {
            return CreateCore(ItemArtworkMode.PrimaryOnly, SourceLabelMode.Standard);
        }

        public static DevelopmentDetailsFixtureSnapshot CreateWithLongSourceLabels()
        {
            return CreateCore(ItemArtworkMode.Full, SourceLabelMode.LongStress);
        }

        private static DevelopmentDetailsFixtureSnapshot CreateCore(
            ItemArtworkMode artworkMode,
            SourceLabelMode sourceLabelMode)
        {
            var artworkUris = new Dictionary<string, string>(StringComparer.Ordinal);

            var item = CreateMainItem(artworkMode, sourceLabelMode, artworkUris);
            item.Overview = CreateMainItemOverview(artworkMode, sourceLabelMode);
            item.People = new[]
            {
                Person(artworkUris, "fixture-person-maya", "Maya Chen", "Lena Ortiz", "Actor", ""),
                Person(artworkUris, "fixture-person-owen", "Owen Vale", "Director", "Director", ""),
                Person(artworkUris, "fixture-person-iris", "Iris Nakamura", "Composer", "Composer", "")
            };
            item.GenreItems = new[]
            {
                Reference("fixture-genre-sci-fi", "Sci-Fi"),
                Reference("fixture-genre-mystery", "Mystery")
            };
            item.StudioItems = new[]
            {
                Reference("fixture-studio-terminus", "Terminus Pictures")
            };
            item.TagItems = new[]
            {
                Reference("", "Douban Top"),
                Reference("", "Atmospheric")
            };

            var mediaSources = CreateMediaSources(sourceLabelMode);

            var organizeAncestors = new[]
            {
                Target(artworkUris, "fixture-collection-night", "Night City Collection", "BoxSet", 2026, 7, ""),
                Target(artworkUris, "fixture-playlist-weekend", "Weekend Queue", "Playlist", 2026, 12, "")
            };
            var collectionTargets = new[]
            {
                Target(artworkUris, "fixture-collection-night", "Night City Collection", "BoxSet", 2026, 7, ""),
                Target(artworkUris, "fixture-collection-signal", "Signal Archives", "BoxSet", 2025, 5, "")
            };
            var playlistTargets = new[]
            {
                Target(artworkUris, "fixture-playlist-weekend", "Weekend Queue", "Playlist", 2026, 12, ""),
                Target(artworkUris, "fixture-playlist-late", "Late Night Watchlist", "Playlist", 2024, 18, "")
            };
            var similarItems = new[]
            {
                MediaItem(artworkUris, "fixture-similar-midnight", "Midnight Signal", "Movie", 2025, 104, "", ""),
                MediaItem(artworkUris, "fixture-similar-harbor", "Harbor Run", "Movie", 2024, 96, "", ""),
                MediaItem(artworkUris, "fixture-similar-afterimage", "Afterimage", "Movie", 2026, 111, "", ""),
                MediaItem(artworkUris, "fixture-similar-orbit", "Quiet Orbit", "Movie", 2023, 126, "", "")
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

        private enum ItemArtworkMode
        {
            Full,
            None,
            PrimaryOnly
        }

        private enum SourceLabelMode
        {
            Standard,
            LongStress
        }

        private static EmbyMediaItem CreateMainItem(
            ItemArtworkMode artworkMode,
            SourceLabelMode sourceLabelMode,
            IDictionary<string, string> artworkUris)
        {
            if (sourceLabelMode == SourceLabelMode.LongStress)
            {
                return MediaItem(
                    artworkUris,
                    "fixture-detail-long-source",
                    "Long Source Signal",
                    "Movie",
                    2026,
                    118,
                    "",
                    "",
                    resumeMinutes: 24);
            }

            switch (artworkMode)
            {
                case ItemArtworkMode.None:
                    return MediaItemWithoutArtwork(
                        "fixture-detail-no-art",
                        "No Artwork Signal",
                        "Movie",
                        2024,
                        103,
                        resumeMinutes: 0);

                case ItemArtworkMode.PrimaryOnly:
                    return MediaItemWithPrimaryOnlyArtwork(
                        artworkUris,
                        "fixture-detail-primary-only",
                        "Poster Only Signal",
                        "Movie",
                        2025,
                        109,
                        "",
                        resumeMinutes: 19);

                default:
                    return MediaItem(
                        artworkUris,
                        "fixture-detail-aurora",
                        "Aurora Protocol",
                        "Movie",
                        2026,
                        118,
                        "",
                        "",
                        resumeMinutes: 24);
            }
        }

        private static string CreateMainItemOverview(
            ItemArtworkMode artworkMode,
            SourceLabelMode sourceLabelMode)
        {
            if (sourceLabelMode == SourceLabelMode.LongStress)
            {
                return "A deterministic Details stress item with unusually long version, audio, and subtitle names, used to judge whether the low decision area remains cinematic instead of turning into a technical table.";
            }

            switch (artworkMode)
            {
                case ItemArtworkMode.None:
                    return "A library item intentionally missing poster, thumb, and backdrop artwork, used to verify that the Details surface falls back to a quiet matte atmosphere without fake placeholder art.";

                case ItemArtworkMode.PrimaryOnly:
                    return "A library item intentionally exposing only a vertical Primary image, used to verify that Details treats poster-only media as atmosphere instead of a separate poster viewer.";

                default:
                    return "A signal analyst follows a rogue broadcast through abandoned orbital relays and uncovers a quiet conspiracy hidden inside a forgotten media archive.";
            }
        }

        private static IReadOnlyList<EmbyMediaSource> CreateMediaSources(SourceLabelMode sourceLabelMode)
        {
            if (sourceLabelMode == SourceLabelMode.LongStress)
            {
                return new[]
                {
                    MediaSource(
                        "fixture-source-4k-long",
                        "4K SDR archival remaster with director commentary mezzanine encode",
                        "mkv",
                        3840,
                        2160,
                        22_000_000,
                        videoCodec: "hevc",
                        audioOne: "English EAC3 5.1 commentary - director and sound team archival mix",
                        audioTwo: "Japanese AAC stereo original theatrical dialogue preservation track",
                        subtitleOne: "English SDH descriptive captions for quiet dialogue and radio chatter",
                        subtitleTwo: "Chinese Simplified forced narrative signs and alien language captions"),
                    MediaSource(
                        "fixture-source-1080p-long",
                        "1080p fallback mobile compatible archival service encode",
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
            }

            return new[]
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
                    audioTwo: "Japanese AAC stereo",
                    subtitleOne: "English SDH",
                    subtitleTwo: "Chinese Simplified forced"),
                MediaSource(
                    "fixture-source-1080p",
                    "1080p fallback",
                    "mp4",
                    1920,
                    1080,
                    8_500_000,
                    videoCodec: "h264",
                    audioOne: "English AAC stereo",
                    audioTwo: "",
                    subtitleOne: "English SDH",
                    subtitleTwo: "")
            };
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

        private static EmbyMediaItem MediaItemWithoutArtwork(
            string id,
            string name,
            string type,
            int year,
            int runtimeMinutes,
            int resumeMinutes = 0)
        {
            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = type,
                ProductionYear = year,
                RunTimeTicks = runtimeMinutes * MinuteTicks,
                UserData = new EmbyUserData
                {
                    PlaybackPositionTicks = Math.Max(0, resumeMinutes) * MinuteTicks,
                    PlayedPercentage = resumeMinutes <= 0 || runtimeMinutes <= 0
                        ? null
                        : (double)resumeMinutes / runtimeMinutes * 100d
                }
            };
        }

        private static EmbyMediaItem MediaItemWithPrimaryOnlyArtwork(
            IDictionary<string, string> artworkUris,
            string id,
            string name,
            string type,
            int year,
            int runtimeMinutes,
            string posterAsset,
            int resumeMinutes = 0)
        {
            AddArtwork(artworkUris, id, "Primary", posterAsset);

            return new EmbyMediaItem
            {
                Id = id,
                Name = name,
                Type = type,
                ProductionYear = year,
                RunTimeTicks = runtimeMinutes * MinuteTicks,
                PrimaryImageTag = ArtworkTag,
                PrimaryImageItemId = id,
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

        private static EmbyItemReference Reference(string id, string name)
        {
            return new EmbyItemReference
            {
                Id = id ?? "",
                Name = name ?? ""
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
            _ = artworkUris;
            _ = itemId;
            _ = imageType;
            _ = assetName;
        }
    }
}
