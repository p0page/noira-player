using System;
using NoiraPlayer.Core.Diagnostics;
using NoiraPlayer.Core.PlaybackQuality;

namespace NoiraPlayer.App.Navigation
{
    internal sealed class PlaybackLaunchRequest
    {
#if DEBUG
        public static PlaybackLaunchRequest FromDevelopmentQualityRun(
            DevelopmentNavigationCommand command,
            DateTimeOffset commandReceivedAtUtc)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return new PlaybackLaunchRequest(
                command.ItemId,
                command.ItemName,
                command.StartPositionTicks,
                command.MediaSourceId,
                forceSdrOutput: command.ForceSdrOutput,
                qualityRunId: command.RunId,
                qualityScenario: command.Scenario,
                qualityRunDurationSeconds: command.DurationSeconds,
                qualityPauseSeconds: command.PauseSeconds,
                qualityExpected: command.Expected,
                qualityCommandReceivedAtUtc: commandReceivedAtUtc,
                streamUrl: command.StreamUrl);
        }
#endif

        public PlaybackLaunchRequest(
            string itemId,
            string itemName = "",
            long startPositionTicks = 0,
            string mediaSourceId = "",
            long runtimeTicks = 0,
            bool forceSdrOutput = false,
            string qualityRunId = "",
            string qualityScenario = PlaybackQualityExecutionScenario.Playback,
            int qualityRunDurationSeconds = 0,
            int qualityPauseSeconds = 0,
            PlaybackQualityExpected? qualityExpected = null,
            DateTimeOffset? qualityCommandReceivedAtUtc = null,
            string streamUrl = "")
        {
            ItemId = itemId ?? "";
            ItemName = itemName ?? "";
            StartPositionTicks = startPositionTicks < 0 ? 0 : startPositionTicks;
            MediaSourceId = mediaSourceId ?? "";
            RuntimeTicks = runtimeTicks < 0 ? 0 : runtimeTicks;
            ForceSdrOutput = forceSdrOutput;
            DirectStreamUrl = string.IsNullOrWhiteSpace(streamUrl) ? "" : streamUrl.Trim();
            QualityRunId = string.IsNullOrWhiteSpace(qualityRunId) ? "" : qualityRunId.Trim();
            QualityScenario = string.IsNullOrWhiteSpace(qualityScenario)
                ? PlaybackQualityExecutionScenario.Playback
                : qualityScenario.Trim().ToLowerInvariant();
            if (!PlaybackQualityExecutionScenario.IsKnown(QualityScenario))
            {
                throw new ArgumentException(
                    "Playback quality scenario is unknown.",
                    nameof(qualityScenario));
            }
            QualityRunDurationSeconds = qualityRunDurationSeconds < 10 ? 10 : qualityRunDurationSeconds;
            QualityPauseSeconds = Math.Max(0, Math.Min(900, qualityPauseSeconds));
            QualityExpected = qualityExpected;
            QualityCommandReceivedAtUtc = qualityCommandReceivedAtUtc ?? DateTimeOffset.UtcNow;
        }

        public string ItemId { get; }

        public string ItemName { get; }

        public long StartPositionTicks { get; }

        public string MediaSourceId { get; }

        public long RuntimeTicks { get; }

        public bool ForceSdrOutput { get; }

        public string DirectStreamUrl { get; }

        public string QualityRunId { get; }

        public string QualityScenario { get; }

        public int QualityRunDurationSeconds { get; }

        public int QualityPauseSeconds { get; }

        public PlaybackQualityExpected? QualityExpected { get; }

        public DateTimeOffset QualityCommandReceivedAtUtc { get; }

        public bool IsQualityRun => !string.IsNullOrWhiteSpace(QualityRunId);

        public bool HasDirectStreamUrl => !string.IsNullOrWhiteSpace(DirectStreamUrl);
    }
}
