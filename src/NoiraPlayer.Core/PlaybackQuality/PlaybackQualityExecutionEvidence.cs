namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityEvidenceLevel
    {
        public const string Orchestration = "orchestration";
        public const string NativePlayback = "native-playback";
        public const string AppHosted = "app-hosted";

        public static bool IsKnown(string value)
        {
            return GetRank(value) >= 0;
        }

        public static bool MeetsMinimum(string actual, string minimum)
        {
            var actualRank = GetRank(actual);
            var minimumRank = GetRank(minimum);
            return actualRank >= 0 && minimumRank >= 0 && actualRank >= minimumRank;
        }

        private static int GetRank(string value)
        {
            return value switch
            {
                Orchestration => 0,
                NativePlayback => 1,
                AppHosted => 2,
                _ => -1
            };
        }
    }

    public static class PlaybackQualityExecutionStatus
    {
        public const string Completed = "completed";
        public const string Failed = "failed";
        public const string Unsupported = "unsupported";
        public const string Cancelled = "cancelled";
        public const string TimedOut = "timed-out";
        public const string Skipped = "skipped";

        public static bool IsKnown(string value)
        {
            return value == Completed ||
                value == Failed ||
                value == Unsupported ||
                value == Cancelled ||
                value == TimedOut ||
                value == Skipped;
        }
    }

    public static class PlaybackQualityExecutionScenario
    {
        public const string Playback = "playback";
        public const string Timeline = "timeline";
        public const string AudioSwitch = "audio-switch";
        public const string SubtitleSwitch = "subtitle-switch";
        public const string PauseResume = "pause-resume";
        public const string EndOfStream = "end-of-stream";

        public static bool IsKnown(string value)
        {
            return value == Playback ||
                value == Timeline ||
                value == AudioSwitch ||
                value == SubtitleSwitch ||
                value == PauseResume ||
                value == EndOfStream;
        }
    }

    public sealed class PlaybackQualityExecutionRequirement
    {
        public string MinimumEvidenceLevel { get; set; } =
            PlaybackQualityEvidenceLevel.NativePlayback;

        public string Scenario { get; set; } = "";
    }

    public sealed class PlaybackQualityExecutionEvidence
    {
        public string AttemptId { get; set; } = "";
        public string Runner { get; set; } = "";
        public string Scenario { get; set; } = "";
        public string EvidenceLevel { get; set; } = "";
        public string Status { get; set; } = "";
        public string SourceLocatorHash { get; set; } = "";
        public string OpenedSourceHash { get; set; } = "";
        public string OpenedSourceHashKind { get; set; } = "";
        public string StartedAtUtc { get; set; } = "";
        public double DurationMs { get; set; }
        public bool SourceOpenAttempted { get; set; }
        public bool SourceOpened { get; set; }
        public bool NativeGraphOpened { get; set; }
        public bool DemuxStarted { get; set; }
        public bool DecoderOpened { get; set; }
        public bool PlaybackSampleObserved { get; set; }
    }

    public static class PlaybackQualityExecutionEvidenceFactory
    {
        public static PlaybackQualityExecutionEvidence Clone(
            PlaybackQualityExecutionEvidence? source)
        {
            if (source == null)
            {
                return new PlaybackQualityExecutionEvidence();
            }

            return new PlaybackQualityExecutionEvidence
            {
                AttemptId = source.AttemptId,
                Runner = source.Runner,
                Scenario = source.Scenario,
                EvidenceLevel = source.EvidenceLevel,
                Status = source.Status,
                SourceLocatorHash = source.SourceLocatorHash,
                OpenedSourceHash = source.OpenedSourceHash,
                OpenedSourceHashKind = source.OpenedSourceHashKind,
                StartedAtUtc = source.StartedAtUtc,
                DurationMs = source.DurationMs,
                SourceOpenAttempted = source.SourceOpenAttempted,
                SourceOpened = source.SourceOpened,
                NativeGraphOpened = source.NativeGraphOpened,
                DemuxStarted = source.DemuxStarted,
                DecoderOpened = source.DecoderOpened,
                PlaybackSampleObserved = source.PlaybackSampleObserved
            };
        }
    }

    public sealed class PlaybackQualityExecutionCoverage
    {
        public int DeclaredCaseCount { get; set; }
        public int AttemptedCaseCount { get; set; }
        public int OpenedCaseCount { get; set; }
        public int DecodedCaseCount { get; set; }
        public int RenderedCaseCount { get; set; }
        public int CompletedCaseCount { get; set; }
        public int FailedCaseCount { get; set; }
        public int UnsupportedCaseCount { get; set; }
        public int SkippedCaseCount { get; set; }
        public int MissingCaseCount { get; set; }
        public int QuarantineMissingCaseCount { get; set; }
    }
}
