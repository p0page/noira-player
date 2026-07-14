using System;
using System.Collections.Generic;
using System.Linq;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public sealed class PlaybackQualityRenderPhaseCaseSummary
    {
        public string CaseId { get; set; } = "";
        public string Status { get; set; } = "insufficient-evidence";
        public int ExpectedRepeatCount { get; set; }
        public int ObservedRepeatCount { get; set; }
        public int ComparableRepeatCount { get; set; }
        public int InsufficientEvidenceRepeatCount { get; set; }
        public List<PlaybackQualityRenderPhaseSignalSummary> Signals { get; } =
            new List<PlaybackQualityRenderPhaseSignalSummary>();
    }

    public sealed class PlaybackQualityRenderPhaseSignalSummary
    {
        public string Signal { get; set; } = "";
        public string Unit { get; set; } = "ms";
        public bool LowerIsBetter { get; set; } = true;
        public string DirectionConsistency { get; set; } = "insufficient-evidence";
        public int ExpectedRepeatCount { get; set; }
        public int ComparableRepeatCount { get; set; }
        public int LowerCount { get; set; }
        public int HigherCount { get; set; }
        public int UnchangedCount { get; set; }
        public PlaybackQualityRenderPhaseDistribution Baseline { get; set; } =
            new PlaybackQualityRenderPhaseDistribution();
        public PlaybackQualityRenderPhaseDistribution Candidate { get; set; } =
            new PlaybackQualityRenderPhaseDistribution();
        public PlaybackQualityRenderPhaseDistribution AbsoluteDelta { get; set; } =
            new PlaybackQualityRenderPhaseDistribution();
    }

    public sealed class PlaybackQualityRenderPhaseDistribution
    {
        public double Minimum { get; set; }
        public double Median { get; set; }
        public double Maximum { get; set; }
    }

    public static class PlaybackQualityRenderPhaseSummaryAggregator
    {
        public static List<PlaybackQualityRenderPhaseCaseSummary> Summarize(
            IEnumerable<PlaybackQualityRenderPhaseComparison> comparisons,
            IEnumerable<string> caseIds,
            int expectedRepeatCount)
        {
            if (comparisons == null)
            {
                throw new ArgumentNullException(nameof(comparisons));
            }

            if (caseIds == null)
            {
                throw new ArgumentNullException(nameof(caseIds));
            }

            if (expectedRepeatCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedRepeatCount));
            }

            var comparisonList = comparisons.Where(item => item != null).ToList();
            var summaries = new List<PlaybackQualityRenderPhaseCaseSummary>();
            foreach (var caseId in caseIds)
            {
                var caseComparisons = comparisonList
                    .Where(item => string.Equals(item.CaseId, caseId, StringComparison.Ordinal))
                    .OrderBy(item => item.RepeatIndex)
                    .ToList();
                var comparable = caseComparisons
                    .Where(item => string.Equals(item.Status, "comparable", StringComparison.Ordinal))
                    .ToList();
                var summary = new PlaybackQualityRenderPhaseCaseSummary
                {
                    CaseId = caseId ?? "",
                    ExpectedRepeatCount = expectedRepeatCount,
                    ObservedRepeatCount = caseComparisons.Count,
                    ComparableRepeatCount = comparable.Count,
                    InsufficientEvidenceRepeatCount = caseComparisons.Count - comparable.Count,
                    Status = caseComparisons.Count == expectedRepeatCount &&
                        comparable.Count == expectedRepeatCount
                            ? "complete"
                            : "insufficient-evidence"
                };

                foreach (var signal in GetSignalOrder(comparable))
                {
                    summary.Signals.Add(SummarizeSignal(
                        comparable,
                        signal,
                        expectedRepeatCount));
                }

                summaries.Add(summary);
            }

            return summaries;
        }

        private static List<string> GetSignalOrder(
            List<PlaybackQualityRenderPhaseComparison> comparisons)
        {
            var signals = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var comparison in comparisons)
            {
                foreach (var metric in comparison.Metrics)
                {
                    if (seen.Add(metric.Signal))
                    {
                        signals.Add(metric.Signal);
                    }
                }
            }

            return signals;
        }

        private static PlaybackQualityRenderPhaseSignalSummary SummarizeSignal(
            List<PlaybackQualityRenderPhaseComparison> comparisons,
            string signal,
            int expectedRepeatCount)
        {
            var metrics = comparisons
                .SelectMany(item => item.Metrics)
                .Where(metric => string.Equals(metric.Signal, signal, StringComparison.Ordinal))
                .ToList();
            var first = metrics.Count > 0
                ? metrics[0]
                : new PlaybackQualityRenderPhaseMetricDelta();
            var summary = new PlaybackQualityRenderPhaseSignalSummary
            {
                Signal = signal,
                Unit = first.Unit,
                LowerIsBetter = first.LowerIsBetter,
                ExpectedRepeatCount = expectedRepeatCount,
                ComparableRepeatCount = metrics.Count,
                LowerCount = metrics.Count(metric => metric.Direction == "lower"),
                HigherCount = metrics.Count(metric => metric.Direction == "higher"),
                UnchangedCount = metrics.Count(metric => metric.Direction == "unchanged"),
                Baseline = Distribution(metrics.Select(metric => metric.Baseline)),
                Candidate = Distribution(metrics.Select(metric => metric.Candidate)),
                AbsoluteDelta = Distribution(metrics.Select(metric => metric.AbsoluteDelta))
            };
            summary.DirectionConsistency = DetermineDirectionConsistency(
                summary,
                expectedRepeatCount);
            return summary;
        }

        private static string DetermineDirectionConsistency(
            PlaybackQualityRenderPhaseSignalSummary summary,
            int expectedRepeatCount)
        {
            if (summary.ComparableRepeatCount != expectedRepeatCount)
            {
                return "insufficient-evidence";
            }

            if (summary.LowerCount == expectedRepeatCount)
            {
                return "consistent-lower";
            }

            if (summary.HigherCount == expectedRepeatCount)
            {
                return "consistent-higher";
            }

            if (summary.UnchangedCount == expectedRepeatCount)
            {
                return "consistent-unchanged";
            }

            return "mixed";
        }

        private static PlaybackQualityRenderPhaseDistribution Distribution(
            IEnumerable<double> values)
        {
            var ordered = values.OrderBy(value => value).ToList();
            if (ordered.Count == 0)
            {
                return new PlaybackQualityRenderPhaseDistribution();
            }

            var middle = ordered.Count / 2;
            var median = ordered.Count % 2 == 0
                ? (ordered[middle - 1] + ordered[middle]) / 2.0
                : ordered[middle];
            return new PlaybackQualityRenderPhaseDistribution
            {
                Minimum = ordered[0],
                Median = median,
                Maximum = ordered[ordered.Count - 1]
            };
        }
    }
}
