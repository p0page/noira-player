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
                transportBytes: metrics.FfmpegOpenInputBytesRead,
                transportProvider: Provider(metrics.FfmpegOpenInputTransportCalls, metrics),
                transportCallEvidenceAvailable: EvidenceAvailable(metrics.FfmpegOpenInputTransportCalls, metrics),
                transportCalls: metrics.FfmpegOpenInputTransportCalls);
            Add(
                nativeOpen,
                "ffmpeg.find-stream-info",
                streamInfoMs,
                transportBytes: metrics.FfmpegStreamInfoBytesRead,
                transportProvider: Provider(metrics.FfmpegStreamInfoTransportCalls, metrics),
                transportCallEvidenceAvailable: EvidenceAvailable(metrics.FfmpegStreamInfoTransportCalls, metrics),
                transportCalls: metrics.FfmpegStreamInfoTransportCalls);
            Add(nativeOpen, "native.initialize-components", graphOtherMs);
            Add(
                nativeOpen,
                "native.startup-seek",
                startupSeekMs,
                transportBytes: metrics.NativeStartupSeekBytesRead,
                transportProvider: Provider(metrics.NativeStartupSeekTransportCalls, metrics),
                transportCallEvidenceAvailable: EvidenceAvailable(metrics.NativeStartupSeekTransportCalls, metrics),
                transportCalls: metrics.NativeStartupSeekTransportCalls);
            Add(
                nativeOpen,
                "native.first-frame.demux-read",
                firstFrameDemuxMs,
                packetCount: metrics.NativeFirstFrameDemuxPacketCount,
                transportBytes: metrics.NativeFirstFrameTransportBytesRead,
                packetPayloadBytes: metrics.NativeFirstFrameDemuxBytes,
                transportProvider: Provider(metrics.NativeFirstFrameTransportCalls, metrics),
                transportCallEvidenceAvailable: EvidenceAvailable(metrics.NativeFirstFrameTransportCalls, metrics),
                transportCalls: metrics.NativeFirstFrameTransportCalls);
            Add(nativeOpen, "native.first-frame.decode-control", firstFrameDecodeControlMs);
            Add(nativeOpen, "native.first-frame.present", firstFramePresentMs);
            Add(nativeOpen, "host.dispatch-overhead", hostDispatchMs);
        }

        private static string Provider(
            PlaybackQualityTransportCallSnapshot calls,
            PlaybackQualityMetricsSnapshot metrics) =>
            string.IsNullOrWhiteSpace(calls.Provider)
                ? metrics.StartupTransportProvider
                : calls.Provider;

        private static bool EvidenceAvailable(
            PlaybackQualityTransportCallSnapshot calls,
            PlaybackQualityMetricsSnapshot metrics) =>
            string.IsNullOrWhiteSpace(calls.Provider)
                ? metrics.StartupTransportCallEvidenceAvailable
                : calls.EvidenceAvailable;

        private static void Add(
            PlaybackQualityStartupStage stage,
            string name,
            double durationMs,
            string status = "measured",
            ulong packetCount = 0,
            ulong transportBytes = 0,
            ulong packetPayloadBytes = 0,
            string transportProvider = "",
            bool transportCallEvidenceAvailable = false,
            PlaybackQualityTransportCallSnapshot? transportCalls = null)
        {
            var hasTransportCallContract = transportCalls != null;
            stage.Components.Add(new PlaybackQualityStartupComponent
            {
                Name = name,
                DurationMs = durationMs,
                Status = status,
                PacketCount = packetCount,
                TransportBytes = transportBytes,
                PacketPayloadBytes = packetPayloadBytes,
                TransportProvider = hasTransportCallContract ? transportProvider : "",
                TransportCallEvidenceStatus = !hasTransportCallContract
                    ? "not-applicable"
                    : transportCallEvidenceAvailable ? "measured" : "unavailable",
                TransportReadCalls = transportCallEvidenceAvailable ? transportCalls?.ReadCalls : null,
                TransportSeekCalls = transportCallEvidenceAvailable ? transportCalls?.SeekCalls : null,
                TransportReadWaitMs = transportCallEvidenceAvailable ? transportCalls?.ReadWaitMs : null,
                TransportSeekWaitMs = transportCallEvidenceAvailable ? transportCalls?.SeekWaitMs : null,
                TransportSeekDistanceBytes = transportCallEvidenceAvailable ? transportCalls?.SeekDistanceBytes : null
            });
        }
    }
}
