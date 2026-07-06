using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityReportMapper
    {
        public static void ApplyDisplayStatus(
            PlaybackQualityReport report,
            PlaybackDisplayStatus status)
        {
            report.Display.HdrStatus = status.HdrStatus.ToString();
            report.Display.IsHdrDisplayAvailable = status.IsHdrDisplayAvailable;
            report.Display.IsHdrOutputActive = status.IsHdrOutputActive;
            report.Display.RefreshRateHz = status.RefreshRateHz;
            report.Display.Message = status.Message;

            report.ColorPipeline.ActualHdrOutput = MapActualHdrOutput(status);
            report.ColorPipeline.SwapChainFormat = status.SwapChainFormat;
            report.ColorPipeline.SwapChainColorSpace = status.SwapChainColorSpace;
            report.ColorPipeline.IsTenBitSwapChain = status.IsTenBitSwapChain;
            report.ColorPipeline.IsVideoProcessorColorSpaceValidated =
                status.IsVideoProcessorColorSpaceValidated;
            report.ColorPipeline.DxgiInput = status.VideoProcessorInputColorSpace;
            report.ColorPipeline.DxgiOutput = status.VideoProcessorOutputColorSpace;
            report.ColorPipeline.ConversionStatus = status.VideoProcessorConversionStatus;
        }

        private static string MapActualHdrOutput(PlaybackDisplayStatus status)
        {
            switch (status.HdrStatus)
            {
                case HdrOutputStatus.On:
                    return "Hdr10";
                case HdrOutputStatus.Off:
                    return "Sdr";
                case HdrOutputStatus.Unsupported:
                    return "Unsupported";
                case HdrOutputStatus.Failed:
                    return "Failed";
                default:
                    return "Unknown";
            }
        }
    }
}
