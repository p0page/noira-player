using System.Diagnostics;
using System.Reflection;
using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Playback;
using NoiraPlayer.Core.PlaybackQuality;

var result = NativeHeadlessHarnessOptions.TryParse(args, out var options, out var error)
    ? NativeHeadlessHarness.Run(options)
    : NativeHeadlessHarness.CreateArgumentError(error);

if (!string.IsNullOrWhiteSpace(result.ReportPath))
{
    Console.WriteLine(result.ReportPath);
}

return result.ExitCode;

internal static class NativeHeadlessHarness
{
    private const string CollectorVersion = "native-headless-harness-v0.1";
    private const long NativeHelperSeekOffsetTicks = 10_000_000;
    private const string NativeWinRtLinkageLimitation =
        "native-headless: current NoiraPlayer.Native build is a Windows Store C++/WinRT component with public playback entrypoints bound to UWP projection";
    private const string NativeGraphHostLimitation =
        "native-headless: offscreen DirectX composition swapchain is smoke-tested, but this runner still lacks a native PlaybackGraph host and lifecycle bridge";

    public static NativeHeadlessHarnessResult Run(NativeHeadlessHarnessOptions options)
    {
        var executionStartedAt = DateTimeOffset.UtcNow;
        var attemptId = Guid.NewGuid().ToString("N");
        var referenceCase = PlaybackQualityCaptureReferenceCaseFactory.Create(
            options.CaseId,
            itemId: "",
            mediaSourceId: "",
            startPositionTicks: options.StartPositionTicks,
            forceSdrOutput: options.ForceSdrOutput,
            expected: new PlaybackQualityExpected(),
            uri: options.StreamUrl,
            category: "stable",
            severity: "high",
            stability: "stable");
        referenceCase.Purpose.Add("sdr-smoke");
        referenceCase.Purpose.Add("frame-pacing");

        if (!string.IsNullOrWhiteSpace(options.NativeHelperExe))
        {
            return RunNativeHelper(options, referenceCase);
        }

        var runResult = PlaybackQualityRuntimeEvidenceCollector.ComposeSkipRunResult(
            referenceCase,
            new PlaybackQualitySkip
            {
                Code = "native-headless.native-link-blocked",
                Reason = "Current NoiraPlayer.Native build is a Windows Store C++/WinRT component whose public playback entrypoint is projected through UWP and exposes AttachSurface(SwapChainPanel); an offscreen DirectX composition swapchain can be created, but App-free native open still needs a native PlaybackGraph host and lifecycle bridge.",
                Operation = "native-headless-open",
                FailureClass = PlaybackQualityFailureClassification.InsufficientInstrumentation,
                FailureArea = "evidence-collection",
                IsExpected = false,
                IsRetriable = true
            },
            new PlaybackQualityEnvironment
            {
                CollectorVersion = CollectorVersion,
                PlayerCoreVersion = GetPlayerCoreVersion(),
                SourceRevision = GetSourceRevision(),
                BuildConfiguration = GetBuildConfiguration()
            },
            CreateExecutionEvidence(
                options,
                attemptId,
                executionStartedAt,
                PlaybackQualityEvidenceLevel.Orchestration,
                PlaybackQualityExecutionStatus.Skipped,
                sourceOpenAttempted: false,
                sourceOpened: false,
                nativeGraphOpened: false,
                demuxStarted: false,
                decoderOpened: false,
                playbackSampleObserved: false));

        if (!runResult.Report.Limitations.Contains(NativeWinRtLinkageLimitation))
        {
            runResult.Report.Limitations.Add(NativeWinRtLinkageLimitation);
        }

        if (!runResult.Report.Limitations.Contains(NativeGraphHostLimitation))
        {
            runResult.Report.Limitations.Add(NativeGraphHostLimitation);
        }

        var reportPath = GetReportPath(options.ReportsDir, options.CaseId);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? options.ReportsDir);
        File.WriteAllText(reportPath, PlaybackQualityReportSerializer.Serialize(runResult));

