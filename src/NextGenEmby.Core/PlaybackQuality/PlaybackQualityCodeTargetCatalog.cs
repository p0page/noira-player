using System.Collections.Generic;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityCodeTargetCatalog
    {
        private static readonly string[] UnsupportedSourceTargets =
        {
            "src/NextGenEmby.Core/Playback/PlaybackOrchestrator.cs",
            "src/NextGenEmby.Core/Playback/HdrPlaybackProfileClassifier.cs",
            "src/NextGenEmby.Core/Emby"
        };

        private static readonly string[] ColorPipelineTargets =
        {
            "src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.cpp",
            "src/NextGenEmby.Native/DxDeviceResources.cpp",
            "src/NextGenEmby.Native/NativePlaybackEngine.cpp"
        };

        private static readonly string[] StartupTargets =
        {
            "src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportComposer.cs",
            "src/NextGenEmby.Native/NativePlaybackEngine.cpp",
            "src/NextGenEmby.Native/Media/PlaybackGraph.cpp"
        };

        private static readonly string[] BufferingTargets =
        {
            "src/NextGenEmby.Native/Media/PlaybackGraph.cpp",
            "src/NextGenEmby.Native/Media/VideoDecoder.cpp",
            "src/NextGenEmby.Native/Media/AudioDecoder.cpp"
        };

        private static readonly string[] AvSyncTargets =
        {
            "src/NextGenEmby.Native/Media/AudioRenderer.cpp",
            "src/NextGenEmby.Native/Media/PlaybackGraph.cpp",
            "src/NextGenEmby.Native/Media/FramePacing.h"
        };

        private static readonly string[] FramePacingTargets =
        {
            "src/NextGenEmby.Native/Media/FramePacing.h",
            "src/NextGenEmby.Native/Media/PlaybackGraph.cpp",
            "src/NextGenEmby.Native/HdrDisplayController.cpp",
            "src/NextGenEmby.Core/PlaybackQuality/PlaybackRefreshRatePolicy.cs"
        };

        private static readonly string[] EvidenceCollectionTargets =
        {
            "src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportMapper.cs",
            "src/NextGenEmby.Core/PlaybackQuality/PlaybackQualityReportComposer.cs",
            "src/NextGenEmby.Native/NativePlaybackQualityMetrics.cpp",
            "src/NextGenEmby.Native/Media/PlaybackGraph.cpp"
        };

        private static readonly string[] UnknownTargets =
        {
            "src/NextGenEmby.Core/PlaybackQuality",
            "src/NextGenEmby.Native"
        };

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
                case "buffering":
                    return BufferingTargets;
                case "av-sync":
                    return AvSyncTargets;
                case "frame-pacing":
                    return FramePacingTargets;
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

            if (StartsWithSignal(signal, "startup."))
            {
                return "startup";
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
