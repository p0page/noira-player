using NoiraPlayer.Core.Playback;
using NoiraPlayer.Core.Emby;
using System;
using System.Linq;

namespace NoiraPlayer.Core.PlaybackQuality
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
            report.Timing.HardwareDecodedVideoFrames = metrics.HardwareDecodedVideoFrames;
            report.Timing.SoftwareDecodedVideoFrames = metrics.SoftwareDecodedVideoFrames;
            report.Timing.RenderedVideoFrames = metrics.RenderedVideoFrames;
            report.Timing.DroppedVideoFrames = metrics.DroppedVideoFrames;
            report.Timing.SeekPrerollDroppedFrames = metrics.SeekPrerollDroppedFrames;
            report.Timing.VideoAheadWaitCount = metrics.VideoAheadWaitCount;
            report.Timing.AudioAheadWaitCount = metrics.AudioAheadWaitCount;
            report.Timing.VideoClockWaitCount = metrics.VideoClockWaitCount;
            report.Timing.RenderIntervalMsP05 = metrics.RenderIntervalMsP05;
            report.Timing.RenderIntervalMsP50 = metrics.RenderIntervalMsP50;
            report.Timing.RenderIntervalMsP95 = metrics.RenderIntervalMsP95;
            report.Timing.RenderIntervalMsP99 = metrics.RenderIntervalMsP99;
            report.Timing.MinFrameGapMs = metrics.MinFrameGapMs;
            report.Timing.MaxFrameGapMs = metrics.MaxFrameGapMs;
            report.Timing.RenderIntervalSampleCount = metrics.RenderIntervalSampleCount;
            report.Timing.RenderIntervalOverExpected2MsCount = metrics.RenderIntervalOverExpected2MsCount;
            report.Timing.RenderIntervalOverExpected4MsCount = metrics.RenderIntervalOverExpected4MsCount;
            report.Timing.RenderIntervalUnderExpected2MsCount = metrics.RenderIntervalUnderExpected2MsCount;
            report.Timing.RenderIntervalUnderExpected4MsCount = metrics.RenderIntervalUnderExpected4MsCount;
            report.Timing.RenderIntervalAfterAudioAheadWaitSampleCount = metrics.RenderIntervalAfterAudioAheadWaitSampleCount;
            report.Timing.RenderIntervalAfterAudioAheadWaitMsP95 = metrics.RenderIntervalAfterAudioAheadWaitMsP95;
            report.Timing.RenderIntervalAfterAudioAheadWaitMsP99 = metrics.RenderIntervalAfterAudioAheadWaitMsP99;
            report.Timing.RenderIntervalAfterAudioAheadWaitMsMax = metrics.RenderIntervalAfterAudioAheadWaitMsMax;
            report.Timing.AudioAheadWaitEndToPresentSampleCount = metrics.AudioAheadWaitEndToPresentSampleCount;
            report.Timing.AudioAheadWaitEndToPresentMsP50 = metrics.AudioAheadWaitEndToPresentMsP50;
            report.Timing.AudioAheadWaitEndToPresentMsP95 = metrics.AudioAheadWaitEndToPresentMsP95;
            report.Timing.AudioAheadWaitEndToPresentMsP99 = metrics.AudioAheadWaitEndToPresentMsP99;
            report.Timing.AudioAheadWaitEndToPresentMsMax = metrics.AudioAheadWaitEndToPresentMsMax;
            report.Timing.RenderIntervalAfterNonAudioWaitSampleCount = metrics.RenderIntervalAfterNonAudioWaitSampleCount;
            report.Timing.RenderIntervalAfterNonAudioWaitMsP95 = metrics.RenderIntervalAfterNonAudioWaitMsP95;
            report.Timing.RenderIntervalAfterNonAudioWaitMsP99 = metrics.RenderIntervalAfterNonAudioWaitMsP99;
            report.Timing.RenderIntervalAfterNonAudioWaitMsMax = metrics.RenderIntervalAfterNonAudioWaitMsMax;
            report.Timing.PresentDurationMsP50 = metrics.PresentDurationMsP50;
            report.Timing.PresentDurationMsP95 = metrics.PresentDurationMsP95;
            report.Timing.PresentDurationMsP99 = metrics.PresentDurationMsP99;
            report.Timing.PresentDurationMsMax = metrics.PresentDurationMsMax;
            report.Timing.VideoDecodeDurationMsP50 = metrics.VideoDecodeDurationMsP50;
            report.Timing.VideoDecodeDurationMsP95 = metrics.VideoDecodeDurationMsP95;
            report.Timing.VideoDecodeDurationMsP99 = metrics.VideoDecodeDurationMsP99;
            report.Timing.VideoDecodeDurationMsMax = metrics.VideoDecodeDurationMsMax;
            report.Timing.VideoDecodePacketReadDurationMsP50 = metrics.VideoDecodePacketReadDurationMsP50;
            report.Timing.VideoDecodePacketReadDurationMsP95 = metrics.VideoDecodePacketReadDurationMsP95;
            report.Timing.VideoDecodeSendPacketDurationMsP50 = metrics.VideoDecodeSendPacketDurationMsP50;
            report.Timing.VideoDecodeSendPacketDurationMsP95 = metrics.VideoDecodeSendPacketDurationMsP95;
            report.Timing.VideoDecodeReceiveFrameDurationMsP50 = metrics.VideoDecodeReceiveFrameDurationMsP50;
            report.Timing.VideoDecodeReceiveFrameDurationMsP95 = metrics.VideoDecodeReceiveFrameDurationMsP95;
            report.Timing.VideoDecodeFrameMaterializeDurationMsP50 = metrics.VideoDecodeFrameMaterializeDurationMsP50;
            report.Timing.VideoDecodeFrameMaterializeDurationMsP95 = metrics.VideoDecodeFrameMaterializeDurationMsP95;
            report.Timing.VideoRenderDurationMsP50 = metrics.VideoRenderDurationMsP50;
            report.Timing.VideoRenderDurationMsP95 = metrics.VideoRenderDurationMsP95;
            report.Timing.VideoRenderDurationMsP99 = metrics.VideoRenderDurationMsP99;
            report.Timing.VideoRenderDurationMsMax = metrics.VideoRenderDurationMsMax;
            report.Timing.AudioAheadWaitDurationMsP50 = metrics.AudioAheadWaitDurationMsP50;
            report.Timing.AudioAheadWaitDurationMsP95 = metrics.AudioAheadWaitDurationMsP95;
            report.Timing.AudioAheadWaitDurationMsP99 = metrics.AudioAheadWaitDurationMsP99;
            report.Timing.AudioAheadWaitDurationMsMax = metrics.AudioAheadWaitDurationMsMax;
            report.Timing.AudioAheadWaitTargetMsP50 = metrics.AudioAheadWaitTargetMsP50;
            report.Timing.AudioAheadWaitTargetMsP95 = metrics.AudioAheadWaitTargetMsP95;
            report.Timing.AudioAheadWaitTargetMsP99 = metrics.AudioAheadWaitTargetMsP99;
            report.Timing.AudioAheadWaitTargetMsMax = metrics.AudioAheadWaitTargetMsMax;
            report.Timing.AudioAheadWaitOversleepSemantics = metrics.AudioAheadWaitOversleepSemantics;
            report.Timing.AudioAheadWaitOversleepMsP50 = metrics.AudioAheadWaitOversleepMsP50;
            report.Timing.AudioAheadWaitOversleepMsP95 = metrics.AudioAheadWaitOversleepMsP95;
            report.Timing.AudioAheadWaitOversleepMsP99 = metrics.AudioAheadWaitOversleepMsP99;
            report.Timing.AudioAheadWaitOversleepMsMax = metrics.AudioAheadWaitOversleepMsMax;
            report.Timing.AudioAheadWaitFinalDeltaAbsMsP50 = metrics.AudioAheadWaitFinalDeltaAbsMsP50;
            report.Timing.AudioAheadWaitFinalDeltaAbsMsP95 = metrics.AudioAheadWaitFinalDeltaAbsMsP95;
            report.Timing.AudioAheadWaitFinalDeltaAbsMsP99 = metrics.AudioAheadWaitFinalDeltaAbsMsP99;
            report.Timing.AudioAheadWaitFinalDeltaAbsMsMax = metrics.AudioAheadWaitFinalDeltaAbsMsMax;
            report.Timing.AudioAheadWaitEpisodeCount = metrics.AudioAheadWaitEpisodeCount;
            report.Timing.AudioAheadWaitPassesPerEpisodeP50 = metrics.AudioAheadWaitPassesPerEpisodeP50;
            report.Timing.AudioAheadWaitPassesPerEpisodeP95 = metrics.AudioAheadWaitPassesPerEpisodeP95;
            report.Timing.AudioAheadWaitPassesPerEpisodeP99 = metrics.AudioAheadWaitPassesPerEpisodeP99;
            report.Timing.AudioAheadWaitPassesPerEpisodeMax = metrics.AudioAheadWaitPassesPerEpisodeMax;
            report.Timing.AudioAheadWaitPassDurationMsP50 = metrics.AudioAheadWaitPassDurationMsP50;
            report.Timing.AudioAheadWaitPassDurationMsP95 = metrics.AudioAheadWaitPassDurationMsP95;
            report.Timing.AudioAheadWaitPassDurationMsP99 = metrics.AudioAheadWaitPassDurationMsP99;
            report.Timing.AudioAheadWaitPassDurationMsMax = metrics.AudioAheadWaitPassDurationMsMax;
            report.Timing.AudioAheadWaitPassTargetMsP50 = metrics.AudioAheadWaitPassTargetMsP50;
            report.Timing.AudioAheadWaitPassTargetMsP95 = metrics.AudioAheadWaitPassTargetMsP95;
            report.Timing.AudioAheadWaitPassTargetMsP99 = metrics.AudioAheadWaitPassTargetMsP99;
            report.Timing.AudioAheadWaitPassTargetMsMax = metrics.AudioAheadWaitPassTargetMsMax;
            report.Timing.AudioAheadWaitPassOversleepMsP50 = metrics.AudioAheadWaitPassOversleepMsP50;
            report.Timing.AudioAheadWaitPassOversleepMsP95 = metrics.AudioAheadWaitPassOversleepMsP95;
            report.Timing.AudioAheadWaitPassOversleepMsP99 = metrics.AudioAheadWaitPassOversleepMsP99;
            report.Timing.AudioAheadWaitPassOversleepMsMax = metrics.AudioAheadWaitPassOversleepMsMax;
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
            report.Buffers.PlaybackDemuxReadDurationMs = metrics.PlaybackDemuxReadDurationMs;
            report.Buffers.PlaybackDemuxPacketCount = metrics.PlaybackDemuxPacketCount;
            report.Buffers.PlaybackDemuxBytes = metrics.PlaybackDemuxBytes;
            report.Buffers.PlaybackTransportProvider = metrics.PlaybackTransportCalls.Provider;
            report.Buffers.PlaybackTransportCallEvidenceStatus =
                metrics.PlaybackTransportCalls.EvidenceAvailable ? "available" : "unavailable";
            if (metrics.PlaybackTransportCalls.EvidenceAvailable)
            {
                report.Buffers.PlaybackTransportReadCalls = metrics.PlaybackTransportCalls.ReadCalls;
                report.Buffers.PlaybackTransportSeekCalls = metrics.PlaybackTransportCalls.SeekCalls;
                report.Buffers.PlaybackTransportReadWaitMs = metrics.PlaybackTransportCalls.ReadWaitMs;
                report.Buffers.PlaybackTransportSeekWaitMs = metrics.PlaybackTransportCalls.SeekWaitMs;
                report.Buffers.PlaybackTransportSeekDistanceBytes = metrics.PlaybackTransportCalls.SeekDistanceBytes;
            }
            report.ReadRecovery.ReadErrorCount = metrics.ReadErrorCount;
            report.ReadRecovery.ReadRetryCount = metrics.ReadRetryCount;
            report.ReadRecovery.ReadRecoveryCount = metrics.ReadRecoveryCount;
            report.ReadRecovery.MaxConsecutiveReadErrors = metrics.MaxConsecutiveReadErrors;
            report.ReadRecovery.LastReadErrorCode = metrics.LastReadErrorCode;
            report.ReadRecovery.FatalReadErrorCode = metrics.FatalReadErrorCode;
            report.ReadRecovery.LastReadRecoveryDurationMs = metrics.LastReadRecoveryDurationMs;
            report.Tracks.SubtitleDecodedCueCount = metrics.SubtitleDecodedCueCount;
            report.Tracks.SubtitleCueRenderCount = metrics.SubtitleCueRenderCount;
            if (metrics.SelectedSubtitleStreamIndex >= 0)
            {
                report.Tracks.SelectedSubtitleStreamIndex = metrics.SelectedSubtitleStreamIndex;
                report.Tracks.IsSubtitleDisabled = false;
            }
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
                    return "Sdr";
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
