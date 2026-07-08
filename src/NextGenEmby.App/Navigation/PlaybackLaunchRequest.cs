using System;
using NextGenEmby.Core.PlaybackQuality;

namespace NextGenEmby.App.Navigation
{
    internal sealed class PlaybackLaunchRequest
    {
        public PlaybackLaunchRequest(
            string itemId,
            string itemName = "",
            long startPositionTicks = 0,
            string mediaSourceId = "",
            long runtimeTicks = 0,
            bool forceSdrOutput = false,
            string qualityRunId = "",
            int qualityRunDurationSeconds = 0,
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
            QualityRunDurationSeconds = qualityRunDurationSeconds < 10 ? 10 : qualityRunDurationSeconds;
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

        public int QualityRunDurationSeconds { get; }

        public PlaybackQualityExpected? QualityExpected { get; }

        public DateTimeOffset QualityCommandReceivedAtUtc { get; }

        public bool IsQualityRun => !string.IsNullOrWhiteSpace(QualityRunId);

        public bool HasDirectStreamUrl => !string.IsNullOrWhiteSpace(DirectStreamUrl);
    }
}
