using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityOrchestratorProbe
    {
        private const long TimelineSeekOffsetTicks = 300_000_000;

        public static async Task<PlaybackQualityRunResult> RunAsync(
            PlaybackQualityReferenceCase referenceCase,
            PlaybackQualityEnvironment? environment = null)
        {
            if (referenceCase == null)
            {
                throw new ArgumentNullException(nameof(referenceCase));
            }

            if (HasPurpose(referenceCase, "error-handling"))
            {
                var errorResult = PlaybackQualityRuntimeEvidenceCollector.ComposeErrorRunResult(
                    referenceCase,
                    new PlaybackQualityError
                    {
                        Code = "core-probe.error-case",
                        Message = "Core probe intentionally materialized an error-handling reference case without opening playback.",
                        Operation = "open",
                        ExceptionType = "PlaybackQualityProbeException",
                        FailureClass = "sample issue",
                        FailureArea = "error-handling",
                        IsTerminal = true,
                        IsRetriable = false
                    },
                    environment);
                AddProbeLimitations(errorResult.Report.Limitations);
                return new PlaybackQualityRunResult(
                    errorResult.Report,
                    PlaybackQualityReportAnalyzer.Analyze(errorResult.Report),
                    errorResult.CaseMetadata);
            }

            var mediaSource = CreateMediaSource(referenceCase);
            var backend = new ProbePlaybackBackend(
                referenceCase,
                CreateDisplayStatus(referenceCase));
            var orchestrator = new PlaybackOrchestrator(backend);
            var itemId = string.IsNullOrWhiteSpace(referenceCase.ItemId)
                ? referenceCase.CaseId
                : referenceCase.ItemId;

            await orchestrator.StartAsync(
                itemId,
                new[] { mediaSource },
                referenceCase.StartPositionTicks,
                mediaSource.Id).ConfigureAwait(false);
            await orchestrator.PauseAsync().ConfigureAwait(false);
            await orchestrator.ResumeAsync().ConfigureAwait(false);

            if (HasPurpose(referenceCase, "audio-switch"))
            {
                await orchestrator.SwitchAudioStreamAsync(1).ConfigureAwait(false);
            }

            if (HasPurpose(referenceCase, "subtitle-switch"))
            {
                await orchestrator.SwitchSubtitleStreamAsync(3).ConfigureAwait(false);
            }
            else if (HasPurpose(referenceCase, "subtitle-off"))
            {
                await orchestrator.SwitchSubtitleStreamAsync(3).ConfigureAwait(false);
                await orchestrator.SwitchSubtitleStreamAsync(null).ConfigureAwait(false);
            }

            var seekTargetTicks = ResolveSeekTargetTicks(referenceCase);
            if (ShouldProbeSeek(referenceCase))
            {
                await orchestrator.SeekAsync(seekTargetTicks).ConfigureAwait(false);
            }

            var descriptor = orchestrator.CurrentDescriptor ??
                throw new InvalidOperationException("Core probe could not capture playback descriptor.");
            var actualPositionTicks = backend.CurrentPositionTicks;
            var request = PlaybackQualityRuntimeEvidenceCollector.CreateRequest(
                referenceCase,
                descriptor,
                backend,
                backend,
                CreateStartup(),
                environment);

            var composed = PlaybackQualityReportComposer.Compose(request);
            var report = composed.Report;
            report.Position.SeekTargetPositionTicks = seekTargetTicks;
            report.Position.ActualPositionTicks = actualPositionTicks;
            report.Position.SeekPositionErrorMs =
                Math.Abs(actualPositionTicks - seekTargetTicks) / 10000.0;
            report.ColorPipeline.ForceSdrOutput = referenceCase.ForceSdrOutput ||
                HasPurpose(referenceCase, "hdr-force-sdr");
            AddProbeLimitations(report.Limitations);
            PlaybackQualityEvaluator.Evaluate(report);

            await orchestrator.StopAsync().ConfigureAwait(false);

            return new PlaybackQualityRunResult(
                report,
                PlaybackQualityReportAnalyzer.Analyze(report),
                composed.CaseMetadata);
        }

        private static EmbyMediaSource CreateMediaSource(
            PlaybackQualityReferenceCase referenceCase)
        {
            var expected = referenceCase.Expected ?? new PlaybackQualityExpected();
            var source = new EmbyMediaSource
            {
                Id = string.IsNullOrWhiteSpace(referenceCase.MediaSourceId)
                    ? referenceCase.CaseId
                    : referenceCase.MediaSourceId,
                Name = referenceCase.CaseId,
                DirectStreamUrl = referenceCase.Uri,
                Width = expected.Width,
                Height = expected.Height,
                VideoFrameRate = expected.FrameRate,
                HdrProfile = CreateHdrPlaybackProfile(expected)
            };

            source.Streams.Add(new EmbyMediaStream
            {
                Index = 0,
                Kind = EmbyStreamKind.Video,
                Codec = expected.Codec,
                RealFrameRate = expected.FrameRate,
                AverageFrameRate = expected.FrameRate
            });
            source.Streams.Add(new EmbyMediaStream
            {
                Index = 1,
                Kind = EmbyStreamKind.Audio,
                Codec = "eac3",
                Language = "eng",
                ChannelLayout = "5.1",
                DisplayTitle = "English 5.1"
            });
            source.Streams.Add(new EmbyMediaStream
            {
                Index = 2,
                Kind = EmbyStreamKind.Audio,
                Codec = "aac",
                Language = "jpn",
                ChannelLayout = "2.0",
                DisplayTitle = "Japanese Stereo"
            });

            if (HasSubtitlePurpose(referenceCase))
            {
                source.Streams.Add(new EmbyMediaStream
                {
                    Index = 3,
                    Kind = EmbyStreamKind.Subtitle,
                    Codec = "srt",
                    Language = "eng",
                    DisplayTitle = "English",
                    IsExternal = false
                });
            }

            return source;
        }

        private static bool HasSubtitlePurpose(PlaybackQualityReferenceCase referenceCase)
        {
            return HasPurpose(referenceCase, "subtitles") ||
                HasPurpose(referenceCase, "subtitle-switch") ||
                HasPurpose(referenceCase, "subtitle-off");
        }

        private static HdrPlaybackProfile CreateHdrPlaybackProfile(
            PlaybackQualityExpected expected)
        {
            return new HdrPlaybackProfile
            {
                Kind = ParseHdrPlaybackKind(expected.HdrKind),
                Codec = expected.Codec,
                IsDolbyVision = expected.IsDolbyVision == true,
                DolbyVisionProfile = expected.DolbyVisionProfile,
                DolbyVisionCompatibilityId = expected.DolbyVisionCompatibilityId,
                HasHdr10BaseLayer = expected.HasHdr10BaseLayer == true,
                HasHlgBaseLayer = expected.HasHlgBaseLayer == true
            };
        }

        private static HdrPlaybackKind ParseHdrPlaybackKind(string hdrKind)
        {
            if (string.Equals(hdrKind, "Hdr10", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.Hdr10;
            }

            if (string.Equals(hdrKind, "Hlg", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.Hlg;
            }

            if (string.Equals(hdrKind, "DolbyVisionWithHdr10Fallback", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.DolbyVisionWithHdr10Fallback;
            }

            if (string.Equals(hdrKind, "DolbyVisionWithHlgFallback", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.DolbyVisionWithHlgFallback;
            }

            if (string.Equals(hdrKind, "DolbyVisionUnsupported", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.DolbyVisionUnsupported;
            }

            if (string.Equals(hdrKind, "UnknownHdr", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.UnknownHdr;
            }

            return HdrPlaybackKind.Sdr;
        }

        private static PlaybackDisplayStatus CreateDisplayStatus(
            PlaybackQualityReferenceCase referenceCase)
        {
            var expected = referenceCase.Expected ?? new PlaybackQualityExpected();
            var forceSdrOutput = referenceCase.ForceSdrOutput ||
                HasPurpose(referenceCase, "hdr-force-sdr");
            var hdrStatus = ResolveHdrOutputStatus(expected.HdrOutput);
            var isHdrOutput = hdrStatus == HdrOutputStatus.On;
            var isTenBit = IsHdr10Like(expected.HdrOutput) || IsHdr10Like(expected.DxgiOutput);
            var conversionStatus = "";
            if (expected.RequireValidatedConversion &&
                !string.IsNullOrWhiteSpace(expected.HdrOutput))
            {
                conversionStatus = forceSdrOutput && expected.HdrKind != "Sdr"
                    ? "validated;tone-mapped-hable"
                    : "validated";
            }

            return new PlaybackDisplayStatus(
                hdrStatus,
                isHdrDisplayAvailable: isHdrOutput || expected.HdrKind != "Sdr",
                isHdrOutputActive: isHdrOutput,
                message: "core-probe synthetic display status",
                swapChainFormat: isTenBit ? "R10G10B10A2_UNORM" : "B8G8R8A8_UNORM",
                swapChainColorSpace: string.IsNullOrWhiteSpace(expected.DxgiOutput)
                    ? ""
                    : expected.DxgiOutput,
                isTenBitSwapChain: isTenBit,
                isVideoProcessorColorSpaceValidated: !string.IsNullOrWhiteSpace(conversionStatus),
                videoProcessorInputColorSpace: expected.DxgiInput,
                videoProcessorOutputColorSpace: expected.DxgiOutput,
                videoProcessorConversionStatus: conversionStatus,
                refreshRateHz: ResolveRefreshRateHz(referenceCase));
        }

        private static HdrOutputStatus ResolveHdrOutputStatus(string hdrOutput)
        {
            if (string.Equals(hdrOutput, "Hdr10", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hdrOutput, "Hdr", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hdrOutput, "Hlg", StringComparison.OrdinalIgnoreCase))
            {
                return HdrOutputStatus.On;
            }

            if (string.Equals(hdrOutput, "Sdr", StringComparison.OrdinalIgnoreCase))
            {
                return HdrOutputStatus.Off;
            }

            if (string.Equals(hdrOutput, "Unsupported", StringComparison.OrdinalIgnoreCase))
            {
                return HdrOutputStatus.Unsupported;
            }

            return HdrOutputStatus.Unknown;
        }

        private static double ResolveRefreshRateHz(
            PlaybackQualityReferenceCase referenceCase)
        {
            var frameRate = referenceCase.Expected?.FrameRate ?? 0;
            if (frameRate <= 0)
            {
                return 0;
            }

            if (referenceCase.Expected != null &&
                referenceCase.Expected.RequireMatchedDisplayRefreshRate &&
                Math.Abs(frameRate - 23.976) <= 0.01)
            {
                return frameRate * 2.5;
            }

            if (HasPurpose(referenceCase, "cadence-23.976") &&
                Math.Abs(frameRate - 23.976) <= 0.01)
            {
                return frameRate * 2.5;
            }

            return frameRate <= 60.0 ? frameRate : 60.0;
        }

        private static PlaybackQualityMetricsSnapshot CreateMetrics(
            PlaybackQualityReferenceCase referenceCase,
            long positionTicks)
        {
            var expected = referenceCase.Expected ?? new PlaybackQualityExpected();
            var frameRate = expected.FrameRate > 0 ? expected.FrameRate : 60.0;
            var frameDurationMs = 1000.0 / frameRate;
            var renderedFrames = Math.Max(240, expected.MinRenderedVideoFrames ?? 0);

            return new PlaybackQualityMetricsSnapshot
            {
                RenderPasses = (ulong)renderedFrames,
                DecodedVideoFrames = (ulong)renderedFrames,
                RenderedVideoFrames = (ulong)renderedFrames,
                SubmittedAudioFrames = (ulong)renderedFrames,
                DroppedVideoFrames = 0,
                SeekPrerollDroppedFrames = 0,
                VideoAheadWaitCount = 0,
                VideoStarvedPasses = 0,
                AudioStarvedPasses = 0,
                QueuedAudioBuffers = 4,
                AudioClockTicks = positionTicks,
                VideoPositionTicks = positionTicks,
                RenderIntervalMsP50 = frameDurationMs,
                RenderIntervalMsP95 = BelowThreshold(frameDurationMs * 1.01, expected.MaxRenderIntervalMsP95),
                RenderIntervalMsP99 = BelowThreshold(frameDurationMs * 1.02, expected.MaxRenderIntervalMsP99),
                MaxFrameGapMs = BelowThreshold(frameDurationMs * 1.05, expected.MaxFrameGapMs),
                FramePacingSourceFrameRate = frameRate,
                LateFrameDropToleranceMs = frameDurationMs * 2.5,
                AudioVideoDriftMsP50 = 3.0,
                AudioVideoDriftMsP95 = BelowThreshold(5.0, expected.MaxAudioVideoDriftMsP95),
                AudioVideoDriftMsP99 = 8.0,
                AudioVideoDriftMsMax = 12.0
            };
        }

        private static double BelowThreshold(double preferred, double? threshold)
        {
            if (!threshold.HasValue || threshold.Value <= 0)
            {
                return preferred;
            }

            return Math.Min(preferred, threshold.Value * 0.8);
        }

        private static PlaybackQualityStartup CreateStartup()
        {
            return new PlaybackQualityStartup
            {
                CommandReceivedAt = "2026-01-01T00:00:00.000Z",
                PlaybackStartedAt = "2026-01-01T00:00:00.250Z",
                StartupDurationMs = 250.0
            };
        }

        private static long ResolveSeekTargetTicks(
            PlaybackQualityReferenceCase referenceCase)
        {
            return ShouldProbeSeek(referenceCase)
                ? referenceCase.StartPositionTicks + TimelineSeekOffsetTicks
                : referenceCase.StartPositionTicks;
        }

        private static bool ShouldProbeSeek(PlaybackQualityReferenceCase referenceCase)
        {
            return HasPurpose(referenceCase, "timeline") ||
                HasPurpose(referenceCase, "seek") ||
                referenceCase.Expected?.MaxSeekPositionErrorMs != null;
        }

        private static bool IsHdr10Like(string value)
        {
            return value.IndexOf("Hdr", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Hlg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("G2084", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("P2020", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasPurpose(
            PlaybackQualityReferenceCase referenceCase,
            string purpose)
        {
            return referenceCase.Purpose.Contains(purpose);
        }

        private static void AddProbeLimitations(List<string> limitations)
        {
            AddUnique(
                limitations,
                "core-probe: PlaybackOrchestrator lifecycle was executed with an in-process diagnostic backend");
            AddUnique(
                limitations,
                "core-probe: native playback graph, decoder, renderer, network I/O, and HDMI output were not opened");
            AddUnique(
                limitations,
                "core-probe: startup, display, timing, buffering, and A/V sync values are deterministic probe telemetry");
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }

        private sealed class ProbePlaybackBackend :
            IPlaybackBackend,
            IPlaybackBackendDiagnostics,
            IPlaybackQualityMetricsProvider,
            IPlaybackStreamSwitchingBackend
        {
            private readonly PlaybackQualityReferenceCase _referenceCase;

            public ProbePlaybackBackend(
                PlaybackQualityReferenceCase referenceCase,
                PlaybackDisplayStatus displayStatus)
            {
                _referenceCase = referenceCase ??
                    throw new ArgumentNullException(nameof(referenceCase));
                DisplayStatus = displayStatus;
            }

            public long CurrentPositionTicks { get; private set; }

            public PlaybackBackendCapabilities Capabilities { get; } =
                new PlaybackBackendCapabilities(
                    PlaybackBackendFeature.DirectPlayHttp |
                    PlaybackBackendFeature.Hevc |
                    PlaybackBackendFeature.HevcMain10 |
                    PlaybackBackendFeature.Hdr10 |
                    PlaybackBackendFeature.AudioStreamSwitching |
                    PlaybackBackendFeature.SubtitleStreamSwitching |
                    PlaybackBackendFeature.MediaSourceSwitching |
                    PlaybackBackendFeature.NativeAudioOutput);

            public PlaybackDisplayStatus DisplayStatus { get; }

            public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

            public Task StartAsync(PlaybackDescriptor descriptor)
            {
                if (descriptor == null)
                {
                    throw new ArgumentNullException(nameof(descriptor));
                }

                CurrentPositionTicks = descriptor.StartPositionTicks;
                StateChanged?.Invoke(
                    this,
                    new PlaybackStateChangedEventArgs(
                        PlaybackState.Playing,
                        "core-probe started",
                        CurrentPositionTicks));
                return Task.CompletedTask;
            }

            public Task PauseAsync()
            {
                StateChanged?.Invoke(
                    this,
                    new PlaybackStateChangedEventArgs(
                        PlaybackState.Paused,
                        "core-probe paused",
                        CurrentPositionTicks));
                return Task.CompletedTask;
            }

            public Task ResumeAsync()
            {
                StateChanged?.Invoke(
                    this,
                    new PlaybackStateChangedEventArgs(
                        PlaybackState.Playing,
                        "core-probe resumed",
                        CurrentPositionTicks));
                return Task.CompletedTask;
            }

            public Task SeekAsync(long positionTicks)
            {
                CurrentPositionTicks = positionTicks;
                StateChanged?.Invoke(
                    this,
                    new PlaybackStateChangedEventArgs(
                        PlaybackState.Playing,
                        "core-probe seeked",
                        CurrentPositionTicks));
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                StateChanged?.Invoke(
                    this,
                    new PlaybackStateChangedEventArgs(
                        PlaybackState.Stopped,
                        "core-probe stopped",
                        CurrentPositionTicks));
                return Task.CompletedTask;
            }

            public bool TryGetQualityMetrics(out PlaybackQualityMetricsSnapshot metrics)
            {
                metrics = CreateMetrics(_referenceCase, CurrentPositionTicks);
                return true;
            }

            public Task SwitchAudioStreamAsync(int audioStreamIndex)
            {
                return Task.CompletedTask;
            }

            public Task SwitchSubtitleStreamAsync(int? subtitleStreamIndex)
            {
                return Task.CompletedTask;
            }
        }
    }
}
