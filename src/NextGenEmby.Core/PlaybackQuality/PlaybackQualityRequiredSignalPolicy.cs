using System;
using System.Collections.Generic;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityRequiredSignalPolicy
    {
        public static IReadOnlyList<string> CreateRequiredSignals(
            PlaybackQualityReferenceCase referenceCase)
        {
            var requiredSignals = new List<string>();
            if (referenceCase == null)
            {
                return requiredSignals;
            }

            var expected = referenceCase.Expected ?? new PlaybackQualityExpected();
            if (HasPurpose(referenceCase, "error-handling"))
            {
                AddUnique(requiredSignals, "error.code");
                AddUnique(requiredSignals, "error.message");
                AddUnique(requiredSignals, "error.failureClass");
                AddUnique(requiredSignals, "error.failureArea");
                return requiredSignals;
            }

            AddUnique(requiredSignals, "source.codec");
            AddUnique(requiredSignals, "source.width");
            AddUnique(requiredSignals, "source.height");
            AddUnique(requiredSignals, "source.frameRate");
            AddUnique(requiredSignals, "source.hdrKind");

            if (!string.IsNullOrWhiteSpace(expected.HdrPlaybackStrategy))
            {
                AddUnique(requiredSignals, "source.hdrPlaybackStrategy");
            }

            AddNullableSourceSignal(requiredSignals, expected.IsHdr, "source.isHdr");
            AddNullableSourceSignal(requiredSignals, expected.IsDirectPlayable, "source.isDirectPlayable");
            AddNullableSourceSignal(requiredSignals, expected.IsDolbyVision, "source.isDolbyVision");
            AddNullableSourceSignal(requiredSignals, expected.DolbyVisionProfile, "source.dolbyVisionProfile");
            AddNullableSourceSignal(requiredSignals, expected.DolbyVisionCompatibilityId, "source.dolbyVisionCompatibilityId");
            AddNullableSourceSignal(requiredSignals, expected.HasHdr10BaseLayer, "source.hasHdr10BaseLayer");
            AddNullableSourceSignal(requiredSignals, expected.HasHlgBaseLayer, "source.hasHlgBaseLayer");

            if (expected.MaxStartupDurationMs.HasValue)
            {
                AddUnique(requiredSignals, "startup.startupDurationMs");
            }

            if (expected.MaxSeekPositionErrorMs.HasValue ||
                HasPurpose(referenceCase, "timeline") ||
                HasPurpose(referenceCase, "seek"))
            {
                AddUnique(requiredSignals, "position.seekTargetPositionTicks");
                AddUnique(requiredSignals, "position.actualPositionTicks");
                AddUnique(requiredSignals, "position.seekPositionErrorMs");
            }

            if (HasPurpose(referenceCase, "tracks") ||
                HasPurpose(referenceCase, "subtitles") ||
                HasPurpose(referenceCase, "audio-switch") ||
                HasPurpose(referenceCase, "subtitle-switch"))
            {
                AddUnique(requiredSignals, "tracks.videoTrackCount");
                AddUnique(requiredSignals, "tracks.audioTrackCount");
                AddUnique(requiredSignals, "tracks.subtitleTrackCount");
            }

            if (HasPurpose(referenceCase, "subtitles") ||
                HasPurpose(referenceCase, "subtitle-switch") ||
                HasPurpose(referenceCase, "subtitle-off"))
            {
                AddUnique(requiredSignals, "tracks.isSubtitleDisabled");
            }

            if (HasPurpose(referenceCase, "audio-switch"))
            {
                AddUnique(requiredSignals, "tracks.selectedAudioStreamIndex");
            }

            if (HasPurpose(referenceCase, "subtitle-switch"))
            {
                AddUnique(requiredSignals, "tracks.selectedSubtitleStreamIndex");
            }

            if (expected.MinRenderedVideoFrames.HasValue ||
                HasPurpose(referenceCase, "frame-pacing") ||
                HasPurpose(referenceCase, "cadence-23.976"))
            {
                AddUnique(requiredSignals, "timing.renderedVideoFrames");
            }

            if (expected.MaxDroppedFrames.HasValue)
            {
                AddUnique(requiredSignals, "timing.droppedVideoFrames");
            }

            if (expected.MaxFrameGapMs.HasValue ||
                HasPurpose(referenceCase, "frame-pacing") ||
                HasPurpose(referenceCase, "cadence-23.976"))
            {
                AddUnique(requiredSignals, "timing.expectedFrameDurationMs");
                AddUnique(requiredSignals, "timing.maxFrameGapMs");
                AddUnique(requiredSignals, "timing.framePacingSourceFrameRate");
                AddUnique(requiredSignals, "timing.lateFrameDropToleranceMs");
            }

            if (expected.MaxRenderIntervalMsP95.HasValue ||
                HasPurpose(referenceCase, "frame-pacing"))
            {
                AddUnique(requiredSignals, "timing.renderIntervalMsP95");
            }

            if (expected.MaxRenderIntervalMsP99.HasValue ||
                HasPurpose(referenceCase, "frame-pacing"))
            {
                AddUnique(requiredSignals, "timing.renderIntervalMsP99");
            }

            if (expected.MaxAudioVideoDriftMsP95.HasValue ||
                HasPurpose(referenceCase, "av-sync"))
            {
                AddUnique(requiredSignals, "sync.audioVideoDriftMsP95");
            }

            if (expected.MaxVideoStarvedPasses.HasValue ||
                HasPurpose(referenceCase, "buffering"))
            {
                AddUnique(requiredSignals, "buffers.videoStarvedPasses");
            }

            if (expected.MaxAudioStarvedPasses.HasValue ||
                HasPurpose(referenceCase, "buffering"))
            {
                AddUnique(requiredSignals, "buffers.audioStarvedPasses");
            }

            if (!string.IsNullOrWhiteSpace(expected.HdrOutput))
            {
                if (!IsExpectedUnsupportedSource(expected))
                {
                    AddUnique(requiredSignals, "colorPipeline.actualHdrOutput");
                }
            }

            if (!IsExpectedUnsupportedSource(expected) &&
                PlaybackQualityColorExpectationPolicy.RequiresSurfaceEvidence(expected))
            {
                AddUnique(requiredSignals, "display.hdrStatus");
                AddUnique(requiredSignals, "colorPipeline.swapChainFormat");
                AddUnique(requiredSignals, "colorPipeline.swapChainColorSpace");
            }

            if (!IsExpectedUnsupportedSource(expected) &&
                PlaybackQualityColorExpectationPolicy.RequiresTenBitSwapChain(expected))
            {
                AddUnique(requiredSignals, "colorPipeline.isTenBitSwapChain");
            }

            if (!IsExpectedUnsupportedSource(expected) &&
                !string.IsNullOrWhiteSpace(expected.DxgiInput))
            {
                AddUnique(requiredSignals, "colorPipeline.dxgiInput");
            }

            if (!IsExpectedUnsupportedSource(expected) &&
                !string.IsNullOrWhiteSpace(expected.DxgiOutput))
            {
                AddUnique(requiredSignals, "colorPipeline.dxgiOutput");
            }

            if (!IsExpectedUnsupportedSource(expected) &&
                expected.RequireValidatedConversion)
            {
                AddUnique(requiredSignals, "colorPipeline.conversionStatus");
            }

            if (referenceCase.ForceSdrOutput || HasPurpose(referenceCase, "hdr-force-sdr"))
            {
                AddUnique(requiredSignals, "colorPipeline.forceSdrOutput");
            }

            if (expected.RequireMatchedDisplayRefreshRate ||
                HasPurpose(referenceCase, "cadence-23.976"))
            {
                AddUnique(requiredSignals, "display.refreshRateHz");
            }

            return requiredSignals;
        }

        private static bool IsExpectedUnsupportedSource(PlaybackQualityExpected expected)
        {
            return expected != null &&
                ((expected.IsDirectPlayable.HasValue && !expected.IsDirectPlayable.Value) ||
                string.Equals(
                    expected.HdrKind,
                    "DolbyVisionUnsupported",
                    StringComparison.Ordinal));
        }

        public static bool HasReportSignal(
            PlaybackQualityReport report,
            string signal)
        {
            return HasReportSignal(report, signal, presentSignals: null);
        }

        public static bool HasReportSignal(
            PlaybackQualityReport report,
            string signal,
            IReadOnlyCollection<string>? presentSignals)
        {
            if (report == null || string.IsNullOrWhiteSpace(signal))
            {
                return false;
            }

            if (presentSignals != null && !ContainsSignal(presentSignals, signal))
            {
                return false;
            }

            switch (signal)
            {
                case "error.code":
                    return !string.IsNullOrWhiteSpace(report.Error.Code);
                case "error.message":
                    return !string.IsNullOrWhiteSpace(report.Error.Message);
                case "error.operation":
                    return !string.IsNullOrWhiteSpace(report.Error.Operation);
                case "error.exceptionType":
                    return !string.IsNullOrWhiteSpace(report.Error.ExceptionType);
                case "error.failureClass":
                    return !string.IsNullOrWhiteSpace(report.Error.FailureClass);
                case "error.failureArea":
                    return !string.IsNullOrWhiteSpace(report.Error.FailureArea);
                case "error.isTerminal":
                case "error.isRetriable":
                    return !string.IsNullOrWhiteSpace(report.Error.Code);
                case "source.codec":
                    return !string.IsNullOrWhiteSpace(report.Source.Codec);
                case "source.width":
                    return report.Source.Width > 0;
                case "source.height":
                    return report.Source.Height > 0;
                case "source.frameRate":
                    return report.Source.FrameRate > 0;
                case "source.hdrKind":
                    return !string.IsNullOrWhiteSpace(report.Source.HdrKind);
                case "source.hdrPlaybackStrategy":
                    return !string.IsNullOrWhiteSpace(report.Source.HdrPlaybackStrategy);
                case "source.isHdr":
                case "source.isDirectPlayable":
                case "source.isDolbyVision":
                case "source.hasHdr10BaseLayer":
                case "source.hasHlgBaseLayer":
                    return true;
                case "source.dolbyVisionProfile":
                    return report.Source.DolbyVisionProfile.HasValue;
                case "source.dolbyVisionCompatibilityId":
                    return report.Source.DolbyVisionCompatibilityId.HasValue;
                case "startup.startupDurationMs":
                    return report.Startup.StartupDurationMs > 0;
                case "position.requestedStartPositionTicks":
                    return report.Position.RequestedStartPositionTicks.HasValue;
                case "position.seekTargetPositionTicks":
                    return report.Position.SeekTargetPositionTicks.HasValue;
                case "position.actualPositionTicks":
                    return report.Position.ActualPositionTicks.HasValue;
                case "position.seekPositionErrorMs":
                    return report.Position.SeekPositionErrorMs.HasValue ||
                        (report.Position.SeekTargetPositionTicks.HasValue &&
                            report.Position.ActualPositionTicks.HasValue);
                case "tracks.videoTrackCount":
                    if (presentSignals != null)
                    {
                        return true;
                    }

                    return report.Tracks.VideoTrackCount > 0 || report.Tracks.Video.Count > 0;
                case "tracks.audioTrackCount":
                    if (presentSignals != null)
                    {
                        return true;
                    }

                    return report.Tracks.AudioTrackCount > 0 || report.Tracks.Audio.Count > 0;
                case "tracks.subtitleTrackCount":
                    if (presentSignals != null)
                    {
                        return true;
                    }

                    return report.Tracks.SubtitleTrackCount > 0 || report.Tracks.Subtitles.Count > 0;
                case "tracks.selectedVideoStreamIndex":
                    return report.Tracks.SelectedVideoStreamIndex.HasValue;
                case "tracks.selectedAudioStreamIndex":
                    return report.Tracks.SelectedAudioStreamIndex.HasValue;
                case "tracks.selectedSubtitleStreamIndex":
                    return report.Tracks.SelectedSubtitleStreamIndex.HasValue;
                case "tracks.isSubtitleDisabled":
                    if (presentSignals != null)
                    {
                        return true;
                    }

                    return HasTrackEvidence(report);
                case "timing.renderedVideoFrames":
                    return report.Timing.RenderedVideoFrames > 0;
                case "timing.droppedVideoFrames":
                    if (presentSignals != null)
                    {
                        return true;
                    }

                    return HasTimingEvidence(report);
                case "timing.expectedFrameDurationMs":
                    return report.Timing.ExpectedFrameDurationMs > 0;
                case "timing.maxFrameGapMs":
                    return report.Timing.MaxFrameGapMs > 0;
                case "timing.framePacingSourceFrameRate":
                    return report.Timing.FramePacingSourceFrameRate > 0;
                case "timing.lateFrameDropToleranceMs":
                    return report.Timing.LateFrameDropToleranceMs > 0;
                case "timing.renderIntervalMsP95":
                    return report.Timing.RenderIntervalMsP95 > 0;
                case "timing.renderIntervalMsP99":
                    return report.Timing.RenderIntervalMsP99 > 0;
                case "sync.audioVideoDriftMsP95":
                    return presentSignals != null || report.Sync.AudioVideoDriftMsP95 > 0;
                case "buffers.videoStarvedPasses":
                case "buffers.audioStarvedPasses":
                    if (presentSignals != null)
                    {
                        return true;
                    }

                    return HasBufferEvidence(report);
                case "colorPipeline.actualHdrOutput":
                    return !string.IsNullOrWhiteSpace(report.ColorPipeline.ActualHdrOutput);
                case "colorPipeline.swapChainFormat":
                    return !string.IsNullOrWhiteSpace(report.ColorPipeline.SwapChainFormat);
                case "colorPipeline.swapChainColorSpace":
                    return !string.IsNullOrWhiteSpace(report.ColorPipeline.SwapChainColorSpace);
                case "colorPipeline.isTenBitSwapChain":
                    return presentSignals != null || report.ColorPipeline.IsTenBitSwapChain;
                case "colorPipeline.dxgiInput":
                    return !string.IsNullOrWhiteSpace(report.ColorPipeline.DxgiInput);
                case "colorPipeline.dxgiOutput":
                    return !string.IsNullOrWhiteSpace(report.ColorPipeline.DxgiOutput);
                case "colorPipeline.conversionStatus":
                    return !string.IsNullOrWhiteSpace(report.ColorPipeline.ConversionStatus);
                case "colorPipeline.forceSdrOutput":
                    return report.ColorPipeline.ForceSdrOutput;
                case "display.hdrStatus":
                    return !string.IsNullOrWhiteSpace(report.Display.HdrStatus);
                case "display.refreshRateHz":
                    return report.Display.RefreshRateHz > 0;
                default:
                    return false;
            }
        }

        private static bool ContainsSignal(
            IReadOnlyCollection<string> presentSignals,
            string signal)
        {
            foreach (var presentSignal in presentSignals)
            {
                if (string.Equals(presentSignal, signal, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddNullableSourceSignal<T>(
            List<string> requiredSignals,
            T? value,
            string signal)
            where T : struct
        {
            if (value.HasValue)
            {
                AddUnique(requiredSignals, signal);
            }
        }

        private static bool HasPurpose(
            PlaybackQualityReferenceCase referenceCase,
            string purpose)
        {
            return referenceCase.Purpose.Contains(purpose);
        }

        private static bool HasTimingEvidence(PlaybackQualityReport report)
        {
            return report.Timing.RenderPasses > 0 ||
                report.Timing.DecodedVideoFrames > 0 ||
                report.Timing.RenderedVideoFrames > 0 ||
                report.Timing.DroppedVideoFrames > 0;
        }

        private static bool HasTrackEvidence(PlaybackQualityReport report)
        {
            return report.Tracks.VideoTrackCount > 0 ||
                report.Tracks.AudioTrackCount > 0 ||
                report.Tracks.SubtitleTrackCount > 0 ||
                report.Tracks.Video.Count > 0 ||
                report.Tracks.Audio.Count > 0 ||
                report.Tracks.Subtitles.Count > 0 ||
                report.Tracks.SelectedVideoStreamIndex.HasValue ||
                report.Tracks.SelectedAudioStreamIndex.HasValue ||
                report.Tracks.SelectedSubtitleStreamIndex.HasValue;
        }

        private static bool HasBufferEvidence(PlaybackQualityReport report)
        {
            return report.Buffers.SubmittedAudioFrames > 0 ||
                report.Buffers.QueuedAudioBuffers > 0 ||
                report.Buffers.VideoStarvedPasses > 0 ||
                report.Buffers.AudioStarvedPasses > 0;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            foreach (var existing in values)
            {
                if (string.Equals(existing, value, StringComparison.Ordinal))
                {
                    return;
                }
            }

            values.Add(value);
        }
    }
}
