using NextGenEmby.Core.Playback;
using NextGenEmby.Core.Emby;
using System;
using System.Linq;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityReportMapper
    {
        public static void ApplyDisplayStatus(
            PlaybackQualityReport report,
            PlaybackDisplayStatus status)
        {
            report.Display.HdrStatus = status.HdrStatus.ToString();
            report.Display.IsHdrDisplayAvailable = status.IsHdrDisplayAvailable;
            report.Display.IsHdrOutputActive = status.IsHdrOutputActive;
            report.Display.RefreshRateHz = status.RefreshRateHz;
            report.Display.Message = status.Message;

            report.ColorPipeline.ActualHdrOutput = MapActualHdrOutput(status);
            report.ColorPipeline.SwapChainFormat = status.SwapChainFormat;
            report.ColorPipeline.SwapChainColorSpace = status.SwapChainColorSpace;
            report.ColorPipeline.IsTenBitSwapChain = status.IsTenBitSwapChain;
            report.ColorPipeline.IsVideoProcessorColorSpaceValidated =
                status.IsVideoProcessorColorSpaceValidated;
            report.ColorPipeline.DxgiInput = status.VideoProcessorInputColorSpace;
            report.ColorPipeline.DxgiOutput = status.VideoProcessorOutputColorSpace;
            report.ColorPipeline.ConversionStatus = status.VideoProcessorConversionStatus;
        }

        public static void ApplyMetrics(
            PlaybackQualityReport report,
            PlaybackQualityMetricsSnapshot metrics)
        {
            report.Timing.RenderPasses = metrics.RenderPasses;
            report.Timing.DecodedVideoFrames = metrics.DecodedVideoFrames;
            report.Timing.RenderedVideoFrames = metrics.RenderedVideoFrames;
            report.Timing.DroppedVideoFrames = metrics.DroppedVideoFrames;
            report.Timing.SeekPrerollDroppedFrames = metrics.SeekPrerollDroppedFrames;
            report.Timing.VideoAheadWaitCount = metrics.VideoAheadWaitCount;
            report.Timing.AudioAheadWaitCount = metrics.AudioAheadWaitCount;
            report.Timing.VideoClockWaitCount = metrics.VideoClockWaitCount;
            report.Timing.RenderIntervalMsP50 = metrics.RenderIntervalMsP50;
            report.Timing.RenderIntervalMsP95 = metrics.RenderIntervalMsP95;
            report.Timing.RenderIntervalMsP99 = metrics.RenderIntervalMsP99;
            report.Timing.MaxFrameGapMs = metrics.MaxFrameGapMs;
            report.Timing.PresentDurationMsP50 = metrics.PresentDurationMsP50;
            report.Timing.PresentDurationMsP95 = metrics.PresentDurationMsP95;
            report.Timing.PresentDurationMsP99 = metrics.PresentDurationMsP99;
            report.Timing.PresentDurationMsMax = metrics.PresentDurationMsMax;
            report.Timing.AudioAheadWaitDurationMsP50 = metrics.AudioAheadWaitDurationMsP50;
            report.Timing.AudioAheadWaitDurationMsP95 = metrics.AudioAheadWaitDurationMsP95;
            report.Timing.AudioAheadWaitDurationMsP99 = metrics.AudioAheadWaitDurationMsP99;
            report.Timing.AudioAheadWaitDurationMsMax = metrics.AudioAheadWaitDurationMsMax;
            report.Timing.AudioAheadWaitTargetMsP50 = metrics.AudioAheadWaitTargetMsP50;
            report.Timing.AudioAheadWaitTargetMsP95 = metrics.AudioAheadWaitTargetMsP95;
            report.Timing.AudioAheadWaitTargetMsP99 = metrics.AudioAheadWaitTargetMsP99;
            report.Timing.AudioAheadWaitTargetMsMax = metrics.AudioAheadWaitTargetMsMax;
            report.Timing.AudioAheadWaitOversleepMsP50 = metrics.AudioAheadWaitOversleepMsP50;
            report.Timing.AudioAheadWaitOversleepMsP95 = metrics.AudioAheadWaitOversleepMsP95;
            report.Timing.AudioAheadWaitOversleepMsP99 = metrics.AudioAheadWaitOversleepMsP99;
            report.Timing.AudioAheadWaitOversleepMsMax = metrics.AudioAheadWaitOversleepMsMax;
            report.Timing.FramePacingSourceFrameRate = metrics.FramePacingSourceFrameRate;
            report.Timing.LateFrameDropToleranceMs = metrics.LateFrameDropToleranceMs;

            report.Sync.AudioClockTicks = metrics.AudioClockTicks;
            report.Sync.VideoPositionTicks = metrics.VideoPositionTicks;
            report.Sync.AudioVideoDriftMsP50 = metrics.AudioVideoDriftMsP50;
            report.Sync.AudioVideoDriftMsP95 = metrics.AudioVideoDriftMsP95;
            report.Sync.AudioVideoDriftMsP99 = metrics.AudioVideoDriftMsP99;
            report.Sync.AudioVideoDriftMsMax = metrics.AudioVideoDriftMsMax;
            report.Position.ActualPositionTicks = metrics.VideoPositionTicks;

            report.Buffers.SubmittedAudioFrames = metrics.SubmittedAudioFrames;
            report.Buffers.QueuedAudioBuffers = metrics.QueuedAudioBuffers;
            report.Buffers.VideoStarvedPasses = metrics.VideoStarvedPasses;
            report.Buffers.AudioStarvedPasses = metrics.AudioStarvedPasses;
        }

        public static void ApplySource(
            PlaybackQualityReport report,
            PlaybackDescriptor descriptor)
        {
            var source = descriptor.MediaSource;
            var selectedAudio = descriptor.AudioStreamIndex.HasValue
                ? source.AudioStreams.FirstOrDefault(s => s.Index == descriptor.AudioStreamIndex.Value)
                : null;
            var audio = selectedAudio ?? source.AudioStreams.FirstOrDefault();
            var video = source.VideoStreams.FirstOrDefault();

            report.Source.ItemId = descriptor.ItemId;
            report.Source.MediaSourceId = source.Id;
            report.Source.HasDirectStreamUrl = !string.IsNullOrWhiteSpace(source.DirectStreamUrl);
            report.Source.DirectStreamProtocol = MapDirectStreamProtocol(source.DirectStreamUrl);
            report.Source.Container = source.Container;
            report.Source.Bitrate = source.Bitrate;
            report.Source.DurationTicks = source.RunTimeTicks;
            report.Position.RequestedStartPositionTicks = descriptor.StartPositionTicks;
            report.Position.SeekTargetPositionTicks = descriptor.StartPositionTicks;
            report.Source.Codec = FirstNonEmpty(video?.Codec, source.HdrProfile.Codec);
            report.Source.Width = source.Width;
            report.Source.Height = source.Height;
            report.Source.FrameRate = source.VideoFrameRate;
            report.Source.VideoRange = video?.VideoRange ?? "";
            report.Source.ColorPrimaries = video?.ColorPrimaries ?? "";
            report.Source.ColorTransfer = video?.ColorTransfer ?? "";
            report.Source.ColorSpace = video?.ColorSpace ?? "";
            report.Source.Chapters.Clear();
            foreach (var chapter in source.Chapters)
            {
                report.Source.Chapters.Add(new PlaybackQualityChapter
                {
                    Name = chapter.Name,
                    StartPositionTicks = chapter.StartPositionTicks,
                    ImageTag = chapter.ImageTag
                });
            }

            report.Source.HasChapterMetadata =
                source.HasChapterMetadata || report.Source.Chapters.Count > 0;
            report.Source.ChapterCount = report.Source.HasChapterMetadata
                ? report.Source.Chapters.Count
                : (int?)null;
            report.Timing.ExpectedFrameDurationMs = source.VideoFrameRate > 0
                ? 1000.0 / source.VideoFrameRate
                : 0;
            report.Source.HdrKind = source.HdrProfile.Kind.ToString();
            report.Source.HdrPlaybackStrategy = source.HdrProfile.PlaybackStrategy;
            report.Source.IsHdr = source.HdrProfile.IsHdr;
            report.Source.IsDirectPlayable = source.HdrProfile.IsDirectPlayable;
            report.Source.IsDolbyVision = source.HdrProfile.IsDolbyVision;
            report.Source.DolbyVisionProfile = source.HdrProfile.DolbyVisionProfile;
            report.Source.DolbyVisionCompatibilityId = source.HdrProfile.DolbyVisionCompatibilityId;
            report.Source.HasHdr10BaseLayer = source.HdrProfile.HasHdr10BaseLayer;
            report.Source.HasHlgBaseLayer = source.HdrProfile.HasHlgBaseLayer;
            report.Source.AudioCodec = audio?.Codec ?? "";
            ApplyTracks(report, descriptor, video);
        }

        private static void ApplyTracks(
            PlaybackQualityReport report,
            PlaybackDescriptor descriptor,
            EmbyMediaStream? selectedVideo)
        {
            report.Tracks.Video.Clear();
            report.Tracks.Audio.Clear();
            report.Tracks.Subtitles.Clear();

            foreach (var stream in descriptor.MediaSource.VideoStreams)
            {
                report.Tracks.Video.Add(MapTrack(stream));
            }

            foreach (var stream in descriptor.MediaSource.AudioStreams)
            {
                report.Tracks.Audio.Add(MapTrack(stream));
            }

            foreach (var stream in descriptor.MediaSource.SubtitleStreams)
            {
                report.Tracks.Subtitles.Add(MapTrack(stream));
            }

            report.Tracks.VideoTrackCount = report.Tracks.Video.Count;
            report.Tracks.AudioTrackCount = report.Tracks.Audio.Count;
            report.Tracks.SubtitleTrackCount = report.Tracks.Subtitles.Count;
            report.Tracks.SelectedVideoStreamIndex = selectedVideo?.Index;
            report.Tracks.SelectedAudioStreamIndex = descriptor.AudioStreamIndex;
            report.Tracks.SelectedSubtitleStreamIndex = descriptor.SubtitleStreamIndex;
            report.Tracks.IsSubtitleDisabled = !descriptor.SubtitleStreamIndex.HasValue;
        }

        private static PlaybackQualityTrack MapTrack(EmbyMediaStream stream)
        {
            return new PlaybackQualityTrack
            {
                Index = stream.Index,
                Kind = stream.Kind.ToString(),
                Codec = stream.Codec,
                Language = stream.Language,
                ChannelLayout = stream.ChannelLayout,
                Channels = stream.Channels,
                DisplayTitle = stream.DisplayTitle,
                IsExternal = stream.IsExternal,
                IsDefault = stream.IsDefault,
                IsForced = stream.IsForced,
                RealFrameRate = stream.RealFrameRate,
                AverageFrameRate = stream.AverageFrameRate
            };
        }

        private static string MapActualHdrOutput(PlaybackDisplayStatus status)
        {
            switch (status.HdrStatus)
            {
                case HdrOutputStatus.On:
                    return "Hdr10";
                case HdrOutputStatus.Off:
                    return "Sdr";
                case HdrOutputStatus.Unsupported:
                    return "Unsupported";
                case HdrOutputStatus.Failed:
                    return "Failed";
                default:
                    return "Unknown";
            }
        }

        private static string MapDirectStreamProtocol(string directStreamUrl)
        {
            if (string.IsNullOrWhiteSpace(directStreamUrl))
            {
                return "";
            }

            if (directStreamUrl.StartsWith("/", StringComparison.Ordinal))
            {
                return "relative";
            }

            if (directStreamUrl.Length >= 2 &&
                char.IsLetter(directStreamUrl[0]) &&
                directStreamUrl[1] == ':')
            {
                return "file-path";
            }

            if (Uri.TryCreate(directStreamUrl, UriKind.Absolute, out var uri) &&
                !string.IsNullOrWhiteSpace(uri.Scheme))
            {
                return uri.Scheme.ToLowerInvariant();
            }

            return "unknown";
        }

        private static string FirstNonEmpty(string? first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first! : second ?? "";
        }
    }
}
