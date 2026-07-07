using System;
using System.IO;
using System.Linq;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityCapturedReportPath
    {
        public static string GetReportRelativePath(string runId)
        {
            var normalized = NormalizeRunId(runId);
            return normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : normalized + ".json";
        }

        private static string NormalizeRunId(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new ArgumentException("Playback quality runId is required.", nameof(runId));
            }

            var normalized = runId.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized.StartsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Playback quality runId must be a relative report-set key.", nameof(runId));
            }

            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var segments = normalized.Split('/');
            if (segments.Length == 0)
            {
                throw new ArgumentException("Playback quality runId is required.", nameof(runId));
            }

            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment) ||
                    string.Equals(segment, ".", StringComparison.Ordinal) ||
                    string.Equals(segment, "..", StringComparison.Ordinal) ||
                    segment.IndexOfAny(invalidFileNameChars) >= 0)
                {
                    throw new ArgumentException(
                        "Playback quality runId contains an unsafe path segment.",
                        nameof(runId));
                }
            }

            return string.Join("/", segments.Where(segment => segment.Length > 0));
        }
    }
}
