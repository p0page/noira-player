using System.Collections.Generic;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityCodeTargetCatalog
    {
        private static readonly string[] UnsupportedSourceTargets =
        {
            "src/NoiraPlayer.Core/Playback/PlaybackOrchestrator.cs",
            "src/NoiraPlayer.Core/Playback/HdrPlaybackProfileClassifier.cs",
            "src/NoiraPlayer.Core/Emby"
        };

        private static readonly string[] ColorPipelineTargets =
        {
            "src/NoiraPlayer.Native/Media/DxgiColorSpaceMapper.cpp",
            "src/NoiraPlayer.Native/DxDeviceResources.cpp",
            "src/NoiraPlayer.Native/NativePlaybackEngine.cpp"
        };

        private static readonly string[] StartupTargets =
        {
            "src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportComposer.cs",
            "src/NoiraPlayer.Native/NativePlaybackEngine.cpp",
            "src/NoiraPlayer.Native/Media/PlaybackGraph.cpp"
        };

        private static readonly string[] ErrorHandlingTargets =
        {
            "src/NoiraPlayer.Core/Playback/PlaybackOrchestrator.cs",
            "src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityRuntimeEvidenceCollector.cs",
            "src/NoiraPlayer.Native/NativePlaybackEngine.cpp",
            "src/NoiraPlayer.Native/Media/PlaybackGraph.cpp",
            "src/NoiraPlayer.Native/Media/HttpMediaInput.cpp"
        };

        private static readonly string[] BufferingTargets =
        {
            "src/NoiraPlayer.Native/Media/PlaybackGraph.cpp",
            "src/NoiraPlayer.Native/Media/VideoDecoder.cpp",
            "src/NoiraPlayer.Native/Media/AudioDecoder.cpp"
        };

        private static readonly string[] TimelineTargets =
        {
            "src/NoiraPlayer.Core/Playback/PlaybackOrchestrator.cs",
            "src/NoiraPlayer.Core/Playback/SeekPreviewSession.cs",
            "src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportMapper.cs",
            "src/NoiraPlayer.Native/NativePlaybackEngine.cpp",
            "src/NoiraPlayer.Native/Media/PlaybackGraph.cpp"
        };

        private static readonly string[] AvSyncTargets =
        {
            "src/NoiraPlayer.Native/Media/AudioRenderer.cpp",
            "src/NoiraPlayer.Native/Media/PlaybackGraph.cpp",
            "src/NoiraPlayer.Native/Media/FramePacing.h"
        };

        private static readonly string[] FramePacingTargets =
        {
            "src/NoiraPlayer.Native/Media/FramePacing.h",
            "src/NoiraPlayer.Native/Media/PlaybackGraph.cpp",
            "src/NoiraPlayer.Native/HdrDisplayController.cpp",
            "src/NoiraPlayer.Core/PlaybackQuality/PlaybackRefreshRatePolicy.cs"
        };

        private static readonly string[] TrackTargets =
        {
            "src/NoiraPlayer.Native/Media/AudioDecoder.cpp",
            "src/NoiraPlayer.Native/Media/AudioRenderer.cpp",
            "src/NoiraPlayer.Native/Media/PlaybackGraph.cpp"
        };

        private static readonly string[] SubtitleTargets =
        {
            "src/NoiraPlayer.Native/Media/SubtitleDecoder.cpp",
            "src/NoiraPlayer.Native/Media/SubtitleRenderer.cpp",
            "src/NoiraPlayer.Native/Media/PlaybackGraph.cpp"
        };

        private static readonly string[] PlaybackLifecycleTargets =
        {
            "src/NoiraPlayer.Core/Playback/PlaybackOrchestrator.cs",
            "src/NoiraPlayer.Native/NativePlaybackEngine.cpp",
            "src/NoiraPlayer.Native/Media/PlaybackGraph.cpp"
        };

        private static readonly string[] EvidenceCollectionTargets =
        {
            "src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportMapper.cs",
            "src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportComposer.cs",
            "src/NoiraPlayer.Native/NativePlaybackQualityMetrics.cpp",
            "src/NoiraPlayer.Native/Media/PlaybackGraph.cpp"
        };

        private static readonly string[] UnknownTargets =
        {
            "src/NoiraPlayer.Core/PlaybackQuality",
            "src/NoiraPlayer.Native"
        };

        public static readonly string[] KnownFailureAreas =
        {
            "none",
            "unsupported-source",
            "color-pipeline",
            "startup",
            "error-handling",
            "buffering",
            "timeline",
            "av-sync",
            "frame-pacing",
            "evidence-collection",
            "metadata",
            "tracks",
            "subtitles",
            "playback-lifecycle",
            "reporting",
            "unknown"
        };

        public static bool IsKnownFailureArea(string failureArea)
        {
            if (string.IsNullOrWhiteSpace(failureArea))
            {
                return false;
            }

            foreach (var known in KnownFailureAreas)
            {
                if (string.Equals(
                    known,
                    failureArea,
                    System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyList<string> GetForFailureArea(string failureArea)
        {
            switch (failureArea)
            {
                case "unsupported-source":
                    return UnsupportedSourceTargets;
                case "color-pipeline":
                    return ColorPipelineTargets;
                case "startup":
                    return StartupTargets;
                case "error-handling":
                    return ErrorHandlingTargets;
                case "buffering":
                    return BufferingTargets;
                case "timeline":
                    return TimelineTargets;
                case "av-sync":
                    return AvSyncTargets;
                case "frame-pacing":
                    return FramePacingTargets;
                case "tracks":
                    return TrackTargets;
                case "subtitles":
                    return SubtitleTargets;
                case "playback-lifecycle":
                    return PlaybackLifecycleTargets;
                case "evidence-collection":
                    return EvidenceCollectionTargets;
                case "unknown":
                    return UnknownTargets;
                default:
                    return UnknownTargets;
            }
        }

        public static IReadOnlyList<string> GetForSignal(string signal)
        {
            return GetForFailureArea(GetFailureAreaForSignal(signal));
        }

        public static string GetFailureAreaForSignal(string signal)
        {
            if (StartsWithSignal(signal, "source."))
            {
                return "unsupported-source";
            }

            if (StartsWithSignal(signal, "error."))
            {
                return "error-handling";
            }

            if (StartsWithSignal(signal, "skip."))
            {
                return "evidence-collection";
            }

            if (StartsWithSignal(signal, "startup."))
            {
                return "startup";
            }

            if (StartsWithSignal(signal, "position."))
            {
                return "timeline";
            }

            if (string.Equals(
                    signal,
                    "tracks.subtitleCueRenderCount",
                    System.StringComparison.Ordinal))
            {
                return "subtitles";
            }

            if (StartsWithSignal(signal, "timing.") ||
                string.Equals(signal, "display.refreshRateHz", System.StringComparison.Ordinal))
            {
                return "frame-pacing";
            }

            if (StartsWithSignal(signal, "sync."))
            {
                return "av-sync";
            }

            if (StartsWithSignal(signal, "buffers."))
            {
                return "buffering";
            }

            if (StartsWithSignal(signal, "colorPipeline.") ||
                string.Equals(signal, "display.hdrStatus", System.StringComparison.Ordinal))
            {
                return "color-pipeline";
            }

            if (string.Equals(signal, "lifecycle.audio-switch", System.StringComparison.Ordinal))
            {
                return "tracks";
            }

            if (string.Equals(signal, "lifecycle.subtitle-switch", System.StringComparison.Ordinal) ||
                string.Equals(signal, "lifecycle.subtitle-off", System.StringComparison.Ordinal))
            {
                return "subtitles";
            }

            if (StartsWithSignal(signal, "lifecycle."))
            {
                return "playback-lifecycle";
            }

            return "evidence-collection";
        }

        public static void AddForFailureArea(List<string> codeTargets, string failureArea)
        {
            foreach (var target in GetForFailureArea(failureArea))
            {
                AddUnique(codeTargets, target);
            }
        }

        public static void AddForFailureAreas(List<string> codeTargets, IEnumerable<string> failureAreas)
        {
            foreach (var area in failureAreas)
            {
                AddForFailureArea(codeTargets, area);
            }
        }

        public static void AddForSignal(List<string> codeTargets, string signal)
        {
            foreach (var target in GetForSignal(signal))
            {
                AddUnique(codeTargets, target);
            }
        }

        public static void AddForSignals(List<string> codeTargets, IEnumerable<string> signals)
        {
            foreach (var signal in signals)
            {
                AddForSignal(codeTargets, signal);
            }
        }

        private static bool StartsWithSignal(string signal, string prefix)
        {
            return !string.IsNullOrWhiteSpace(signal) &&
                signal.StartsWith(prefix, System.StringComparison.Ordinal);
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }
    }
}
