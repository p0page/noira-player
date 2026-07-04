using System;

namespace NextGenEmby.Core.Playback
{
    public enum HdrOutputStatus
    {
        Unknown = 0,
        Unsupported = 1,
        Off = 2,
        On = 3,
        Failed = 4
    }

    public sealed class PlaybackDisplayStatus
    {
        public PlaybackDisplayStatus(
            HdrOutputStatus hdrStatus,
            bool isHdrDisplayAvailable,
            bool isHdrOutputActive,
            string message = "")
        {
            if (hdrStatus == HdrOutputStatus.Failed && string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Failed HDR status requires a message.", nameof(message));
            }

            HdrStatus = hdrStatus;
            IsHdrDisplayAvailable = isHdrDisplayAvailable;
            IsHdrOutputActive = isHdrOutputActive;
            Message = message ?? "";
        }

        public HdrOutputStatus HdrStatus { get; }

        public bool IsHdrDisplayAvailable { get; }

        public bool IsHdrOutputActive { get; }

        public string Message { get; }
    }
}
