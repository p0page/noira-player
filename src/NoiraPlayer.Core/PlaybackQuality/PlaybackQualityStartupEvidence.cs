using System;
using System.Linq;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityStartupEvidence
    {
        public static void EnrichNativeOpenBreakdown(
            PlaybackQualityStartup startup,
            PlaybackQualityMetricsSnapshot metrics)
        {
            if (startup == null || metrics == null || metrics.NativeGraphOpenDurationMs <= 0)
            {
                return;
            }

            var nativeOpen = startup.Stages.FirstOrDefault(
                stage => string.Equals(stage.Name, "native.open", StringComparison.Ordinal));
            if (nativeOpen == null || nativeOpen.DurationMs <= 0)
            {
                return;
            }

            var openInputMs = Math.Max(0, metrics.FfmpegOpenInputDurationMs);
            var streamInfoMs = Math.Max(0, metrics.FfmpegStreamInfoDurationMs);
            var startupSeekMs = Math.Max(0, metrics.NativeStartupSeekDurationMs);
            var firstFrameMs = Math.Max(0, metrics.NativeFirstFrameDurationMs);
            var firstFrameDemuxMs = Math.Min(
                firstFrameMs,
                Math.Max(0, metrics.NativeFirstFrameDemuxReadDurationMs));
            var firstFramePresentMs = Math.Min(
                Math.Max(0, firstFrameMs - firstFrameDemuxMs),
                Math.Max(0, metrics.NativeFirstFramePresentDurationMs));
            var firstFrameDecodeControlMs = Math.Max(
                0,
                firstFrameMs - firstFrameDemuxMs - firstFramePresentMs);
            var graphOtherMs = Math.Max(
                0,
                metrics.NativeGraphOpenDurationMs - openInputMs - streamInfoMs - startupSeekMs - firstFrameMs);
            var hostDispatchMs = Math.Max(
                0,
                nativeOpen.DurationMs - metrics.NativeGraphOpenDurationMs);

            nativeOpen.Components.Clear();
            Add(
                nativeOpen,
                "ffmpeg.open-input",
                openInputMs,
                bytes: metrics.FfmpegOpenInputBytesRead,
                byteKind: "avio-transport");
            Add(
                nativeOpen,
                "ffmpeg.find-stream-info",
                streamInfoMs,
                bytes: metrics.FfmpegStreamInfoBytesRead,
                byteKind: "avio-transport");
            Add(nativeOpen, "native.initialize-components", graphOtherMs);
            Add(
                nativeOpen,
                "native.startup-seek",
                startupSeekMs,
                bytes: metrics.NativeStartupSeekBytesRead,
                byteKind: "avio-transport");
            Add(
                nativeOpen,
                "native.first-frame.demux-read",
                firstFrameDemuxMs,
                packetCount: metrics.NativeFirstFrameDemuxPacketCount,
                bytes: metrics.NativeFirstFrameDemuxBytes,
                byteKind: "demux-packet-payload");
            Add(nativeOpen, "native.first-frame.decode-control", firstFrameDecodeControlMs);
            Add(nativeOpen, "native.first-frame.present", firstFramePresentMs);
            Add(nativeOpen, "host.dispatch-overhead", hostDispatchMs);
        }

        private static void Add(
            PlaybackQualityStartupStage stage,
            string name,
            double durationMs,
            string status = "measured",
            ulong packetCount = 0,
            ulong bytes = 0,
            string byteKind = "")
        {
            stage.Components.Add(new PlaybackQualityStartupComponent
            {
                Name = name,
                DurationMs = durationMs,
                Status = status,
                PacketCount = packetCount,
                Bytes = bytes,
                ByteKind = byteKind
            });
        }
    }
}
