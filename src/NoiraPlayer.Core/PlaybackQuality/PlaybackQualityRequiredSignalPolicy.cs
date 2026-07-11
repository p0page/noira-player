using System;
using System.Collections.Generic;

namespace NoiraPlayer.Core.PlaybackQuality
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
                AddUnique(requiredSignals, "lifecycle.error");
                return requiredSignals;
            }

            AddUnique(requiredSignals, "source.codec");
            AddUnique(requiredSignals, "source.hasDirectStreamUrl");
            AddUnique(requiredSignals, "source.directStreamProtocol");
            AddUnique(requiredSignals, "source.width");
            AddUnique(requiredSignals, "source.height");
            AddUnique(requiredSignals, "source.frameRate");
            AddUnique(requiredSignals, "source.hdrKind");

            AddExpectedSourceStringSignal(requiredSignals, expected.VideoRange, "source.videoRange");
            AddExpectedSourceStringSignal(requiredSignals, expected.ColorPrimaries, "source.colorPrimaries");
            AddExpectedSourceStringSignal(requiredSignals, expected.ColorTransfer, "source.colorTransfer");
            AddExpectedSourceStringSignal(requiredSignals, expected.ColorSpace, "source.colorSpace");

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

            if (IsExpectedUnsupportedSource(expected))
            {
                return requiredSignals;
            }

            AddUnique(requiredSignals, "runtimeMetrics.status");
            AddUnique(requiredSignals, "runtimeMetrics.providerStatus");
            AddUnique(requiredSignals, "runtimeMetrics.hasSnapshot");
            AddUnique(requiredSignals, "runtimeMetrics.hasPlaybackSample");

            AddUnique(requiredSignals, "lifecycle.load");
            AddUnique(requiredSignals, "lifecycle.play");
            AddUnique(requiredSignals, "lifecycle.pause");
            AddUnique(requiredSignals, "lifecycle.resume");
            AddUnique(requiredSignals, "lifecycle.stop");

            if (HasPurpose(referenceCase, "end-of-stream"))
            {
                AddUnique(requiredSignals, "lifecycle.endOfStream");
            }

            if (expected.MaxStartupDurationMs.HasValue)
            {
                AddUnique(requiredSignals, "startup.startupDurationMs");
            }

            if (expected.MaxSeekPositionErrorMs.HasValue ||
                HasPurpose(referenceCase, "timeline") ||
                HasPurpose(referenceCase, "seek"))
            {
                AddUnique(requiredSignals, "lifecycle.seek");
                AddUnique(requiredSignals, "source.durationTicks");
                AddUnique(requiredSignals, "source.containerStartTimeTicks");
                AddUnique(requiredSignals, "source.videoStreamStartTimeTicks");
                AddUnique(requiredSignals, "position.seekTargetPositionTicks");
                AddUnique(requiredSignals, "position.seekDemuxTargetTicks");
                AddUnique(requiredSignals, "position.actualPositionTicks");
                AddUnique(requiredSignals, "position.firstPresentedPositionTicks");
                AddUnique(requiredSignals, "position.postSeekPositionTicks");
                AddUnique(requiredSignals, "position.postSeekAdvanced");
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
                AddUnique(requiredSignals, "tracks.video.isExternal");
                AddUnique(requiredSignals, "tracks.video.isDefault");
                AddUnique(requiredSignals, "tracks.video.isForced");
                AddUnique(requiredSignals, "tracks.audio.isExternal");
                AddUnique(requiredSignals, "tracks.audio.channels");
                AddUnique(requiredSignals, "tracks.audio.isDefault");
                AddUnique(requiredSignals, "tracks.audio.isForced");
            }

            if (HasPurpose(referenceCase, "subtitles") ||
                HasPurpose(referenceCase, "subtitle-switch") ||
                HasPurpose(referenceCase, "subtitle-off"))
            {
                AddUnique(requiredSignals, "tracks.isSubtitleDisabled");
                AddUnique(requiredSignals, "tracks.subtitles.isExternal");
                AddUnique(requiredSignals, "tracks.subtitles.isDefault");
                AddUnique(requiredSignals, "tracks.subtitles.isForced");
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

        public static bool RequiresNativePlaybackEvidence(string signal)
        {
            switch (signal)
            {
                case "source.durationTicks":
                case "source.containerStartTimeTicks":
                case "source.videoStreamStartTimeTicks":
                case "position.seekDemuxTargetTicks":
                case "position.firstPresentedPositionTicks":
                case "position.postSeekPositionTicks":
                case "position.postSeekAdvanced":
                    return true;
                default:
                    return false;
            }
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
                case "skip.code":
                    return !string.IsNullOrWhiteSpace(report.Skip.Code);
                case "skip.reason":
                    return !string.IsNullOrWhiteSpace(report.Skip.Reason);
                case "skip.operation":
                    return !string.IsNullOrWhiteSpace(report.Skip.Operation);
                case "skip.failureClass":
                    return !string.IsNullOrWhiteSpace(report.Skip.FailureClass);
                case "skip.failureArea":
                    return !string.IsNullOrWhiteSpace(report.Skip.FailureArea);
                case "skip.isExpected":
                case "skip.isRetriable":
                    return !string.IsNullOrWhiteSpace(report.Skip.Code);
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
                case "runtimeMetrics.status":
                    return !string.IsNullOrWhiteSpace(report.RuntimeMetrics.Status) &&
                        report.RuntimeMetrics.Status != "unknown";
                case "runtimeMetrics.providerStatus":
                    return !string.IsNullOrWhiteSpace(report.RuntimeMetrics.ProviderStatus) &&
                        report.RuntimeMetrics.ProviderStatus != "unknown";
                case "runtimeMetrics.reason":
                    return !string.IsNullOrWhiteSpace(report.RuntimeMetrics.Reason);
                case "runtimeMetrics.hasSnapshot":
                case "runtimeMetrics.hasPlaybackSample":
                    return !string.IsNullOrWhiteSpace(report.RuntimeMetrics.Status) &&
                        report.RuntimeMetrics.Status != "unknown";
                case "runtimeMetrics.processWallClockMs":
                    return report.RuntimeMetrics.ProcessWallClockMs > 0;
                case "runtimeMetrics.processCpuTimeMs":
                    return report.RuntimeMetrics.ProcessCpuTimeMs > 0;
                case "runtimeMetrics.processCpuUtilizationRatio":
                    return report.RuntimeMetrics.ProcessCpuUtilizationRatio > 0;
                case "source.codec":
                    return !string.IsNullOrWhiteSpace(report.Source.Codec);
                case "source.hasDirectStreamUrl":
                    return report.Source.HasDirectStreamUrl;
                case "source.directStreamProtocol":
                    return !string.IsNullOrWhiteSpace(report.Source.DirectStreamProtocol);
                case "source.container":
                    return !string.IsNullOrWhiteSpace(report.Source.Container);
                case "source.bitrate":
                    return report.Source.Bitrate > 0;
                case "source.durationTicks":
                    return report.Source.DurationTicks > 0;
                case "source.containerStartTimeTicks":
                    return report.Source.ContainerStartTimeTicks.HasValue;
                case "source.videoStreamStartTimeTicks":
                    return report.Source.VideoStreamStartTimeTicks.HasValue;
                case "source.hasChapterMetadata":
                    return presentSignals != null ||
                        report.Source.HasChapterMetadata ||
                        report.Source.ChapterCount.HasValue ||
                        report.Source.Chapters.Count > 0;
                case "source.chapterCount":
                    return presentSignals != null ||
                        report.Source.HasChapterMetadata ||
                        report.Source.ChapterCount.HasValue ||
                        report.Source.Chapters.Count > 0;
                case "source.chapters.name":
                    return presentSignals != null || HasChapterTextEvidence(report, chapter => chapter.Name);
                case "source.chapters.startPositionTicks":
                    return presentSignals != null || report.Source.Chapters.Count > 0;
                case "source.chapters.imageTag":
                    return presentSignals != null || HasChapterTextEvidence(report, chapter => chapter.ImageTag);
                case "source.width":
                    return report.Source.Width > 0;
                case "source.height":
                    return report.Source.Height > 0;
                case "source.frameRate":
                    return report.Source.FrameRate > 0;
                case "source.hdrKind":
                    return !string.IsNullOrWhiteSpace(report.Source.HdrKind);
                case "source.videoRange":
                    return !string.IsNullOrWhiteSpace(report.Source.VideoRange);
                case "source.colorPrimaries":
                    return !string.IsNullOrWhiteSpace(report.Source.ColorPrimaries);
                case "source.colorTransfer":
                    return !string.IsNullOrWhiteSpace(report.Source.ColorTransfer);
                case "source.colorSpace":
                    return !string.IsNullOrWhiteSpace(report.Source.ColorSpace);
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
                case "lifecycle.load":
                    return HasLifecycleOperation(report, "load");
                case "lifecycle.play":
                    return HasLifecycleOperation(report, "play");
                case "lifecycle.pause":
                    return HasLifecycleOperation(report, "pause");
                case "lifecycle.resume":
                    return HasLifecycleOperation(report, "resume");
                case "lifecycle.seek":
                    return HasLifecycleOperation(report, "seek");
                case "lifecycle.stop":
                    return HasLifecycleOperation(report, "stop");
                case "lifecycle.endOfStream":
                    return HasLifecycleOperation(report, "endOfStream");
                case "lifecycle.audio-switch":
                    return HasLifecycleOperation(report, "audio-switch");
                case "lifecycle.subtitle-switch":
                    return HasLifecycleOperation(report, "subtitle-switch");
                case "lifecycle.subtitle-off":
                    return HasLifecycleOperation(report, "subtitle-off");
                case "lifecycle.error":
                    return HasLifecycleOperation(report, "error") ||
                        HasLifecycleStatus(report, "error") ||
                        HasLifecycleStatus(report, "failed");
                case "lifecycle.skip":
                    return HasLifecycleOperation(report, "skip") ||
                        HasLifecycleStatus(report, "skipped");
                case "position.requestedStartPositionTicks":
                    return report.Position.RequestedStartPositionTicks.HasValue;
                case "position.seekTargetPositionTicks":
                    return report.Position.SeekTargetPositionTicks.HasValue;
                case "position.seekDemuxTargetTicks":
                    return report.Position.SeekDemuxTargetTicks.HasValue;
                case "position.actualPositionTicks":
                    return report.Position.ActualPositionTicks.HasValue;
                case "position.firstPresentedPositionTicks":
                    return report.Position.FirstPresentedPositionTicks.HasValue;
                case "position.postSeekPositionTicks":
                    return report.Position.PostSeekPositionTicks.HasValue;
                case "position.postSeekAdvanced":
                    return report.Position.PostSeekAdvanced.HasValue;
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
                case "tracks.video.isExternal":
                    return report.Tracks.Video.Count > 0;
                case "tracks.video.isDefault":
                    return HasTrackFlagEvidence(report.Tracks.Video, track => track.IsDefault);
                case "tracks.video.isForced":
                    return HasTrackFlagEvidence(report.Tracks.Video, track => track.IsForced);
                case "tracks.audio.isExternal":
                    return report.Tracks.Audio.Count > 0;
                case "tracks.audio.channels":
                    return HasAudioChannelEvidence(report);
                case "tracks.audio.isDefault":
                    return HasTrackFlagEvidence(report.Tracks.Audio, track => track.IsDefault);
                case "tracks.audio.isForced":
                    return HasTrackFlagEvidence(report.Tracks.Audio, track => track.IsForced);
                case "tracks.subtitles.isExternal":
                    return report.Tracks.Subtitles.Count > 0;
                case "tracks.subtitles.isDefault":
                    return HasTrackFlagEvidence(report.Tracks.Subtitles, track => track.IsDefault);
                case "tracks.subtitles.isForced":
                    return HasTrackFlagEvidence(report.Tracks.Subtitles, track => track.IsForced);
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
                case "timing.audioAheadWaitFinalDeltaAbsMsP50":
                    return presentSignals != null || report.Timing.AudioAheadWaitFinalDeltaAbsMsP50 > 0;
                case "timing.audioAheadWaitFinalDeltaAbsMsP95":
                    return presentSignals != null || report.Timing.AudioAheadWaitFinalDeltaAbsMsP95 > 0;
                case "timing.audioAheadWaitFinalDeltaAbsMsP99":
                    return presentSignals != null || report.Timing.AudioAheadWaitFinalDeltaAbsMsP99 > 0;
                case "timing.audioAheadWaitFinalDeltaAbsMsMax":
                    return presentSignals != null || report.Timing.AudioAheadWaitFinalDeltaAbsMsMax > 0;
                case "timing.audioAheadWaitPassDurationMsP50":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassDurationMsP50 > 0;
                case "timing.audioAheadWaitPassDurationMsP95":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassDurationMsP95 > 0;
                case "timing.audioAheadWaitPassDurationMsP99":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassDurationMsP99 > 0;
                case "timing.audioAheadWaitPassDurationMsMax":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassDurationMsMax > 0;
                case "timing.audioAheadWaitPassTargetMsP50":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassTargetMsP50 > 0;
                case "timing.audioAheadWaitPassTargetMsP95":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassTargetMsP95 > 0;
                case "timing.audioAheadWaitPassTargetMsP99":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassTargetMsP99 > 0;
                case "timing.audioAheadWaitPassTargetMsMax":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassTargetMsMax > 0;
                case "timing.audioAheadWaitPassOversleepMsP50":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassOversleepMsP50 > 0;
                case "timing.audioAheadWaitPassOversleepMsP95":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassOversleepMsP95 > 0;
                case "timing.audioAheadWaitPassOversleepMsP99":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassOversleepMsP99 > 0;
                case "timing.audioAheadWaitPassOversleepMsMax":
                    return presentSignals != null || report.Timing.AudioAheadWaitPassOversleepMsMax > 0;
                case "timing.renderIntervalAfterAudioAheadWaitSampleCount":
                    return presentSignals != null ||
                        report.Timing.RenderIntervalAfterAudioAheadWaitSampleCount > 0 ||
                        report.Timing.RenderIntervalAfterNonAudioWaitSampleCount > 0;
                case "timing.renderIntervalAfterAudioAheadWaitMsP95":
                    return presentSignals != null || report.Timing.RenderIntervalAfterAudioAheadWaitMsP95 > 0;
                case "timing.renderIntervalAfterAudioAheadWaitMsP99":
                    return presentSignals != null || report.Timing.RenderIntervalAfterAudioAheadWaitMsP99 > 0;
                case "timing.renderIntervalAfterAudioAheadWaitMsMax":
                    return presentSignals != null || report.Timing.RenderIntervalAfterAudioAheadWaitMsMax > 0;
                case "timing.audioAheadWaitEndToPresentSampleCount":
                    return presentSignals != null || report.Timing.AudioAheadWaitEndToPresentSampleCount > 0;
                case "timing.audioAheadWaitEndToPresentMsP50":
                    return presentSignals != null || report.Timing.AudioAheadWaitEndToPresentMsP50 > 0;
                case "timing.audioAheadWaitEndToPresentMsP95":
                    return presentSignals != null || report.Timing.AudioAheadWaitEndToPresentMsP95 > 0;
                case "timing.audioAheadWaitEndToPresentMsP99":
                    return presentSignals != null || report.Timing.AudioAheadWaitEndToPresentMsP99 > 0;
                case "timing.audioAheadWaitEndToPresentMsMax":
                    return presentSignals != null || report.Timing.AudioAheadWaitEndToPresentMsMax > 0;
                case "timing.renderIntervalAfterNonAudioWaitSampleCount":
                    return presentSignals != null ||
                        report.Timing.RenderIntervalAfterAudioAheadWaitSampleCount > 0 ||
                        report.Timing.RenderIntervalAfterNonAudioWaitSampleCount > 0;
                case "timing.renderIntervalAfterNonAudioWaitMsP95":
                    return presentSignals != null || report.Timing.RenderIntervalAfterNonAudioWaitMsP95 > 0;
                case "timing.renderIntervalAfterNonAudioWaitMsP99":
                    return presentSignals != null || report.Timing.RenderIntervalAfterNonAudioWaitMsP99 > 0;
                case "timing.renderIntervalAfterNonAudioWaitMsMax":
                    return presentSignals != null || report.Timing.RenderIntervalAfterNonAudioWaitMsMax > 0;
                case "timing.videoAheadWaitCount":
                case "timing.audioAheadWaitCount":
                case "timing.videoClockWaitCount":
                    return presentSignals != null ||
                        report.Timing.VideoAheadWaitCount > 0 ||
                        report.Timing.AudioAheadWaitCount > 0 ||
                        report.Timing.VideoClockWaitCount > 0;
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

        private static bool HasLifecycleOperation(
            PlaybackQualityReport report,
            string operation)
        {
            foreach (var item in report.Lifecycle.Events)
            {
                if (string.Equals(item.Operation, operation, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLifecycleStatus(
            PlaybackQualityReport report,
            string status)
        {
            foreach (var item in report.Lifecycle.Events)
            {
                if (string.Equals(item.Status, status, StringComparison.Ordinal))
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

        private static void AddExpectedSourceStringSignal(
            List<string> requiredSignals,
            string value,
            string signal)
        {
            if (!string.IsNullOrWhiteSpace(value))
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
                report.Timing.DroppedVideoFrames > 0 ||
                report.Timing.VideoAheadWaitCount > 0 ||
                report.Timing.AudioAheadWaitCount > 0 ||
                report.Timing.VideoClockWaitCount > 0 ||
                report.Timing.AudioAheadWaitFinalDeltaAbsMsP50 > 0 ||
                report.Timing.AudioAheadWaitFinalDeltaAbsMsP95 > 0 ||
                report.Timing.AudioAheadWaitFinalDeltaAbsMsP99 > 0 ||
                report.Timing.AudioAheadWaitFinalDeltaAbsMsMax > 0 ||
                report.Timing.AudioAheadWaitPassDurationMsP50 > 0 ||
                report.Timing.AudioAheadWaitPassDurationMsP95 > 0 ||
                report.Timing.AudioAheadWaitPassDurationMsP99 > 0 ||
                report.Timing.AudioAheadWaitPassDurationMsMax > 0 ||
                report.Timing.AudioAheadWaitPassTargetMsP50 > 0 ||
                report.Timing.AudioAheadWaitPassTargetMsP95 > 0 ||
                report.Timing.AudioAheadWaitPassTargetMsP99 > 0 ||
                report.Timing.AudioAheadWaitPassTargetMsMax > 0 ||
                report.Timing.AudioAheadWaitPassOversleepMsP50 > 0 ||
                report.Timing.AudioAheadWaitPassOversleepMsP95 > 0 ||
                report.Timing.AudioAheadWaitPassOversleepMsP99 > 0 ||
                report.Timing.AudioAheadWaitPassOversleepMsMax > 0 ||
                report.Timing.RenderIntervalAfterAudioAheadWaitSampleCount > 0 ||
                report.Timing.RenderIntervalAfterAudioAheadWaitMsP95 > 0 ||
                report.Timing.RenderIntervalAfterAudioAheadWaitMsP99 > 0 ||
                report.Timing.RenderIntervalAfterAudioAheadWaitMsMax > 0 ||
                report.Timing.AudioAheadWaitEndToPresentSampleCount > 0 ||
                report.Timing.AudioAheadWaitEndToPresentMsP50 > 0 ||
                report.Timing.AudioAheadWaitEndToPresentMsP95 > 0 ||
                report.Timing.AudioAheadWaitEndToPresentMsP99 > 0 ||
                report.Timing.AudioAheadWaitEndToPresentMsMax > 0 ||
                report.Timing.RenderIntervalAfterNonAudioWaitSampleCount > 0 ||
                report.Timing.RenderIntervalAfterNonAudioWaitMsP95 > 0 ||
                report.Timing.RenderIntervalAfterNonAudioWaitMsP99 > 0 ||
                report.Timing.RenderIntervalAfterNonAudioWaitMsMax > 0;
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

        private static bool HasTrackFlagEvidence(
            IReadOnlyCollection<PlaybackQualityTrack> tracks,
            Func<PlaybackQualityTrack, bool?> select)
        {
            foreach (var track in tracks)
            {
                if (select(track).HasValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAudioChannelEvidence(PlaybackQualityReport report)
        {
            foreach (var track in report.Tracks.Audio)
            {
                if (track.Channels > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasChapterTextEvidence(
            PlaybackQualityReport report,
            Func<PlaybackQualityChapter, string> select)
        {
            foreach (var chapter in report.Source.Chapters)
            {
                if (!string.IsNullOrWhiteSpace(select(chapter)))
                {
                    return true;
                }
            }

            return false;
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