        return new NativeHeadlessHarnessResult(reportPath, 0);
    }

    private static NativeHeadlessHarnessResult RunNativeHelper(
        NativeHeadlessHarnessOptions options,
        PlaybackQualityReferenceCase referenceCase)
    {
        var commandReceivedAt = DateTimeOffset.UtcNow;
        var attemptId = Guid.NewGuid().ToString("N");
        var reportPath = GetReportPath(options.ReportsDir, options.CaseId);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? options.ReportsDir);
        var helper = RunHelperProcess(options, reportPath);
        var lateHelperFailure = helper.ExitCode != 0 && helper.HasTelemetry;
        var playbackStartedAt = commandReceivedAt.AddMilliseconds(helper.StartupDurationMs);

        if (helper.ExitCode != 0 && !helper.HasTelemetry && !helper.IsUnsupported)
        {
            var errorResult = PlaybackQualityRuntimeEvidenceCollector.ComposeErrorRunResult(
                referenceCase,
                new PlaybackQualityError
                {
                    Code = "native-headless.helper-failed",
                    Message = helper.ErrorMessage,
                    Operation = "native-headless-open",
                    ExceptionType = "native-helper-exit",
                    FailureClass = PlaybackQualityFailureClassification.InsufficientInstrumentation,
                    FailureArea = "evidence-collection",
                    IsTerminal = true,
                    IsRetriable = true
                },
                CreateEnvironment(),
                CreateExecutionEvidence(
                    options,
                    attemptId,
                    commandReceivedAt,
                    PlaybackQualityEvidenceLevel.NativePlayback,
                    PlaybackQualityExecutionStatus.Failed,
                    sourceOpenAttempted: true,
                    sourceOpened: false,
                    nativeGraphOpened: false,
                    demuxStarted: false,
                    decoderOpened: false,
                    playbackSampleObserved: false));
            File.WriteAllText(reportPath, PlaybackQualityReportSerializer.Serialize(errorResult));
            return new NativeHeadlessHarnessResult(reportPath, 1);
        }

        var descriptor = CreateDescriptor(
            options.StreamUrl,
            helper.Source,
            helper.SelectedAudioStreamIndex,
            helper.SelectedSubtitleStreamIndex,
            options.StartPositionTicks);
        helper.Metrics.SubtitleCueRenderCount = Math.Max(
            helper.SubtitleSwitch1.CueCountAfter,
            helper.SubtitleSwitch2.CueCountAfter);
        helper.Metrics.SelectedSubtitleStreamIndex =
            helper.SelectedSubtitleStreamIndex ?? -1;
        var diagnostics = new NativeHeadlessDiagnostics(helper.Color, helper.Display);
        var provider = new NativeHeadlessMetricsProvider(helper.Metrics);
        var lifecycle = new PlaybackQualityLifecycle();
        AddLifecycleEvent(lifecycle, "load", "completed", 0);
        if (!helper.IsUnsupported)
        {
            AddLifecycleEvent(lifecycle, "play", "completed", 0);
        }
        if (helper.PauseResume.Attempted)
        {
            AddLifecycleEvent(
                lifecycle,
                "pause",
                "completed",
                helper.PauseResume.PositionBeforePauseTicks,
                $"duration {helper.PauseResume.DurationSeconds} seconds");
            AddLifecycleEvent(
                lifecycle,
                "resume",
                helper.PauseResume.Status,
                helper.PauseResume.PositionAfterResumeTicks,
                $"position {helper.PauseResume.PositionBeforePauseTicks}->{helper.PauseResume.PositionAfterResumeTicks}; " +
                    $"decoded {helper.PauseResume.PostResumeDecodedVideoFrames}; " +
                    $"rendered {helper.PauseResume.PostResumeRenderedVideoFrames}; " +
                    $"playback failed {helper.PauseResume.PlaybackFailed}");
        }
        if (helper.AudioSwitch.Attempted)
        {
            AddLifecycleEvent(
                lifecycle,
                "audio-switch",
                helper.AudioSwitch.Status,
                helper.AudioSwitch.PositionAfterTicks,
                $"stream index {helper.AudioSwitch.StreamIndex}; position " +
                    $"{helper.AudioSwitch.PositionBeforeTicks}->{helper.AudioSwitch.PositionAfterTicks}; " +
                    $"submitted audio frames {helper.AudioSwitch.SubmittedFramesBefore}->{helper.AudioSwitch.SubmittedFramesAfter}");
        }

        AddSubtitleSwitchLifecycleEvent(lifecycle, helper.SubtitleSwitch1);
        AddSubtitleSwitchLifecycleEvent(lifecycle, helper.SubtitleSwitch2);
        if (helper.SubtitleOff.Attempted)
        {
            AddLifecycleEvent(
                lifecycle,
                "subtitle-off",
                helper.SubtitleOff.Status,
                null,
                "selected subtitle stream index " +
                    FormatStreamIndex(helper.SubtitleOff.SelectedStreamIndex));
        }

        if (helper.Seek.Attempted)
        {
            AddLifecycleEvent(
                lifecycle,
                "seek",
                helper.Seek.Status,
                helper.Seek.ActualPositionTicks,
                $"target {helper.Seek.TargetPositionTicks}; first presented {helper.Seek.ActualPositionTicks?.ToString() ?? "unavailable"}; " +
                    $"post-seek {helper.Seek.PostSeekPlaybackPositionTicks}");
        }

        AddLifecycleEvent(
            lifecycle,
            "stop",
            "completed",
            helper.Seek.Attempted
                ? helper.Seek.PostSeekPlaybackPositionTicks
                : helper.Metrics.VideoPositionTicks);

        var request = PlaybackQualityRuntimeEvidenceCollector.CreateRequest(
            referenceCase,
            descriptor,
            diagnostics,
            provider,
            CreateStartupEvidence(commandReceivedAt, playbackStartedAt, helper.StartupDurationMs),
            CreateEnvironment(),
            lifecycle,
            new PlaybackQualityPosition
            {
                RequestedStartPositionTicks = options.StartPositionTicks,
                SeekTargetPositionTicks = helper.Seek.Attempted
                    ? helper.Seek.TargetPositionTicks
                    : null,
                SeekDemuxTargetTicks = helper.Seek.Attempted
                    ? helper.Seek.DemuxTargetTicks
                    : null,
                ActualPositionTicks = helper.Seek.Attempted
                    ? helper.Seek.ActualPositionTicks
                    : null,
                FirstPresentedPositionTicks = helper.Seek.Attempted
                    ? helper.Seek.ActualPositionTicks
                    : null,
                PostSeekPositionTicks = helper.Seek.Attempted
                    ? helper.Seek.PostSeekPlaybackPositionTicks
                    : null,
                PostSeekAdvanced = helper.Seek.Attempted
                    ? helper.Seek.PostSeekAdvanced
                    : null,
                SeekPositionErrorMs = helper.Seek.Attempted && helper.Seek.ActualPositionTicks.HasValue
                    ? Math.Abs(helper.Seek.ActualPositionTicks.Value - helper.Seek.TargetPositionTicks) / 10000.0
                    : null,
                SeekOperationDurationMs = helper.Seek.Attempted
                    ? helper.Seek.OperationDurationMs
                    : null,
                SeekRecoveryDurationMs = helper.Seek.Attempted
                    ? helper.Seek.RecoveryDurationMs
                    : null
            },
            CreateExecutionEvidence(
                options,
                attemptId,
                commandReceivedAt,
                PlaybackQualityEvidenceLevel.NativePlayback,
                helper.IsUnsupported
                    ? PlaybackQualityExecutionStatus.Unsupported
                    : lateHelperFailure
                        ? PlaybackQualityExecutionStatus.Failed
                        : PlaybackQualityExecutionStatus.Completed,
                sourceOpenAttempted: true,
                sourceOpened: true,
                nativeGraphOpened: !helper.IsUnsupported,
                demuxStarted: true,
                decoderOpened: !helper.IsUnsupported && helper.Metrics.DecodedVideoFrames > 0,
                playbackSampleObserved: helper.Metrics.DecodedVideoFrames > 0 &&
                    helper.Metrics.RenderedVideoFrames > 0));
        if (helper.IsUnsupported && HasNoSourceClassificationExpectation(request.Expected))
        {
            request.Expected = PlaybackQualityExpectedFactory.CreateDefault(descriptor);
        }
        if (request.RuntimeMetrics != null)
        {
            request.RuntimeMetrics.ProcessWallClockMs = helper.ProcessWallClockMs;
            request.RuntimeMetrics.ProcessCpuTimeMs = helper.ProcessCpuTimeMs;
            request.RuntimeMetrics.ProcessCpuUtilizationRatio = helper.ProcessCpuUtilizationRatio;
        }

        request.SourceTimeline = new PlaybackQualitySourceTimeline
        {
            ContainerStartTimeTicks = helper.Source.ContainerStartTimeTicks,
            VideoStreamStartTimeTicks = helper.Source.VideoStreamStartTimeTicks
        };
        request.Interaction = CreateInteractionEvidence(helper);

        var runResult = lateHelperFailure
            ? PlaybackQualityRuntimeEvidenceCollector.ComposeErrorRunResult(
                referenceCase,
                new PlaybackQualityError
                {
                    Code = "native-headless.helper-failed",
                    Message = helper.ErrorMessage,
                    Operation = "native-headless-playback",
                    ExceptionType = "native-helper-exit",
                    FailureClass = PlaybackQualityFailureClassification.PlayerCoreBug,
                    FailureArea = "playback-runtime",
                    IsTerminal = true,
                    IsRetriable = true
                },
                request)
            : PlaybackQualityReportComposer.Compose(request);

        if (runResult.Report.Execution.SourceOpened)
        {
            runResult.Report.Execution.OpenedSourceHash =
                PlaybackQualitySourceFingerprint.ComputeOpenedMediaSignature(runResult.Report);
            runResult.Report.Execution.OpenedSourceHashKind =
                PlaybackQualitySourceFingerprint.OpenedMediaSignatureKind;
        }

        AddLimitation(
            runResult,
            "native-headless: helper executed App-free native PlaybackGraph with an offscreen DirectX composition swapchain");
        AddLimitation(
            runResult,
            "native-headless: source and DXGI color metadata comes from native helper snapshots; HDMI/display output is not verified");
        AddLimitation(
            runResult,
            "native-headless: display refresh is a software policy snapshot; HDMI/display output is not verified");
        AddLimitation(
            runResult,
            "native-headless: timing metrics are captured before the seek operation; seek outcome is reported as separate position evidence");

        File.WriteAllText(reportPath, PlaybackQualityReportSerializer.Serialize(runResult));
        return new NativeHeadlessHarnessResult(reportPath, lateHelperFailure ? 1 : 0);
    }

    private static PlaybackQualityStartup CreateStartupEvidence(
        DateTimeOffset commandReceivedAt,
        DateTimeOffset playbackStartedAt,
        double nativeOpenDurationMs)
    {
        var startup = new PlaybackQualityStartup
        {
            CommandReceivedAt = commandReceivedAt.ToString("O"),
            PlaybackStartedAt = playbackStartedAt.ToString("O"),
            StartupDurationMs = nativeOpenDurationMs
        };
        startup.Stages.Add(new PlaybackQualityStartupStage
        {
            Name = "native.open",
            StartedAt = commandReceivedAt.ToString("O"),
            CompletedAt = playbackStartedAt.ToString("O"),
            DurationMs = nativeOpenDurationMs
        });
        return startup;
    }

    private static PlaybackQualityExecutionEvidence CreateExecutionEvidence(
        NativeHeadlessHarnessOptions options,
        string attemptId,
        DateTimeOffset startedAt,
        string evidenceLevel,
        string status,
        bool sourceOpenAttempted,
        bool sourceOpened,
        bool nativeGraphOpened,
        bool demuxStarted,
        bool decoderOpened,
        bool playbackSampleObserved)
    {
        return new PlaybackQualityExecutionEvidence
        {
            AttemptId = attemptId,
            Runner = "native-headless",
            Scenario = options.Scenario,
            EvidenceLevel = evidenceLevel,
            Status = status,
            SourceLocatorHash = options.SourceLocatorHash,
            OpenedSourceHash = "",
            StartedAtUtc = startedAt.ToString("O"),
            DurationMs = Math.Max(0, (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
            SourceOpenAttempted = sourceOpenAttempted,
            SourceOpened = sourceOpened,
            NativeGraphOpened = nativeGraphOpened,
            DemuxStarted = demuxStarted,
            DecoderOpened = decoderOpened,
            PlaybackSampleObserved = playbackSampleObserved
        };
    }

    private static PlaybackQualityInteractionEvidence CreateInteractionEvidence(
        NativeHeadlessHelperResult helper)
    {
        if (helper.AudioSwitch.Attempted)
        {
            return new PlaybackQualityInteractionEvidence
            {
                Scenario = PlaybackQualityExecutionScenario.AudioSwitch,
                Attempted = true,
                OperationDurationMs = helper.AudioSwitch.OperationDurationMs,
                LockWaitDurationMs = helper.AudioSwitch.LockWaitDurationMs,
                ExecutionDurationMs = helper.AudioSwitch.ExecutionDurationMs,
                QuiesceDurationMs = helper.AudioSwitch.QuiesceDurationMs,
                SeekDurationMs = helper.AudioSwitch.SeekDurationMs,
                DecoderOpenDurationMs = helper.AudioSwitch.DecoderOpenDurationMs,
                RendererOpenDurationMs = helper.AudioSwitch.RendererOpenDurationMs,
                PacketCacheHit = helper.AudioSwitch.PacketCacheHit,
                PacketCacheEnabled = helper.AudioSwitch.PacketCacheEnabled,
                PacketCachePacketCount = helper.AudioSwitch.PacketCachePacketCount,
                PacketCacheBytes = helper.AudioSwitch.PacketCacheBytes,
                PacketCacheWindowDurationTicks = helper.AudioSwitch.PacketCacheWindowDurationTicks,
                RecoveryDurationMs = helper.AudioSwitch.RecoveryDurationMs,
                PositionDeltaTicks = helper.AudioSwitch.PositionAfterTicks -
                    helper.AudioSwitch.PositionBeforeTicks,
                SubmittedAudioFrameDelta = helper.AudioSwitch.SubmittedFramesAfter >=
                    helper.AudioSwitch.SubmittedFramesBefore
                        ? helper.AudioSwitch.SubmittedFramesAfter -
                            helper.AudioSwitch.SubmittedFramesBefore
                        : 0
            };
        }

        var subtitleSwitch = helper.SubtitleSwitch1.Attempted
            ? helper.SubtitleSwitch1
            : helper.SubtitleSwitch2;
        if (subtitleSwitch.Attempted)
        {
            return new PlaybackQualityInteractionEvidence
            {
                Scenario = PlaybackQualityExecutionScenario.SubtitleSwitch,
                Attempted = true,
                OperationDurationMs = subtitleSwitch.OperationDurationMs,
                LockWaitDurationMs = subtitleSwitch.LockWaitDurationMs,
                ExecutionDurationMs = subtitleSwitch.ExecutionDurationMs,
                QuiesceDurationMs = subtitleSwitch.QuiesceDurationMs,
                SeekDurationMs = subtitleSwitch.SeekDurationMs,
                DecoderOpenDurationMs = subtitleSwitch.DecoderOpenDurationMs,
                RendererOpenDurationMs = subtitleSwitch.RendererOpenDurationMs,
                PacketCacheHit = subtitleSwitch.PacketCacheHit,
                PacketCacheEnabled = subtitleSwitch.PacketCacheEnabled,
                PacketCachePacketCount = subtitleSwitch.PacketCachePacketCount,
                PacketCacheBytes = subtitleSwitch.PacketCacheBytes,
                PacketCacheWindowDurationTicks = subtitleSwitch.PacketCacheWindowDurationTicks,
                RecoveryDurationMs = subtitleSwitch.RecoveryDurationMs,
                CueRenderDurationMs = subtitleSwitch.CueRenderDurationMs,
                PositionDeltaTicks = subtitleSwitch.PositionAfterResumeTicks -
                    subtitleSwitch.PositionBeforeResumeTicks,
                RenderedVideoFrameDelta = subtitleSwitch.RenderedFramesAfter >=
                    subtitleSwitch.RenderedFramesBefore
                        ? subtitleSwitch.RenderedFramesAfter - subtitleSwitch.RenderedFramesBefore
                        : 0,
                SubtitleCueRenderCountDelta = subtitleSwitch.CueCountAfter >=
                    subtitleSwitch.CueCountBefore
                        ? subtitleSwitch.CueCountAfter - subtitleSwitch.CueCountBefore
                        : 0
            };
        }

        return new PlaybackQualityInteractionEvidence();
    }

    private static NativeHeadlessHelperResult RunHelperProcess(
        NativeHeadlessHarnessOptions options,
        string reportPath)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = options.NativeHelperExe,
            Arguments = "--stream-url " + QuoteArgument(options.StreamUrl) +
                " --duration-seconds " + options.DurationSeconds.ToString() +
                " --start-position-ticks " + options.StartPositionTicks.ToString() +
                " --scenario " + options.Scenario +
                (options.PauseSeconds > 0
                    ? " --pause-seconds " + options.PauseSeconds.ToString()
                    : "") +
                (string.Equals(
                    Environment.GetEnvironmentVariable("NOIRAPLAYER_QA_DISABLE_SWITCH_PACKET_CACHE"),
                    "1",
                    StringComparison.Ordinal)
                        ? " --disable-switch-packet-cache"
                        : ""),
            WorkingDirectory = Path.GetDirectoryName(options.NativeHelperExe) ?? "",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var startedAt = Stopwatch.StartNew();
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(options.TimeoutSeconds * 1000);
        startedAt.Stop();
        var processWallClockMs = startedAt.Elapsed.TotalMilliseconds;
        var processCpuTimeMs = TryGetProcessCpuTimeMs(process);
        var processCpuUtilizationRatio = processWallClockMs > 0
            ? processCpuTimeMs / processWallClockMs
            : 0.0;

        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            process.WaitForExit();
            var timedOutStdout = stdoutTask.GetAwaiter().GetResult();
            var timedOutStderr = stderrTask.GetAwaiter().GetResult();
            ArchiveHelperTranscript(reportPath, options.StreamUrl, timedOutStdout, timedOutStderr);

            return NativeHeadlessHelperResult.Failed(
                "Native helper timed out before returning playback metrics.");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        ArchiveHelperTranscript(reportPath, options.StreamUrl, stdout, stderr);

        if (process.ExitCode != 0 &&
            TryParseUnsupportedSource(stdout, out var unsupportedSource, out var unsupportedCode))
        {
            return NativeHeadlessHelperResult.Unsupported(
                unsupportedSource,
                unsupportedCode,
                processWallClockMs,
                processCpuTimeMs,
                processCpuUtilizationRatio,
                FirstNonEmpty(stderr.Trim(), "Native helper reported an unsupported source."));
        }

        if (!TryParseMetrics(
            stdout,
            out var metrics,
            out var source,
            out var color,
            out var display,
            out var interactions,
            out var parseError))
        {
            if (process.ExitCode != 0)
            {
                return NativeHeadlessHelperResult.Failed(
                    FirstNonEmpty(
                        stderr.Trim(),
                        "Native helper exited with code " + process.ExitCode + " before returning playback telemetry."));
            }
            return NativeHeadlessHelperResult.Failed(parseError);
        }

        if (process.ExitCode != 0)
        {
            return NativeHeadlessHelperResult.FailedWithTelemetry(
                metrics,
                source,
                color,
                display,
                processWallClockMs,
                processCpuTimeMs,
                processCpuUtilizationRatio,
                interactions,
                FirstNonEmpty(stderr.Trim(), "Native helper exited with code " + process.ExitCode + "."));
        }

        if (options.PauseSeconds > 0 &&
            (!interactions.PauseResume.Attempted ||
                interactions.PauseResume.DurationSeconds != options.PauseSeconds ||
                interactions.PauseResume.Status != "completed"))
        {
            return NativeHeadlessHelperResult.Failed(
                "Native helper did not return completed pause/resume evidence for the requested duration.");
        }

        var expectedSeekTarget = options.StartPositionTicks >= long.MaxValue - NativeHelperSeekOffsetTicks
            ? long.MaxValue
            : options.StartPositionTicks + NativeHelperSeekOffsetTicks;
        if (interactions.Seek.Attempted && interactions.Seek.TargetPositionTicks != expectedSeekTarget)
        {
            return NativeHeadlessHelperResult.Failed(
                "Native helper seek target did not equal requested start position plus the interaction seek offset.");
        }

        return NativeHeadlessHelperResult.Succeeded(
            metrics,
            source,
            color,
            display,
            processWallClockMs,
            processCpuTimeMs,
            processCpuUtilizationRatio,
            interactions);
    }

    private static void ArchiveHelperTranscript(
        string reportPath,
        string streamUrl,
        string stdout,
        string stderr)
    {
        File.WriteAllText(
            reportPath + ".helper.stdout.log",
            SanitizeHelperTranscript(stdout, streamUrl));
        File.WriteAllText(
            reportPath + ".helper.stderr.log",
            SanitizeHelperTranscript(stderr, streamUrl));
    }

    private static string SanitizeHelperTranscript(string transcript, string streamUrl)
    {
        if (string.IsNullOrEmpty(transcript) || string.IsNullOrEmpty(streamUrl))
        {
            return transcript;
        }

        var sanitized = transcript.Replace(
            streamUrl,
            "<redacted-stream-url>",
            StringComparison.Ordinal);
        try
        {
            var decodedStreamUrl = Uri.UnescapeDataString(streamUrl);
            sanitized = sanitized.Replace(
                decodedStreamUrl,
                "<redacted-stream-url>",
                StringComparison.Ordinal);
        }
        catch (UriFormatException)
        {
        }

        return sanitized;
    }

    private static double TryGetProcessCpuTimeMs(Process process)
    {
        try
        {
            return process.TotalProcessorTime.TotalMilliseconds;
        }
        catch
        {
            return 0.0;
        }
    }

    private static bool TryParseMetrics(
        string stdout,
        out PlaybackQualityMetricsSnapshot metrics,
        out NativeHeadlessSourceInfo source,
        out NativeHeadlessColorInfo color,
        out NativeHeadlessDisplayInfo display,
        out NativeHeadlessInteractionResults interactions,
        out string error)
    {
        metrics = new PlaybackQualityMetricsSnapshot();
        source = new NativeHeadlessSourceInfo();
        color = new NativeHeadlessColorInfo();
        display = new NativeHeadlessDisplayInfo();
        interactions = new NativeHeadlessInteractionResults();
        error = "";

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in stdout.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0 || separator == token.Length - 1)
            {
                continue;
            }

            values[token.Substring(0, separator)] = token.Substring(separator + 1);
        }

        if (!TryPopulateRequiredNativeMetrics(values, metrics, out error))
        {
            return false;
        }

        if (!TryParseInteractionResults(values, out interactions, out error))
        {
            return false;
        }
        if (!TryGetRequiredNonNegativeInt64(values, "containerStartTimeTicks", out var containerStartTimeTicks, out error) ||
            !TryGetRequiredNonNegativeInt64(values, "videoStreamStartTimeTicks", out var videoStreamStartTimeTicks, out error) ||
            !TryGetRequiredNonNegativeInt64(values, "logicalDurationTicks", out var logicalDurationTicks, out error))
        {
            return false;
        }

        source = new NativeHeadlessSourceInfo
        {
            Codec = GetString(values, "sourceCodec"),
            Width = GetInt32(values, "sourceWidth"),
            Height = GetInt32(values, "sourceHeight"),
            FrameRate = GetDouble(values, "sourceFrameRate"),
            HdrKind = GetString(values, "sourceHdrKind"),
            VideoRange = DecodeSourceToken(GetString(values, "sourceVideoRange")),
            ColorPrimaries = GetString(values, "sourceColorPrimaries"),
            ColorTransfer = GetString(values, "sourceColorTransfer"),
            ColorSpace = GetString(values, "sourceColorSpace"),
            IsDolbyVision = GetInt32(values, "sourceIsDolbyVision") != 0,
            DolbyVisionProfile = GetInt32(values, "sourceDolbyVisionProfile"),
            DolbyVisionCompatibilityId = GetInt32(values, "sourceDolbyVisionCompatibilityId"),
            HasHdr10BaseLayer = GetInt32(values, "sourceHasHdr10BaseLayer") != 0,
            HasHlgBaseLayer = GetInt32(values, "sourceHasHlgBaseLayer") != 0,
            ContainerStartTimeTicks = containerStartTimeTicks,
            VideoStreamStartTimeTicks = videoStreamStartTimeTicks,
            LogicalDurationTicks = logicalDurationTicks
        };
        source.Tracks.AddRange(ParseNativeTracks(values));
        color = new NativeHeadlessColorInfo
        {
            DxgiInput = GetString(values, "dxgiInput"),
            DxgiOutput = GetString(values, "dxgiOutput"),
            ConversionStatus = GetString(values, "conversionStatus"),
            IsVideoProcessorColorSpaceValidated = GetInt32(values, "isVideoProcessorColorSpaceValidated") != 0
        };
        display = new NativeHeadlessDisplayInfo
        {
            RefreshRateHz = GetDouble(values, "displayRefreshRateHz"),
            RefreshPolicy = GetString(values, "displayRefreshPolicy")
        };
        return true;
    }

    private static List<NativeHeadlessTrackInfo> ParseNativeTracks(
        Dictionary<string, string> values)
    {
        var tracks = new List<NativeHeadlessTrackInfo>();
        var count = GetInt32(values, "sourceTrackCount");
        for (var index = 0; index < count; index++)
        {
            var prefix = "track" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var kind = GetString(values, prefix + "Kind");
            if (string.IsNullOrWhiteSpace(kind))
            {
                continue;
            }

            tracks.Add(new NativeHeadlessTrackInfo
            {
                Index = GetInt32(values, prefix + "Index"),
                Kind = kind,
                Codec = GetString(values, prefix + "Codec"),
                Language = GetString(values, prefix + "Language"),
                ChannelLayout = GetString(values, prefix + "ChannelLayout"),
                Channels = GetInt32(values, prefix + "Channels"),
                IsDefault = GetInt32(values, prefix + "IsDefault") != 0,
                IsForced = GetInt32(values, prefix + "IsForced") != 0,
                RealFrameRate = GetDouble(values, prefix + "RealFrameRate"),
                AverageFrameRate = GetDouble(values, prefix + "AverageFrameRate")
            });
        }

        return tracks;
    }

    private static bool TryParseInteractionResults(
        Dictionary<string, string> values,
        out NativeHeadlessInteractionResults interactions,
        out string error)
    {
        interactions = new NativeHeadlessInteractionResults();
        if (!TryParsePauseResumeOutcome(values, out var pauseResume, out error) ||
            !TryParseAudioSwitchOutcome(values, out var audioSwitch, out error) ||
            !TryParseSubtitleSwitchOutcome(values, "subtitleSwitch1", out var subtitleSwitch1, out error) ||
            !TryParseSubtitleSwitchOutcome(values, "subtitleSwitch2", out var subtitleSwitch2, out error) ||
            !TryParseSubtitleOffOutcome(values, out var subtitleOff, out error) ||
            !TryParseSeekOutcome(values, out var seek, out error) ||
            !TryGetRequiredNullableStreamIndex(values, "selectedAudioStreamIndex", out var selectedAudio, out error) ||
            !TryGetRequiredNullableStreamIndex(values, "selectedSubtitleStreamIndex", out var selectedSubtitle, out error))
        {
            return false;
        }

        if (audioSwitch.Attempted && audioSwitch.Status == "completed")
        {
            if (!selectedAudio.HasValue || selectedAudio.Value != audioSwitch.StreamIndex)
            {
                error = "Native helper field 'selectedAudioStreamIndex' must equal audioSwitchStreamIndex when audioSwitchStatus is completed.";
                return false;
            }

            if (audioSwitch.PositionAfterTicks <= audioSwitch.PositionBeforeTicks)
            {
                error = "Native helper field 'audioSwitchPositionAfterTicks' must advance when audioSwitchStatus is completed.";
                return false;
            }

            if (audioSwitch.SubmittedFramesAfter <= audioSwitch.SubmittedFramesBefore)
            {
                error = "Native helper field 'audioSwitchSubmittedFramesAfter' must advance when audioSwitchStatus is completed.";
                return false;
            }
        }

        if (!ValidateSubtitleSwitchOutcome(subtitleSwitch1, "subtitleSwitch1", out error) ||
            !ValidateSubtitleSwitchOutcome(subtitleSwitch2, "subtitleSwitch2", out error))
        {
            return false;
        }

        interactions = new NativeHeadlessInteractionResults
        {
            PauseResume = pauseResume,
            AudioSwitch = audioSwitch,
            SubtitleSwitch1 = subtitleSwitch1,
            SubtitleSwitch2 = subtitleSwitch2,
            SubtitleOff = subtitleOff,
            Seek = seek,
            SelectedAudioStreamIndex = selectedAudio,
            SelectedSubtitleStreamIndex = selectedSubtitle
        };
        return true;
    }

    private static bool TryParsePauseResumeOutcome(
        Dictionary<string, string> values,
        out NativeHeadlessPauseResumeOutcome outcome,
        out string error)
    {
        outcome = new NativeHeadlessPauseResumeOutcome();
        error = "";
        if (!values.TryGetValue("pauseDurationSeconds", out var rawDuration))
        {
            return true;
        }

        if (!int.TryParse(rawDuration, out var durationSeconds) ||
            durationSeconds < 0)
        {
            error = "Native helper field 'pauseDurationSeconds' must be a non-negative integer.";
            return false;
        }

        if (durationSeconds == 0)
        {
            return true;
        }

        if (!TryGetInt64(values, "positionBeforePauseTicks", out var positionBeforePauseTicks) ||
            positionBeforePauseTicks < 0 ||
            !TryGetInt64(values, "positionAfterResumeTicks", out var positionAfterResumeTicks) ||
            positionAfterResumeTicks < 0 ||
            !TryGetUInt64(values, "postResumeDecodedVideoFrames", out var decodedVideoFrames) ||
            !TryGetUInt64(values, "postResumeRenderedVideoFrames", out var renderedVideoFrames) ||
            !values.TryGetValue("playbackFailed", out var rawPlaybackFailed) ||
            !int.TryParse(rawPlaybackFailed, out var playbackFailed) ||
            (playbackFailed != 0 && playbackFailed != 1))
        {
            error = "Native helper pause/resume evidence is incomplete or invalid.";
            return false;
        }

        var status = GetString(values, "pauseResumeStatus");
        if (status != "completed" && status != "failed")
        {
            error = "Native helper field 'pauseResumeStatus' must be completed or failed.";
            return false;
        }

        if (status == "completed" &&
            (positionAfterResumeTicks <= positionBeforePauseTicks ||
                decodedVideoFrames == 0 ||
                renderedVideoFrames == 0 ||
                playbackFailed != 0))
        {
            error = "Completed native pause/resume evidence must advance position and preserve decoded/rendered playback.";
            return false;
        }

        outcome = new NativeHeadlessPauseResumeOutcome
        {
            Attempted = true,
            DurationSeconds = durationSeconds,
            Status = status,
            PositionBeforePauseTicks = positionBeforePauseTicks,
            PositionAfterResumeTicks = positionAfterResumeTicks,
            PostResumeDecodedVideoFrames = decodedVideoFrames,
            PostResumeRenderedVideoFrames = renderedVideoFrames,
            PlaybackFailed = playbackFailed != 0
        };
        return true;
    }

    private static bool TryParseUnsupportedSource(
        string stdout,
        out NativeHeadlessSourceInfo source,
        out string unsupportedCode)
    {
        source = new NativeHeadlessSourceInfo();
        unsupportedCode = "";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in stdout.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0 || separator == token.Length - 1)
            {
                continue;
            }

            values[token.Substring(0, separator)] = token.Substring(separator + 1);
        }

        unsupportedCode = GetString(values, "unsupportedCode");
        if (!string.Equals(
                unsupportedCode,
                "dolby-vision-profile5-no-fallback",
                StringComparison.Ordinal) ||
            GetInt32(values, "sourceIsDolbyVision") == 0 ||
            GetInt32(values, "sourceDolbyVisionProfile") != 5)
        {
            return false;
        }

        source = new NativeHeadlessSourceInfo
        {
            Codec = GetString(values, "sourceCodec"),
            Width = GetInt32(values, "sourceWidth"),
            Height = GetInt32(values, "sourceHeight"),
            FrameRate = GetDouble(values, "sourceFrameRate"),
            HdrKind = GetString(values, "sourceHdrKind"),
            VideoRange = DecodeSourceToken(GetString(values, "sourceVideoRange")),
            ColorPrimaries = GetString(values, "sourceColorPrimaries"),
            ColorTransfer = GetString(values, "sourceColorTransfer"),
            ColorSpace = GetString(values, "sourceColorSpace"),
            IsDolbyVision = true,
            DolbyVisionProfile = 5,
            DolbyVisionCompatibilityId = GetInt32(values, "sourceDolbyVisionCompatibilityId"),
            HasHdr10BaseLayer = GetInt32(values, "sourceHasHdr10BaseLayer") != 0,
            HasHlgBaseLayer = GetInt32(values, "sourceHasHlgBaseLayer") != 0,
            ContainerStartTimeTicks = GetInt64(values, "containerStartTimeTicks"),
            VideoStreamStartTimeTicks = GetInt64(values, "videoStreamStartTimeTicks"),
            LogicalDurationTicks = GetInt64(values, "logicalDurationTicks")
        };
        return !string.IsNullOrWhiteSpace(source.Codec) &&
            source.Width > 0 &&
            source.Height > 0 &&
            source.FrameRate > 0.0 &&
            string.Equals(source.HdrKind, "DolbyVisionUnsupported", StringComparison.OrdinalIgnoreCase) &&
            !source.HasHdr10BaseLayer &&
            !source.HasHlgBaseLayer;
    }

    private static string DecodeSourceToken(string value)
    {
        return value.Replace('_', ' ');
    }

    private static bool HasNoSourceClassificationExpectation(PlaybackQualityExpected? expected)
    {
        return expected == null ||
            (string.IsNullOrWhiteSpace(expected.HdrKind) &&
                string.IsNullOrWhiteSpace(expected.HdrPlaybackStrategy) &&
                !expected.IsDirectPlayable.HasValue &&
                !expected.IsDolbyVision.HasValue &&
                !expected.DolbyVisionProfile.HasValue &&
                !expected.DolbyVisionCompatibilityId.HasValue);
    }

    private static bool TryPopulateRequiredNativeMetrics(
        Dictionary<string, string> values,
        PlaybackQualityMetricsSnapshot metrics,
        out string error)
    {
        return
            TrySetRequiredUInt64(values, "decodedVideoFrames", value => metrics.DecodedVideoFrames = value, out error) &&
            TrySetRequiredUInt64(values, "hardwareDecodedVideoFrames", value => metrics.HardwareDecodedVideoFrames = value, out error) &&
            TrySetRequiredUInt64(values, "softwareDecodedVideoFrames", value => metrics.SoftwareDecodedVideoFrames = value, out error) &&
            TrySetRequiredUInt64(values, "renderedVideoFrames", value => metrics.RenderedVideoFrames = value, out error) &&
            TrySetRequiredUInt64(values, "renderPasses", value => metrics.RenderPasses = value, out error) &&
            TrySetRequiredUInt64(values, "submittedAudioFrames", value => metrics.SubmittedAudioFrames = value, out error) &&
            TrySetRequiredUInt64(values, "queuedAudioBuffers", value => metrics.QueuedAudioBuffers = value, out error) &&
            TrySetRequiredUInt64(values, "droppedVideoFrames", value => metrics.DroppedVideoFrames = value, out error) &&
            TrySetRequiredUInt64(values, "seekPrerollDroppedFrames", value => metrics.SeekPrerollDroppedFrames = value, out error) &&
            TrySetRequiredUInt64(values, "videoAheadWaitCount", value => metrics.VideoAheadWaitCount = value, out error) &&
            TrySetRequiredUInt64(values, "audioAheadWaitCount", value => metrics.AudioAheadWaitCount = value, out error) &&
            TrySetRequiredUInt64(values, "videoClockWaitCount", value => metrics.VideoClockWaitCount = value, out error) &&
            TrySetRequiredUInt64(values, "videoStarvedPasses", value => metrics.VideoStarvedPasses = value, out error) &&
            TrySetRequiredUInt64(values, "audioStarvedPasses", value => metrics.AudioStarvedPasses = value, out error) &&
            TrySetRequiredNonNegativeInt64(values, "audioClockTicks", value => metrics.AudioClockTicks = value, out error) &&
            TrySetRequiredNonNegativeInt64(values, "videoPositionTicks", value => metrics.VideoPositionTicks = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "nativeGraphOpenDurationMs", value => metrics.NativeGraphOpenDurationMs = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "ffmpegOpenInputDurationMs", value => metrics.FfmpegOpenInputDurationMs = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "ffmpegStreamInfoDurationMs", value => metrics.FfmpegStreamInfoDurationMs = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "nativeStartupSeekDurationMs", value => metrics.NativeStartupSeekDurationMs = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "nativeFirstFrameDurationMs", value => metrics.NativeFirstFrameDurationMs = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "nativeFirstFrameDemuxReadDurationMs", value => metrics.NativeFirstFrameDemuxReadDurationMs = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "nativeFirstFramePresentDurationMs", value => metrics.NativeFirstFramePresentDurationMs = value, out error) &&
            TrySetRequiredUInt64(values, "nativeFirstFrameDemuxPacketCount", value => metrics.NativeFirstFrameDemuxPacketCount = value, out error) &&
            TrySetRequiredUInt64(values, "nativeFirstFrameDemuxBytes", value => metrics.NativeFirstFrameDemuxBytes = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "renderIntervalMsP05", value => metrics.RenderIntervalMsP05 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "renderIntervalMsP50", value => metrics.RenderIntervalMsP50 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "renderIntervalMsP95", value => metrics.RenderIntervalMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "renderIntervalMsP99", value => metrics.RenderIntervalMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "minFrameGapMs", value => metrics.MinFrameGapMs = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "maxFrameGapMs", value => metrics.MaxFrameGapMs = value, out error) &&
            TrySetRequiredUInt64(values, "renderIntervalSampleCount", value => metrics.RenderIntervalSampleCount = value, out error) &&
            TrySetRequiredUInt64(values, "renderIntervalOverExpected2MsCount", value => metrics.RenderIntervalOverExpected2MsCount = value, out error) &&
            TrySetRequiredUInt64(values, "renderIntervalOverExpected4MsCount", value => metrics.RenderIntervalOverExpected4MsCount = value, out error) &&
            TrySetRequiredUInt64(values, "renderIntervalUnderExpected2MsCount", value => metrics.RenderIntervalUnderExpected2MsCount = value, out error) &&
            TrySetRequiredUInt64(values, "renderIntervalUnderExpected4MsCount", value => metrics.RenderIntervalUnderExpected4MsCount = value, out error) &&
            TrySetRequiredUInt64(values, "renderIntervalAfterAudioAheadWaitSampleCount", value => metrics.RenderIntervalAfterAudioAheadWaitSampleCount = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "renderIntervalAfterAudioAheadWaitMsP95", value => metrics.RenderIntervalAfterAudioAheadWaitMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "renderIntervalAfterAudioAheadWaitMsP99", value => metrics.RenderIntervalAfterAudioAheadWaitMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "renderIntervalAfterAudioAheadWaitMsMax", value => metrics.RenderIntervalAfterAudioAheadWaitMsMax = value, out error) &&
            TrySetRequiredUInt64(values, "audioAheadWaitEndToPresentSampleCount", value => metrics.AudioAheadWaitEndToPresentSampleCount = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitEndToPresentMsP50", value => metrics.AudioAheadWaitEndToPresentMsP50 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitEndToPresentMsP95", value => metrics.AudioAheadWaitEndToPresentMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitEndToPresentMsP99", value => metrics.AudioAheadWaitEndToPresentMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitEndToPresentMsMax", value => metrics.AudioAheadWaitEndToPresentMsMax = value, out error) &&
            TrySetRequiredUInt64(values, "renderIntervalAfterNonAudioWaitSampleCount", value => metrics.RenderIntervalAfterNonAudioWaitSampleCount = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "renderIntervalAfterNonAudioWaitMsP95", value => metrics.RenderIntervalAfterNonAudioWaitMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "renderIntervalAfterNonAudioWaitMsP99", value => metrics.RenderIntervalAfterNonAudioWaitMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "renderIntervalAfterNonAudioWaitMsMax", value => metrics.RenderIntervalAfterNonAudioWaitMsMax = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "presentDurationMsP50", value => metrics.PresentDurationMsP50 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "presentDurationMsP95", value => metrics.PresentDurationMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "presentDurationMsP99", value => metrics.PresentDurationMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "presentDurationMsMax", value => metrics.PresentDurationMsMax = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitDurationMsP50", value => metrics.AudioAheadWaitDurationMsP50 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitDurationMsP95", value => metrics.AudioAheadWaitDurationMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitDurationMsP99", value => metrics.AudioAheadWaitDurationMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitDurationMsMax", value => metrics.AudioAheadWaitDurationMsMax = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitTargetMsP50", value => metrics.AudioAheadWaitTargetMsP50 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitTargetMsP95", value => metrics.AudioAheadWaitTargetMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitTargetMsP99", value => metrics.AudioAheadWaitTargetMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitTargetMsMax", value => metrics.AudioAheadWaitTargetMsMax = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitOversleepMsP50", value => metrics.AudioAheadWaitOversleepMsP50 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitOversleepMsP95", value => metrics.AudioAheadWaitOversleepMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitOversleepMsP99", value => metrics.AudioAheadWaitOversleepMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitOversleepMsMax", value => metrics.AudioAheadWaitOversleepMsMax = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitFinalDeltaAbsMsP50", value => metrics.AudioAheadWaitFinalDeltaAbsMsP50 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitFinalDeltaAbsMsP95", value => metrics.AudioAheadWaitFinalDeltaAbsMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitFinalDeltaAbsMsP99", value => metrics.AudioAheadWaitFinalDeltaAbsMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitFinalDeltaAbsMsMax", value => metrics.AudioAheadWaitFinalDeltaAbsMsMax = value, out error) &&
            TrySetRequiredUInt64(values, "audioAheadWaitEpisodeCount", value => metrics.AudioAheadWaitEpisodeCount = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassesPerEpisodeP50", value => metrics.AudioAheadWaitPassesPerEpisodeP50 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassesPerEpisodeP95", value => metrics.AudioAheadWaitPassesPerEpisodeP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassesPerEpisodeP99", value => metrics.AudioAheadWaitPassesPerEpisodeP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassesPerEpisodeMax", value => metrics.AudioAheadWaitPassesPerEpisodeMax = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassDurationMsP50", value => metrics.AudioAheadWaitPassDurationMsP50 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassDurationMsP95", value => metrics.AudioAheadWaitPassDurationMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassDurationMsP99", value => metrics.AudioAheadWaitPassDurationMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassDurationMsMax", value => metrics.AudioAheadWaitPassDurationMsMax = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassTargetMsP50", value => metrics.AudioAheadWaitPassTargetMsP50 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassTargetMsP95", value => metrics.AudioAheadWaitPassTargetMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassTargetMsP99", value => metrics.AudioAheadWaitPassTargetMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassTargetMsMax", value => metrics.AudioAheadWaitPassTargetMsMax = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassOversleepMsP50", value => metrics.AudioAheadWaitPassOversleepMsP50 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassOversleepMsP95", value => metrics.AudioAheadWaitPassOversleepMsP95 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassOversleepMsP99", value => metrics.AudioAheadWaitPassOversleepMsP99 = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "audioAheadWaitPassOversleepMsMax", value => metrics.AudioAheadWaitPassOversleepMsMax = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "framePacingSourceFrameRate", value => metrics.FramePacingSourceFrameRate = value, out error) &&
            TrySetRequiredNonNegativeDouble(values, "lateFrameDropToleranceMs", value => metrics.LateFrameDropToleranceMs = value, out error) &&
            TrySetRequiredFiniteDouble(values, "audioVideoDriftMsP50", value => metrics.AudioVideoDriftMsP50 = value, out error) &&
            TrySetRequiredFiniteDouble(values, "audioVideoDriftMsP95", value => metrics.AudioVideoDriftMsP95 = value, out error) &&
            TrySetRequiredFiniteDouble(values, "audioVideoDriftMsP99", value => metrics.AudioVideoDriftMsP99 = value, out error) &&
            TrySetRequiredFiniteDouble(values, "audioVideoDriftMsMax", value => metrics.AudioVideoDriftMsMax = value, out error);
    }

    private static bool TryParseAudioSwitchOutcome(
        Dictionary<string, string> values,
        out NativeHeadlessAudioSwitchOutcome outcome,
        out string error)
    {
        outcome = new NativeHeadlessAudioSwitchOutcome();
        if (!TryGetAttempted(values, "audioSwitchAttempted", out var attempted, out error))
        {
            return false;
        }

        outcome.Attempted = attempted;
        if (!attempted)
        {
            return true;
        }

        if (!TryGetInteractionStatus(values, "audioSwitchStatus", out var status, out error) ||
            !TryGetRequiredNonNegativeInt32(values, "audioSwitchStreamIndex", out var streamIndex, out error) ||
            !TryGetRequiredInt64(values, "audioSwitchPositionBeforeTicks", out var positionBefore, out error) ||
            !TryGetRequiredInt64(values, "audioSwitchPositionAfterTicks", out var positionAfter, out error) ||
            !TryGetRequiredUInt64(values, "audioSwitchSubmittedFramesBefore", out var framesBefore, out error) ||
            !TryGetRequiredUInt64(values, "audioSwitchSubmittedFramesAfter", out var framesAfter, out error) ||
            !TryGetRequiredNonNegativeDouble(values, "audioSwitchOperationDurationMs", out var operationDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, "audioSwitchLockWaitDurationMs", out var lockWaitDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, "audioSwitchExecutionDurationMs", out var executionDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, "audioSwitchQuiesceDurationMs", out var quiesceDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, "audioSwitchSeekDurationMs", out var seekDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, "audioSwitchDecoderOpenDurationMs", out var decoderOpenDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, "audioSwitchRendererOpenDurationMs", out var rendererOpenDurationMs, out error) ||
            !TryGetAttempted(values, "audioSwitchPacketCacheHit", out var packetCacheHit, out error) ||
            !TryGetAttempted(values, "audioSwitchPacketCacheEnabled", out var packetCacheEnabled, out error) ||
            !TryGetRequiredUInt64(values, "audioSwitchPacketCachePacketCount", out var packetCachePacketCount, out error) ||
            !TryGetRequiredUInt64(values, "audioSwitchPacketCacheBytes", out var packetCacheBytes, out error) ||
            !TryGetRequiredNonNegativeInt64(values, "audioSwitchPacketCacheWindowDurationTicks", out var packetCacheWindowDurationTicks, out error) ||
            !TryGetRequiredNonNegativeDouble(values, "audioSwitchRecoveryDurationMs", out var recoveryDurationMs, out error))
        {
            return false;
        }

        outcome.Status = status;
        outcome.StreamIndex = streamIndex;
        outcome.PositionBeforeTicks = positionBefore;
        outcome.PositionAfterTicks = positionAfter;
        outcome.SubmittedFramesBefore = framesBefore;
        outcome.SubmittedFramesAfter = framesAfter;
        outcome.OperationDurationMs = operationDurationMs;
        outcome.LockWaitDurationMs = lockWaitDurationMs;
        outcome.ExecutionDurationMs = executionDurationMs;
        outcome.QuiesceDurationMs = quiesceDurationMs;
        outcome.SeekDurationMs = seekDurationMs;
        outcome.DecoderOpenDurationMs = decoderOpenDurationMs;
        outcome.RendererOpenDurationMs = rendererOpenDurationMs;
        outcome.PacketCacheHit = packetCacheHit;
        outcome.PacketCacheEnabled = packetCacheEnabled;
        outcome.PacketCachePacketCount = packetCachePacketCount;
        outcome.PacketCacheBytes = packetCacheBytes;
        outcome.PacketCacheWindowDurationTicks = packetCacheWindowDurationTicks;
        outcome.RecoveryDurationMs = recoveryDurationMs;
        return true;
    }

    private static bool TryParseSubtitleSwitchOutcome(
        Dictionary<string, string> values,
        string prefix,
        out NativeHeadlessSubtitleSwitchOutcome outcome,
        out string error)
    {
        outcome = new NativeHeadlessSubtitleSwitchOutcome();
        if (!TryGetAttempted(values, prefix + "Attempted", out var attempted, out error))
        {
            return false;
        }

        outcome.Attempted = attempted;
        if (!attempted)
        {
            return true;
        }

        if (!TryGetInteractionStatus(values, prefix + "Status", out var status, out error) ||
            !TryGetRequiredNonNegativeInt32(values, prefix + "StreamIndex", out var streamIndex, out error) ||
            !TryGetRequiredUInt64(values, prefix + "CueCountBefore", out var cueCountBefore, out error) ||
            !TryGetRequiredUInt64(values, prefix + "CueCountAfter", out var cueCountAfter, out error) ||
            !TryGetAttempted(values, prefix + "PausedSwitch", out var pausedSwitch, out error) ||
            !TryGetRequiredNullableStreamIndex(values, prefix + "SelectedStreamIndex", out var selectedStreamIndex, out error) ||
            !TryGetRequiredNonNegativeInt64(values, prefix + "PausedPositionBeforeTicks", out var pausedPositionBefore, out error) ||
            !TryGetRequiredNonNegativeInt64(values, prefix + "PausedPositionAfterTicks", out var pausedPositionAfter, out error) ||
            !TryGetRequiredNonNegativeInt64(values, prefix + "PositionBeforeResumeTicks", out var positionBeforeResume, out error) ||
            !TryGetRequiredNonNegativeInt64(values, prefix + "PositionAfterResumeTicks", out var positionAfterResume, out error) ||
            !TryGetRequiredNonNegativeDouble(values, prefix + "OperationDurationMs", out var operationDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, prefix + "LockWaitDurationMs", out var lockWaitDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, prefix + "ExecutionDurationMs", out var executionDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, prefix + "QuiesceDurationMs", out var quiesceDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, prefix + "SeekDurationMs", out var seekDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, prefix + "DecoderOpenDurationMs", out var decoderOpenDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, prefix + "RendererOpenDurationMs", out var rendererOpenDurationMs, out error) ||
            !TryGetAttempted(values, prefix + "PacketCacheHit", out var packetCacheHit, out error) ||
            !TryGetAttempted(values, prefix + "PacketCacheEnabled", out var packetCacheEnabled, out error) ||
            !TryGetRequiredUInt64(values, prefix + "PacketCachePacketCount", out var packetCachePacketCount, out error) ||
            !TryGetRequiredUInt64(values, prefix + "PacketCacheBytes", out var packetCacheBytes, out error) ||
            !TryGetRequiredNonNegativeInt64(values, prefix + "PacketCacheWindowDurationTicks", out var packetCacheWindowDurationTicks, out error) ||
            !TryGetRequiredNonNegativeDouble(values, prefix + "RecoveryDurationMs", out var recoveryDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, prefix + "CueRenderDurationMs", out var cueRenderDurationMs, out error) ||
            !TryGetRequiredUInt64(values, prefix + "RenderedFramesBefore", out var renderedFramesBefore, out error) ||
            !TryGetRequiredUInt64(values, prefix + "RenderedFramesAfter", out var renderedFramesAfter, out error))
        {
            return false;
        }

        outcome.Status = status;
        outcome.StreamIndex = streamIndex;
        outcome.CueCountBefore = cueCountBefore;
        outcome.CueCountAfter = cueCountAfter;
        outcome.PausedSwitch = pausedSwitch;
        outcome.SelectedStreamIndex = selectedStreamIndex;
        outcome.PausedPositionBeforeTicks = pausedPositionBefore;
        outcome.PausedPositionAfterTicks = pausedPositionAfter;
        outcome.PositionBeforeResumeTicks = positionBeforeResume;
        outcome.PositionAfterResumeTicks = positionAfterResume;
        outcome.OperationDurationMs = operationDurationMs;
        outcome.LockWaitDurationMs = lockWaitDurationMs;
        outcome.ExecutionDurationMs = executionDurationMs;
        outcome.QuiesceDurationMs = quiesceDurationMs;
        outcome.SeekDurationMs = seekDurationMs;
        outcome.DecoderOpenDurationMs = decoderOpenDurationMs;
        outcome.RendererOpenDurationMs = rendererOpenDurationMs;
        outcome.PacketCacheHit = packetCacheHit;
        outcome.PacketCacheEnabled = packetCacheEnabled;
        outcome.PacketCachePacketCount = packetCachePacketCount;
        outcome.PacketCacheBytes = packetCacheBytes;
        outcome.PacketCacheWindowDurationTicks = packetCacheWindowDurationTicks;
        outcome.RecoveryDurationMs = recoveryDurationMs;
        outcome.CueRenderDurationMs = cueRenderDurationMs;
        outcome.RenderedFramesBefore = renderedFramesBefore;
        outcome.RenderedFramesAfter = renderedFramesAfter;
        return true;
    }

    private static bool ValidateSubtitleSwitchOutcome(
        NativeHeadlessSubtitleSwitchOutcome outcome,
        string prefix,
        out string error)
    {
        error = "";
        if (!outcome.Attempted || outcome.Status != "completed")
        {
            return true;
        }

        if (!outcome.SelectedStreamIndex.HasValue ||
            outcome.SelectedStreamIndex.Value != outcome.StreamIndex)
        {
            error = $"Native helper field '{prefix}SelectedStreamIndex' must equal {prefix}StreamIndex when {prefix}Status is completed.";
            return false;
        }

        if (outcome.CueCountAfter <= outcome.CueCountBefore)
        {
            error = $"Native helper field '{prefix}CueCountAfter' must advance when {prefix}Status is completed.";
            return false;
        }

        if (outcome.PausedSwitch &&
            outcome.PausedPositionAfterTicks != outcome.PausedPositionBeforeTicks)
        {
            error = $"Native helper field '{prefix}PausedPositionAfterTicks' must remain unchanged while the completed subtitle switch is paused.";
            return false;
        }

        if (outcome.PausedSwitch &&
            outcome.PositionAfterResumeTicks <= outcome.PositionBeforeResumeTicks)
        {
            error = $"Native helper field '{prefix}PositionAfterResumeTicks' must advance after the completed paused subtitle switch resumes.";
            return false;
        }

        if (outcome.RenderedFramesAfter <= outcome.RenderedFramesBefore)
        {
            error = $"Native helper field '{prefix}RenderedFramesAfter' must advance when {prefix}Status is completed.";
            return false;
        }

        return true;
    }

    private static bool TryParseSubtitleOffOutcome(
        Dictionary<string, string> values,
        out NativeHeadlessSubtitleOffOutcome outcome,
        out string error)
    {
        outcome = new NativeHeadlessSubtitleOffOutcome();
        if (!TryGetAttempted(values, "subtitleOffAttempted", out var attempted, out error))
        {
            return false;
        }

        outcome.Attempted = attempted;
        if (!attempted)
        {
            return true;
        }

        if (!TryGetInteractionStatus(values, "subtitleOffStatus", out var status, out error) ||
            !TryGetRequiredNullableStreamIndex(values, "subtitleOffSelectedStreamIndex", out var selectedStreamIndex, out error))
        {
            return false;
        }

        outcome.Status = status;
        outcome.SelectedStreamIndex = selectedStreamIndex;
        return true;
    }

    private static bool TryParseSeekOutcome(
        Dictionary<string, string> values,
        out NativeHeadlessSeekOutcome outcome,
        out string error)
    {
        outcome = new NativeHeadlessSeekOutcome();
        if (!TryGetAttempted(values, "seekAttempted", out var attempted, out error))
        {
            return false;
        }

        outcome.Attempted = attempted;
        if (!attempted)
        {
            return true;
        }

        if (!TryGetInteractionStatus(values, "seekStatus", out var status, out error) ||
            !TryGetRequiredNonNegativeInt64(values, "seekTargetPositionTicks", out var targetPosition, out error) ||
            !TryGetRequiredNonNegativeInt64(values, "seekDemuxTargetTicks", out var demuxTarget, out error) ||
            !TryGetRequiredNullableNonNegativeInt64(values, "seekActualPositionTicks", out var actualPosition, out error) ||
            !TryGetRequiredNonNegativeInt64(values, "postSeekPlaybackPositionTicks", out var postSeekPosition, out error) ||
            !TryGetAttempted(values, "postSeekAdvanced", out var postSeekAdvanced, out error) ||
            !TryGetRequiredNonNegativeDouble(values, "seekOperationDurationMs", out var operationDurationMs, out error) ||
            !TryGetRequiredNonNegativeDouble(values, "seekRecoveryDurationMs", out var recoveryDurationMs, out error))
        {
            return false;
        }

        if (status == "completed" && !actualPosition.HasValue)
        {
            error = "Native helper field 'seekActualPositionTicks' must contain the first presented frame position when seekStatus is completed.";
            return false;
        }

        if (status == "completed" && !postSeekAdvanced)
        {
            error = "Native helper field 'postSeekAdvanced' must be 1 when seekStatus is completed.";
            return false;
        }

        outcome.Status = status;
        outcome.TargetPositionTicks = targetPosition;
        outcome.DemuxTargetTicks = demuxTarget;
        outcome.ActualPositionTicks = actualPosition;
        outcome.PostSeekPlaybackPositionTicks = postSeekPosition;
        outcome.PostSeekAdvanced = postSeekAdvanced;
        outcome.OperationDurationMs = operationDurationMs;
        outcome.RecoveryDurationMs = recoveryDurationMs;
        return true;
    }

    private static bool TryGetAttempted(
        Dictionary<string, string> values,
        string key,
        out bool attempted,
        out string error)
    {
        attempted = false;
        if (!values.TryGetValue(key, out var raw) || (raw != "0" && raw != "1"))
        {
            error = "Native helper field '" + key + "' must be present as 0 or 1.";
            return false;
        }

        attempted = raw == "1";
        error = "";
        return true;
    }

    private static bool TryGetInteractionStatus(
        Dictionary<string, string> values,
        string key,
        out string status,
        out string error)
    {
        status = "";
        if (!values.TryGetValue(key, out var raw) || (raw != "completed" && raw != "failed"))
        {
            error = "Native helper field '" + key + "' must be completed or failed for an attempted operation.";
            return false;
        }

        status = raw;
        error = "";
        return true;
    }

    private static bool TryGetRequiredNonNegativeInt32(
        Dictionary<string, string> values,
        string key,
        out int value,
        out string error)
    {
        value = 0;
        if (!values.TryGetValue(key, out var raw) || !int.TryParse(raw, out value) || value < 0)
        {
            error = "Native helper field '" + key + "' must be a non-negative integer.";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryGetRequiredInt64(
        Dictionary<string, string> values,
        string key,
        out long value,
        out string error)
    {
        if (!TryGetInt64(values, key, out value))
        {
            error = "Native helper field '" + key + "' must be a signed integer.";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryGetRequiredNonNegativeInt64(
        Dictionary<string, string> values,
        string key,
        out long value,
        out string error)
    {
        if (!TryGetRequiredInt64(values, key, out value, out error) || value < 0)
        {
            error = "Native helper field '" + key + "' must be a non-negative signed integer.";
            return false;
        }

        return true;
    }

    private static bool TryGetRequiredNullableNonNegativeInt64(
        Dictionary<string, string> values,
        string key,
        out long? value,
        out string error)
    {
        value = null;
        if (!values.TryGetValue(key, out var raw) || !long.TryParse(raw, out var parsed) || parsed < -1)
        {
            error = "Native helper field '" + key + "' must be -1 or a non-negative signed integer.";
            return false;
        }

        value = parsed >= 0 ? parsed : null;
        error = "";
        return true;
    }

    private static bool TryGetRequiredUInt64(
        Dictionary<string, string> values,
        string key,
        out ulong value,
        out string error)
    {
        if (!TryGetUInt64(values, key, out value))
        {
            error = "Native helper field '" + key + "' must be an unsigned integer.";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TrySetRequiredUInt64(
        Dictionary<string, string> values,
        string key,
        Action<ulong> setValue,
        out string error)
    {
        if (!TryGetRequiredUInt64(values, key, out var value, out error))
        {
            return false;
        }

        setValue(value);
        return true;
    }

    private static bool TrySetRequiredNonNegativeInt64(
        Dictionary<string, string> values,
        string key,
        Action<long> setValue,
        out string error)
    {
        if (!TryGetRequiredNonNegativeInt64(values, key, out var value, out error))
        {
            return false;
        }

        setValue(value);
        return true;
    }

    private static bool TrySetRequiredNonNegativeDouble(
        Dictionary<string, string> values,
        string key,
        Action<double> setValue,
        out string error)
    {
        if (!TryGetRequiredFiniteDouble(values, key, out var value, out error) || value < 0)
        {
            error = "Native helper field '" + key + "' must be a finite non-negative number.";
            return false;
        }

        setValue(value);
        return true;
    }

    private static bool TryGetRequiredNonNegativeDouble(
        Dictionary<string, string> values,
        string key,
        out double value,
        out string error)
    {
        if (!TryGetRequiredFiniteDouble(values, key, out value, out error) || value < 0)
        {
            error = "Native helper field '" + key + "' must be a finite non-negative number.";
            return false;
        }

        return true;
    }

    private static bool TrySetRequiredFiniteDouble(
        Dictionary<string, string> values,
        string key,
        Action<double> setValue,
        out string error)
    {
        if (!TryGetRequiredFiniteDouble(values, key, out var value, out error))
        {
            return false;
        }

        setValue(value);
        return true;
    }

    private static bool TryGetRequiredFiniteDouble(
        Dictionary<string, string> values,
        string key,
        out double value,
        out string error)
    {
        value = 0;
        if (!values.TryGetValue(key, out var raw) ||
            !double.TryParse(
                raw,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value) ||
            !double.IsFinite(value))
        {
            error = "Native helper field '" + key + "' must be a finite number.";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryGetRequiredNullableStreamIndex(
        Dictionary<string, string> values,
        string key,
        out int? streamIndex,
        out string error)
    {
        streamIndex = null;
        if (!values.TryGetValue(key, out var raw) || !int.TryParse(raw, out var parsed) || parsed < -1)
        {
            error = "Native helper field '" + key + "' must be -1 or a non-negative integer.";
            return false;
        }

        streamIndex = parsed >= 0 ? parsed : null;
        error = "";
        return true;
    }

    private static bool TryGetUInt64(
        Dictionary<string, string> values,
        string key,
        out ulong value)
    {
        value = 0;
        return values.TryGetValue(key, out var raw) &&
            ulong.TryParse(raw, out value);
    }

    private static ulong GetUInt64(
        Dictionary<string, string> values,
        string key)
    {
        return TryGetUInt64(values, key, out var value)
            ? value
            : 0;
    }

    private static bool TryGetInt64(
        Dictionary<string, string> values,
        string key,
        out long value)
    {
        value = 0;
        return values.TryGetValue(key, out var raw) &&
            long.TryParse(raw, out value);
    }

    private static long GetInt64(
        Dictionary<string, string> values,
        string key)
    {
        return TryGetInt64(values, key, out var value) ? value : 0;
    }

    private static int GetInt32(
        Dictionary<string, string> values,
        string key)
    {
        return values.TryGetValue(key, out var raw) &&
            int.TryParse(raw, out var value)
                ? value
                : 0;
    }

    private static string GetString(
        Dictionary<string, string> values,
        string key)
    {
        return values.TryGetValue(key, out var value)
            ? value
            : "";
    }

    private static double GetDouble(
        Dictionary<string, string> values,
        string key)
    {
        return values.TryGetValue(key, out var raw) &&
            double.TryParse(
                raw,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value)
                    ? value
                    : 0;
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
    }

    private static PlaybackDescriptor CreateDescriptor(
        string streamUrl,
        NativeHeadlessSourceInfo sourceInfo,
        int? selectedAudioStreamIndex,
        int? selectedSubtitleStreamIndex,
        long startPositionTicks)
    {
        var source = new EmbyMediaSource
        {
            Id = "native-headless-direct-uri",
            Name = "native-headless-direct-uri",
            DirectStreamUrl = streamUrl,
            Container = "mp4",
            RunTimeTicks = sourceInfo.LogicalDurationTicks,
            Width = sourceInfo.Width,
            Height = sourceInfo.Height,
            VideoFrameRate = sourceInfo.FrameRate,
            HdrProfile = HdrPlaybackProfile.Sdr()
        };
        if (sourceInfo.Tracks.Count > 0)
        {
            foreach (var track in sourceInfo.Tracks)
            {
                var streamKind = MapStreamKind(track.Kind);
                if (!streamKind.HasValue)
                {
                    continue;
                }

                source.Streams.Add(new EmbyMediaStream
                {
                    Index = track.Index,
                    Kind = streamKind.Value,
                    Codec = track.Codec,
                    Language = track.Language,
                    ChannelLayout = track.ChannelLayout,
                    Channels = track.Channels,
                    IsDefault = track.IsDefault,
                    IsForced = track.IsForced,
                    VideoRange = streamKind.Value == EmbyStreamKind.Video ? sourceInfo.VideoRange : "",
                    ColorPrimaries = streamKind.Value == EmbyStreamKind.Video ? sourceInfo.ColorPrimaries : "",
                    ColorTransfer = streamKind.Value == EmbyStreamKind.Video ? sourceInfo.ColorTransfer : "",
                    ColorSpace = streamKind.Value == EmbyStreamKind.Video ? sourceInfo.ColorSpace : "",
                    RealFrameRate = track.RealFrameRate,
                    AverageFrameRate = track.AverageFrameRate
                });
            }
        }

        if (source.Streams.Count == 0)
        {
            source.Streams.Add(new EmbyMediaStream
            {
                Index = 0,
                Kind = EmbyStreamKind.Video,
                Codec = sourceInfo.Codec,
                VideoRange = sourceInfo.VideoRange,
                ColorPrimaries = sourceInfo.ColorPrimaries,
                ColorTransfer = sourceInfo.ColorTransfer,
                ColorSpace = sourceInfo.ColorSpace,
                RealFrameRate = sourceInfo.FrameRate,
                AverageFrameRate = sourceInfo.FrameRate
            });
        }

        var selectedVideo = source.VideoStreams.FirstOrDefault();
        if (selectedVideo != null && string.IsNullOrWhiteSpace(selectedVideo.Codec))
        {
            selectedVideo.Codec = sourceInfo.Codec;
        }

        source.HdrProfile.Codec = sourceInfo.Codec;
        source.HdrProfile.VideoRange = sourceInfo.VideoRange;
        source.HdrProfile.ColorPrimaries = sourceInfo.ColorPrimaries;
        source.HdrProfile.ColorTransfer = sourceInfo.ColorTransfer;
        source.HdrProfile.ColorSpace = sourceInfo.ColorSpace;
        if (string.Equals(sourceInfo.HdrKind, "Hdr10", StringComparison.OrdinalIgnoreCase))
        {
            source.HdrProfile = new HdrPlaybackProfile
            {
                Kind = HdrPlaybackKind.Hdr10,
                Codec = sourceInfo.Codec,
                VideoRange = sourceInfo.VideoRange,
                ColorPrimaries = sourceInfo.ColorPrimaries,
                ColorTransfer = sourceInfo.ColorTransfer,
                ColorSpace = sourceInfo.ColorSpace
            };
        }
        else if (string.Equals(sourceInfo.HdrKind, "Hlg", StringComparison.OrdinalIgnoreCase))
        {
            source.HdrProfile = new HdrPlaybackProfile
            {
                Kind = HdrPlaybackKind.Hlg,
                Codec = sourceInfo.Codec,
                VideoRange = sourceInfo.VideoRange,
                ColorPrimaries = sourceInfo.ColorPrimaries,
                ColorTransfer = sourceInfo.ColorTransfer,
                ColorSpace = sourceInfo.ColorSpace
            };
        }
        else if (string.Equals(sourceInfo.HdrKind, "DolbyVisionUnsupported", StringComparison.OrdinalIgnoreCase))
        {
            source.HdrProfile.Kind = HdrPlaybackKind.DolbyVisionUnsupported;
        }
        else if (string.Equals(sourceInfo.HdrKind, "DolbyVisionWithHdr10Fallback", StringComparison.OrdinalIgnoreCase))
        {
            source.HdrProfile.Kind = HdrPlaybackKind.DolbyVisionWithHdr10Fallback;
        }
        else if (string.Equals(sourceInfo.HdrKind, "DolbyVisionWithHlgFallback", StringComparison.OrdinalIgnoreCase))
        {
            source.HdrProfile.Kind = HdrPlaybackKind.DolbyVisionWithHlgFallback;
        }

        source.HdrProfile.IsDolbyVision = sourceInfo.IsDolbyVision;
        source.HdrProfile.DolbyVisionProfile = sourceInfo.IsDolbyVision
            ? sourceInfo.DolbyVisionProfile
            : null;
        source.HdrProfile.DolbyVisionCompatibilityId = sourceInfo.IsDolbyVision
            ? sourceInfo.DolbyVisionCompatibilityId
            : null;
        source.HdrProfile.HasHdr10BaseLayer = sourceInfo.HasHdr10BaseLayer;
        source.HdrProfile.HasHlgBaseLayer = sourceInfo.HasHlgBaseLayer;

        selectedAudioStreamIndex ??= source.AudioStreams.FirstOrDefault()?.Index;
        return new PlaybackDescriptor(
            itemId: "",
            mediaSource: source,
            availableSources: new[] { source },
            startPositionTicks: startPositionTicks,
            audioStreamIndex: selectedAudioStreamIndex,
            subtitleStreamIndex: selectedSubtitleStreamIndex);
    }

    private static EmbyStreamKind? MapStreamKind(string kind)
    {
        if (string.Equals(kind, "Video", StringComparison.OrdinalIgnoreCase))
        {
            return EmbyStreamKind.Video;
        }

        if (string.Equals(kind, "Audio", StringComparison.OrdinalIgnoreCase))
        {
            return EmbyStreamKind.Audio;
        }

        if (string.Equals(kind, "Subtitle", StringComparison.OrdinalIgnoreCase))
        {
            return EmbyStreamKind.Subtitle;
        }

        return null;
    }

    private static void AddLifecycleEvent(
        PlaybackQualityLifecycle lifecycle,
        string operation,
        string status,
        long? positionTicks,
        string message = "")
    {
        lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = operation,
            Status = status,
            PositionTicks = positionTicks,
            Message = message
        });
    }

    private static void AddSubtitleSwitchLifecycleEvent(
        PlaybackQualityLifecycle lifecycle,
        NativeHeadlessSubtitleSwitchOutcome outcome)
    {
        if (!outcome.Attempted)
        {
            return;
        }

        AddLifecycleEvent(
            lifecycle,
            "subtitle-switch",
            outcome.Status,
            null,
            $"subtitle stream index {outcome.StreamIndex}; cue overlay render count " +
                $"{outcome.CueCountBefore}->{outcome.CueCountAfter}; selected subtitle stream index " +
                $"{FormatStreamIndex(outcome.SelectedStreamIndex)}; paused switch {(outcome.PausedSwitch ? 1 : 0)}; " +
                $"paused position {outcome.PausedPositionBeforeTicks}->{outcome.PausedPositionAfterTicks}; " +
                $"resumed position {outcome.PositionBeforeResumeTicks}->{outcome.PositionAfterResumeTicks}");
    }

    private static string FormatStreamIndex(int? streamIndex)
    {
        return streamIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";
    }

    private static void AddLimitation(
        PlaybackQualityRunResult result,
        string limitation)
    {
        if (!result.Report.Limitations.Contains(limitation))
        {
            result.Report.Limitations.Add(limitation);
        }
    }

    private static PlaybackQualityEnvironment CreateEnvironment()
    {
        return new PlaybackQualityEnvironment
        {
            CollectorVersion = CollectorVersion,
            PlayerCoreVersion = GetPlayerCoreVersion(),
            SourceRevision = GetSourceRevision(),
            BuildConfiguration = GetBuildConfiguration()
        };
    }

    public static NativeHeadlessHarnessResult CreateArgumentError(string error)
    {
        Console.Error.WriteLine(error);
        return new NativeHeadlessHarnessResult("", 2);
    }

    private static string GetReportPath(string reportsDir, string caseId)
    {
        var relativePath = PlaybackQualityCapturedReportPath.GetReportRelativePath(caseId);
        return Path.Combine(
            reportsDir,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string GetPlayerCoreVersion()
    {
        var assembly = typeof(PlaybackQualityRuntimeEvidenceCollector).Assembly;
        return assembly.GetName().Version?.ToString() ??
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            "NoiraPlayer.Core";
    }

    private static string GetSourceRevision()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("NOIRAPLAYER_SOURCE_REVISION");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment.Trim();
        }

        return TryRunGit("rev-parse --short HEAD") ?? "unknown-source-revision";
    }

    private static string GetBuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static string? TryRunGit(string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? output
                : null;
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class NativeHeadlessHarnessOptions
{
    public string CaseId { get; private set; } = "";
    public string StreamUrl { get; private set; } = "";
    public string SourceLocatorHash { get; private set; } = "";
    public int DurationSeconds { get; private set; } = 5;
    public long StartPositionTicks { get; private set; }
    public string ReportsDir { get; private set; } = "";
    public string NativeHelperExe { get; private set; } = "";
    public bool ForceSdrOutput { get; private set; }
    public int PauseSeconds { get; private set; }
    public string Scenario { get; private set; } = "playback";
    public int TimeoutSeconds { get; private set; } = 60;

    public static bool TryParse(
        string[] args,
        out NativeHeadlessHarnessOptions options,
        out string error)
    {
        options = new NativeHeadlessHarnessOptions();
        error = "";

        for (var index = 0; index < args.Length; index++)
        {
            var name = args[index];
            if (!name.StartsWith("--", StringComparison.Ordinal))
            {
                error = "Unexpected argument '" + name + "'.";
                return false;
            }

            if (name == "--force-sdr-output")
            {
                options.ForceSdrOutput = true;
                continue;
            }

            if (index + 1 >= args.Length)
            {
                error = "Missing value for argument '" + name + "'.";
                return false;
            }

            var value = args[++index];
            switch (name)
            {
                case "--case-id":
                    options.CaseId = value.Trim();
                    break;
                case "--stream-url":
                    options.StreamUrl = value.Trim();
                    break;
                case "--source-locator-hash":
                    options.SourceLocatorHash = value.Trim();
                    break;
                case "--duration-seconds":
                    if (!int.TryParse(value, out var durationSeconds) || durationSeconds <= 0)
                    {
                        error = "--duration-seconds must be a positive integer.";
                        return false;
                    }

                    options.DurationSeconds = durationSeconds;
                    break;
                case "--start-position-ticks":
                    if (!long.TryParse(value, out var startPositionTicks) || startPositionTicks < 0)
                    {
                        error = "--start-position-ticks must be a non-negative integer.";
                        return false;
                    }

                    options.StartPositionTicks = startPositionTicks;
                    break;
                case "--pause-seconds":
                    if (!int.TryParse(value, out var pauseSeconds) || pauseSeconds <= 0 || pauseSeconds > 900)
                    {
                        error = "--pause-seconds must be between 1 and 900.";
                        return false;
                    }

                    options.PauseSeconds = pauseSeconds;
                    break;
                case "--scenario":
                    if (value != "playback" &&
                        value != "timeline" &&
                        value != "audio-switch" &&
                        value != "subtitle-switch" &&
                        value != "pause-resume")
                    {
                        error = "--scenario must be playback, timeline, audio-switch, subtitle-switch, or pause-resume.";
                        return false;
                    }

                    options.Scenario = value;
                    break;
                case "--timeout-seconds":
                    if (!int.TryParse(value, out var timeoutSeconds) || timeoutSeconds <= 0 || timeoutSeconds > 1800)
                    {
                        error = "--timeout-seconds must be between 1 and 1800.";
                        return false;
                    }

                    options.TimeoutSeconds = timeoutSeconds;
                    break;
                case "--reports-dir":
                    options.ReportsDir = value.Trim();
                    break;
                case "--native-helper-exe":
                    options.NativeHelperExe = value.Trim();
                    break;
                default:
                    error = "Unknown argument '" + name + "'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(options.CaseId))
        {
            error = "--case-id is required.";
            return false;
        }

        if (!Uri.TryCreate(options.StreamUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFile))
        {
            error = "--stream-url must be an absolute http, https, or file URI.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.ReportsDir))
        {
            error = "--reports-dir is required.";
            return false;
        }

        if (options.TimeoutSeconds < options.DurationSeconds + options.PauseSeconds)
        {
            error = "--timeout-seconds must cover duration-seconds plus pause-seconds.";
            return false;
        }

        if ((options.Scenario == "pause-resume") != (options.PauseSeconds > 0))
        {
            error = "--scenario pause-resume requires --pause-seconds, and pause-seconds requires the pause-resume scenario.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.SourceLocatorHash))
        {
            options.SourceLocatorHash = PlaybackQualitySourceFingerprint.Compute(options.StreamUrl);
        }
        else if (!System.Text.RegularExpressions.Regex.IsMatch(
            options.SourceLocatorHash,
            "^sha256:[0-9a-f]{64}$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            error = "--source-locator-hash must be a lowercase SHA-256 fingerprint.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.NativeHelperExe) &&
            !File.Exists(options.NativeHelperExe))
        {
            error = "--native-helper-exe must point to an existing helper executable.";
            return false;
        }

        return true;
    }
}

internal sealed record NativeHeadlessHarnessResult(
    string ReportPath,
    int ExitCode);

internal sealed class NativeHeadlessHelperResult
{
    private NativeHeadlessHelperResult(
        int exitCode,
        PlaybackQualityMetricsSnapshot metrics,
        NativeHeadlessSourceInfo source,
        NativeHeadlessColorInfo color,
        NativeHeadlessDisplayInfo display,
        double processWallClockMs,
        double processCpuTimeMs,
        double processCpuUtilizationRatio,
        NativeHeadlessInteractionResults interactions,
        string errorMessage,
        string unsupportedCode = "")
    {
        ExitCode = exitCode;
        Metrics = metrics;
        Source = source;
        Color = color;
        Display = display;
        StartupDurationMs = metrics.NativeGraphOpenDurationMs > 0
            ? metrics.NativeGraphOpenDurationMs
            : processWallClockMs;
        ProcessWallClockMs = processWallClockMs;
        ProcessCpuTimeMs = processCpuTimeMs;
        ProcessCpuUtilizationRatio = processCpuUtilizationRatio;
        Interactions = interactions;
        ErrorMessage = errorMessage;
        UnsupportedCode = unsupportedCode;
        HasTelemetry = metrics.DecodedVideoFrames > 0 || metrics.RenderedVideoFrames > 0;
    }

    public int ExitCode { get; }

    public PlaybackQualityMetricsSnapshot Metrics { get; }

    public NativeHeadlessSourceInfo Source { get; }

    public NativeHeadlessColorInfo Color { get; }

    public NativeHeadlessDisplayInfo Display { get; }

    public double StartupDurationMs { get; }

    public double ProcessWallClockMs { get; }

    public double ProcessCpuTimeMs { get; }

    public double ProcessCpuUtilizationRatio { get; }

    public NativeHeadlessInteractionResults Interactions { get; }

    public NativeHeadlessAudioSwitchOutcome AudioSwitch => Interactions.AudioSwitch;

    public NativeHeadlessPauseResumeOutcome PauseResume => Interactions.PauseResume;

    public NativeHeadlessSubtitleSwitchOutcome SubtitleSwitch1 => Interactions.SubtitleSwitch1;

    public NativeHeadlessSubtitleSwitchOutcome SubtitleSwitch2 => Interactions.SubtitleSwitch2;

    public NativeHeadlessSubtitleOffOutcome SubtitleOff => Interactions.SubtitleOff;

    public NativeHeadlessSeekOutcome Seek => Interactions.Seek;

    public int? SelectedAudioStreamIndex => Interactions.SelectedAudioStreamIndex;

    public int? SelectedSubtitleStreamIndex => Interactions.SelectedSubtitleStreamIndex;

    public string ErrorMessage { get; }

    public bool HasTelemetry { get; }

    public string UnsupportedCode { get; }

    public bool IsUnsupported => !string.IsNullOrWhiteSpace(UnsupportedCode);

    public static NativeHeadlessHelperResult Succeeded(
        PlaybackQualityMetricsSnapshot metrics,
        NativeHeadlessSourceInfo source,
        NativeHeadlessColorInfo color,
        NativeHeadlessDisplayInfo display,
        double startupDurationMs,
        double processCpuTimeMs,
        double processCpuUtilizationRatio,
        NativeHeadlessInteractionResults interactions)
    {
        return new NativeHeadlessHelperResult(
            0,
            metrics,
            source,
            color,
            display,
            startupDurationMs,
            processCpuTimeMs,
            processCpuUtilizationRatio,
            interactions,
            "");
    }

    public static NativeHeadlessHelperResult Failed(string errorMessage)
    {
        return new NativeHeadlessHelperResult(
            1,
            new PlaybackQualityMetricsSnapshot(),
            new NativeHeadlessSourceInfo(),
            new NativeHeadlessColorInfo(),
            new NativeHeadlessDisplayInfo(),
            0,
            0,
            0,
            new NativeHeadlessInteractionResults(),
            errorMessage);
    }

    public static NativeHeadlessHelperResult FailedWithTelemetry(
        PlaybackQualityMetricsSnapshot metrics,
        NativeHeadlessSourceInfo source,
        NativeHeadlessColorInfo color,
        NativeHeadlessDisplayInfo display,
        double startupDurationMs,
        double processCpuTimeMs,
        double processCpuUtilizationRatio,
        NativeHeadlessInteractionResults interactions,
        string errorMessage)
    {
        return new NativeHeadlessHelperResult(
            1,
            metrics,
            source,
            color,
            display,
            startupDurationMs,
            processCpuTimeMs,
            processCpuUtilizationRatio,
            interactions,
            errorMessage);
    }

    public static NativeHeadlessHelperResult Unsupported(
        NativeHeadlessSourceInfo source,
        string unsupportedCode,
        double startupDurationMs,
        double processCpuTimeMs,
        double processCpuUtilizationRatio,
        string errorMessage)
    {
        return new NativeHeadlessHelperResult(
            3,
            new PlaybackQualityMetricsSnapshot(),
            source,
            new NativeHeadlessColorInfo(),
            new NativeHeadlessDisplayInfo(),
            startupDurationMs,
            processCpuTimeMs,
            processCpuUtilizationRatio,
            new NativeHeadlessInteractionResults(),
            errorMessage,
            unsupportedCode);
    }
}

internal sealed class NativeHeadlessInteractionResults
{
    public NativeHeadlessPauseResumeOutcome PauseResume { get; set; } =
        new NativeHeadlessPauseResumeOutcome();

    public NativeHeadlessAudioSwitchOutcome AudioSwitch { get; set; } =
        new NativeHeadlessAudioSwitchOutcome();

    public NativeHeadlessSubtitleSwitchOutcome SubtitleSwitch1 { get; set; } =
        new NativeHeadlessSubtitleSwitchOutcome();

    public NativeHeadlessSubtitleSwitchOutcome SubtitleSwitch2 { get; set; } =
        new NativeHeadlessSubtitleSwitchOutcome();

    public NativeHeadlessSubtitleOffOutcome SubtitleOff { get; set; } =
        new NativeHeadlessSubtitleOffOutcome();

    public NativeHeadlessSeekOutcome Seek { get; set; } =
        new NativeHeadlessSeekOutcome();

    public int? SelectedAudioStreamIndex { get; set; }

    public int? SelectedSubtitleStreamIndex { get; set; }
}

internal sealed class NativeHeadlessPauseResumeOutcome
{
    public bool Attempted { get; set; }
    public int DurationSeconds { get; set; }
    public string Status { get; set; } = "";
    public long PositionBeforePauseTicks { get; set; }
    public long PositionAfterResumeTicks { get; set; }
    public ulong PostResumeDecodedVideoFrames { get; set; }
    public ulong PostResumeRenderedVideoFrames { get; set; }
    public bool PlaybackFailed { get; set; }
}

internal sealed class NativeHeadlessAudioSwitchOutcome
{
    public bool Attempted { get; set; }

    public string Status { get; set; } = "";

    public int StreamIndex { get; set; } = -1;

    public long PositionBeforeTicks { get; set; }

    public long PositionAfterTicks { get; set; }

    public ulong SubmittedFramesBefore { get; set; }

    public ulong SubmittedFramesAfter { get; set; }

    public double OperationDurationMs { get; set; }

    public double LockWaitDurationMs { get; set; }

    public double ExecutionDurationMs { get; set; }

    public double QuiesceDurationMs { get; set; }

    public double SeekDurationMs { get; set; }

    public double DecoderOpenDurationMs { get; set; }

    public double RendererOpenDurationMs { get; set; }

    public bool PacketCacheHit { get; set; }

    public bool PacketCacheEnabled { get; set; }

    public ulong PacketCachePacketCount { get; set; }

    public ulong PacketCacheBytes { get; set; }

    public long PacketCacheWindowDurationTicks { get; set; }

    public double RecoveryDurationMs { get; set; }
}

internal sealed class NativeHeadlessSubtitleSwitchOutcome
{
    public bool Attempted { get; set; }

    public string Status { get; set; } = "";

    public int StreamIndex { get; set; } = -1;

    public ulong CueCountBefore { get; set; }

    public ulong CueCountAfter { get; set; }

    public bool PausedSwitch { get; set; }

    public int? SelectedStreamIndex { get; set; }

    public long PausedPositionBeforeTicks { get; set; }

    public long PausedPositionAfterTicks { get; set; }

    public long PositionBeforeResumeTicks { get; set; }

    public long PositionAfterResumeTicks { get; set; }

    public double OperationDurationMs { get; set; }

    public double LockWaitDurationMs { get; set; }

    public double ExecutionDurationMs { get; set; }

    public double QuiesceDurationMs { get; set; }

    public double SeekDurationMs { get; set; }

    public double DecoderOpenDurationMs { get; set; }

    public double RendererOpenDurationMs { get; set; }

    public bool PacketCacheHit { get; set; }

    public bool PacketCacheEnabled { get; set; }

    public ulong PacketCachePacketCount { get; set; }

    public ulong PacketCacheBytes { get; set; }

    public long PacketCacheWindowDurationTicks { get; set; }

    public double RecoveryDurationMs { get; set; }

    public double CueRenderDurationMs { get; set; }

    public ulong RenderedFramesBefore { get; set; }

    public ulong RenderedFramesAfter { get; set; }
}

internal sealed class NativeHeadlessSubtitleOffOutcome
{
    public bool Attempted { get; set; }

    public string Status { get; set; } = "";

    public int? SelectedStreamIndex { get; set; }
}

internal sealed class NativeHeadlessSeekOutcome
{
    public bool Attempted { get; set; }

    public string Status { get; set; } = "";

    public long TargetPositionTicks { get; set; }

    public long DemuxTargetTicks { get; set; }

    public long? ActualPositionTicks { get; set; }

    public long PostSeekPlaybackPositionTicks { get; set; }

    public bool PostSeekAdvanced { get; set; }

    public double OperationDurationMs { get; set; }

    public double RecoveryDurationMs { get; set; }
}

internal sealed class NativeHeadlessSourceInfo
{
    public string Codec { get; set; } = "";

    public int Width { get; set; }

    public int Height { get; set; }

    public double FrameRate { get; set; }

    public string HdrKind { get; set; } = "";

    public string VideoRange { get; set; } = "";

    public string ColorPrimaries { get; set; } = "";

    public string ColorTransfer { get; set; } = "";

    public string ColorSpace { get; set; } = "";

    public bool IsDolbyVision { get; set; }

    public int DolbyVisionProfile { get; set; }

    public int DolbyVisionCompatibilityId { get; set; }

    public bool HasHdr10BaseLayer { get; set; }

    public bool HasHlgBaseLayer { get; set; }

    public long ContainerStartTimeTicks { get; set; }

    public long VideoStreamStartTimeTicks { get; set; }

    public long LogicalDurationTicks { get; set; }

    public List<NativeHeadlessTrackInfo> Tracks { get; } =
        new List<NativeHeadlessTrackInfo>();
}

internal sealed class NativeHeadlessTrackInfo
{
    public int Index { get; set; }

    public string Kind { get; set; } = "";

    public string Codec { get; set; } = "";

    public string Language { get; set; } = "";

    public string ChannelLayout { get; set; } = "";

    public int Channels { get; set; }

    public bool IsDefault { get; set; }

    public bool IsForced { get; set; }

    public double RealFrameRate { get; set; }

    public double AverageFrameRate { get; set; }
}

internal sealed class NativeHeadlessColorInfo
{
    public string DxgiInput { get; set; } = "";

    public string DxgiOutput { get; set; } = "";

    public string ConversionStatus { get; set; } = "";

    public bool IsVideoProcessorColorSpaceValidated { get; set; }
}

internal sealed class NativeHeadlessDisplayInfo
{
    public double RefreshRateHz { get; set; }

    public string RefreshPolicy { get; set; } = "";
}

internal sealed class NativeHeadlessMetricsProvider :
    IPlaybackQualityMetricsProvider,
    IPlaybackQualityMetricsProviderIdentity
{
    private readonly PlaybackQualityMetricsSnapshot _metrics;

    public NativeHeadlessMetricsProvider(PlaybackQualityMetricsSnapshot metrics)
    {
        _metrics = metrics;
    }

    public string PlaybackQualityMetricsProviderId => "native-headless";

    public bool TryGetQualityMetrics(out PlaybackQualityMetricsSnapshot metrics)
    {
        metrics = _metrics;
        return true;
    }
}

internal sealed class NativeHeadlessDiagnostics : IPlaybackBackendDiagnostics
{
    public NativeHeadlessDiagnostics(
        NativeHeadlessColorInfo color,
        NativeHeadlessDisplayInfo display)
    {
        var message = "native-headless offscreen composition swapchain";
        if (!string.IsNullOrWhiteSpace(display.RefreshPolicy))
        {
            message += "; display refresh policy=" + display.RefreshPolicy;
        }

        DisplayStatus = new PlaybackDisplayStatus(
            HdrOutputStatus.Off,
            isHdrDisplayAvailable: false,
            isHdrOutputActive: false,
            message: message,
            swapChainFormat: "DXGI_FORMAT_B8G8R8A8_UNORM",
            swapChainColorSpace: "DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709",
            isTenBitSwapChain: false,
            isVideoProcessorColorSpaceValidated: color.IsVideoProcessorColorSpaceValidated,
            videoProcessorInputColorSpace: color.DxgiInput,
            videoProcessorOutputColorSpace: color.DxgiOutput,
            videoProcessorConversionStatus: string.IsNullOrWhiteSpace(color.ConversionStatus)
                ? "missing-native-helper-conversion-status"
                : color.ConversionStatus,
            refreshRateHz: display.RefreshRateHz);
    }

    public PlaybackBackendCapabilities Capabilities { get; } =
        new PlaybackBackendCapabilities(
            PlaybackBackendFeature.DirectPlayHttp |
            PlaybackBackendFeature.Hevc |
            PlaybackBackendFeature.HevcMain10 |
            PlaybackBackendFeature.NativeAudioOutput);

    public PlaybackDisplayStatus DisplayStatus { get; }
}
