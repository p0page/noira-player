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
            string message = "",
            string swapChainFormat = "",
            string swapChainColorSpace = "",
            bool isTenBitSwapChain = false,
            bool isVideoProcessorColorSpaceValidated = false,
            string videoProcessorInputColorSpace = "",
            string videoProcessorOutputColorSpace = "",
            string videoProcessorConversionStatus = "")
        {
            if (hdrStatus == HdrOutputStatus.Failed && string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Failed HDR status requires a message.", nameof(message));
            }

            HdrStatus = hdrStatus;
            IsHdrDisplayAvailable = isHdrDisplayAvailable;
            IsHdrOutputActive = isHdrOutputActive;
            Message = message ?? "";
            SwapChainFormat = swapChainFormat ?? "";
            SwapChainColorSpace = swapChainColorSpace ?? "";
            IsTenBitSwapChain = isTenBitSwapChain;
            IsVideoProcessorColorSpaceValidated = isVideoProcessorColorSpaceValidated;
            VideoProcessorInputColorSpace = videoProcessorInputColorSpace ?? "";
            VideoProcessorOutputColorSpace = videoProcessorOutputColorSpace ?? "";
            VideoProcessorConversionStatus = videoProcessorConversionStatus ?? "";
        }

        public HdrOutputStatus HdrStatus { get; }

        public bool IsHdrDisplayAvailable { get; }

        public bool IsHdrOutputActive { get; }

        public string Message { get; }

        public string SwapChainFormat { get; }

        public string SwapChainColorSpace { get; }

        public bool IsTenBitSwapChain { get; }

        public bool IsVideoProcessorColorSpaceValidated { get; }

        public string VideoProcessorInputColorSpace { get; }

        public string VideoProcessorOutputColorSpace { get; }

        public string VideoProcessorConversionStatus { get; }

        public bool RequiresExplicitToneMapping
        {
            get
            {
                return VideoProcessorConversionStatus.IndexOf(
                    "requires-tone-mapping",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public bool HasMissingToneMappingImplementation
        {
            get
            {
                return VideoProcessorConversionStatus.IndexOf(
                    "tone-mapping-missing",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public bool IsVideoProcessorColorPipelineComplete =>
            IsVideoProcessorColorSpaceValidated &&
            !RequiresExplicitToneMapping &&
            !HasMissingToneMappingImplementation;
    }
}
