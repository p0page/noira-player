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
    private const long NativeHelperSeekTargetPositionTicks = 0;
    private const string NativeWinRtLinkageLimitation =
        "native-headless: current NoiraPlayer.Native build is a Windows Store C++/WinRT component with public playback entrypoints bound to UWP projection";
    private const string NativeGraphHostLimitation =
        "native-headless: offscreen DirectX composition swapchain is smoke-tested, but this runner still lacks a native PlaybackGraph host and lifecycle bridge";

    public static NativeHeadlessHarnessResult Run(NativeHeadlessHarnessOptions options)
    {
        var referenceCase = PlaybackQualityCaptureReferenceCaseFactory.Create(
            options.CaseId,
            itemId: "",
            mediaSourceId: "",
            startPositionTicks: 0,
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
            });

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
        var helper = RunHelperProcess(options);
        var playbackStartedAt = DateTimeOffset.UtcNow;
        var reportPath = GetReportPath(options.ReportsDir, options.CaseId);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? options.ReportsDir);

        if (helper.ExitCode != 0)
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
                CreateEnvironment());
            File.WriteAllText(reportPath, PlaybackQualityReportSerializer.Serialize(errorResult));
            return new NativeHeadlessHarnessResult(reportPath, 1);
        }

        var descriptor = CreateDescriptor(options.StreamUrl, helper.Source);
        var diagnostics = new NativeHeadlessDiagnostics(helper.Color, helper.Display);
        var provider = new NativeHeadlessMetricsProvider(helper.Metrics);
        var lifecycle = new PlaybackQualityLifecycle();
        AddLifecycleEvent(lifecycle, "load", "completed", 0);
        AddLifecycleEvent(lifecycle, "play", "completed", 0);
        AddLifecycleEvent(lifecycle, "pause", "completed", helper.Metrics.VideoPositionTicks);
        AddLifecycleEvent(lifecycle, "resume", "completed", helper.Metrics.VideoPositionTicks);
        AddLifecycleEvent(lifecycle, "seek", "completed", helper.SeekActualPositionTicks);
        AddLifecycleEvent(lifecycle, "stop", "completed", helper.SeekActualPositionTicks);

        var request = PlaybackQualityRuntimeEvidenceCollector.CreateRequest(
            referenceCase,
            descriptor,
            diagnostics,
            provider,
            new PlaybackQualityStartup
            {
                CommandReceivedAt = commandReceivedAt.ToString("O"),
                PlaybackStartedAt = playbackStartedAt.ToString("O"),
                StartupDurationMs = helper.StartupDurationMs
            },
            CreateEnvironment(),
            lifecycle,
            new PlaybackQualityPosition
            {
                RequestedStartPositionTicks = 0,
                SeekTargetPositionTicks = NativeHelperSeekTargetPositionTicks,
                ActualPositionTicks = helper.SeekActualPositionTicks,
                SeekPositionErrorMs = helper.SeekActualPositionTicks >= NativeHelperSeekTargetPositionTicks
                    ? Math.Abs(helper.SeekActualPositionTicks - NativeHelperSeekTargetPositionTicks) / 10000.0
                    : null
            });
        if (request.RuntimeMetrics != null)
        {
            request.RuntimeMetrics.ProcessWallClockMs = helper.ProcessWallClockMs;
            request.RuntimeMetrics.ProcessCpuTimeMs = helper.ProcessCpuTimeMs;
            request.RuntimeMetrics.ProcessCpuUtilizationRatio = helper.ProcessCpuUtilizationRatio;
        }

        var runResult = PlaybackQualityReportComposer.Compose(request);

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
        return new NativeHeadlessHarnessResult(reportPath, 0);
    }

    private static NativeHeadlessHelperResult RunHelperProcess(NativeHeadlessHarnessOptions options)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = options.NativeHelperExe,
            Arguments = "--stream-url " + QuoteArgument(options.StreamUrl) +
                " --duration-seconds " + options.DurationSeconds.ToString(),
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
        var exited = process.WaitForExit(Math.Max(15, options.DurationSeconds + 15) * 1000);
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

            return NativeHeadlessHelperResult.Failed(
                "Native helper timed out before returning playback metrics.");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            return NativeHeadlessHelperResult.Failed(
                FirstNonEmpty(stderr.Trim(), stdout.Trim(), "Native helper exited with code " + process.ExitCode + "."));
        }

        if (!TryParseMetrics(
            stdout,
            out var metrics,
            out var source,
            out var color,
            out var display,
            out var seekActualPositionTicks,
            out var parseError))
        {
            return NativeHeadlessHelperResult.Failed(parseError);
        }

        return NativeHeadlessHelperResult.Succeeded(
            metrics,
            source,
            color,
            display,
            processWallClockMs,
            processCpuTimeMs,
            processCpuUtilizationRatio,
            seekActualPositionTicks);
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
        out long seekActualPositionTicks,
        out string error)
    {
        metrics = new PlaybackQualityMetricsSnapshot();
        source = new NativeHeadlessSourceInfo();
        color = new NativeHeadlessColorInfo();
        display = new NativeHeadlessDisplayInfo();
        seekActualPositionTicks = 0;
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

        if (!TryGetUInt64(values, "decodedVideoFrames", out var decodedVideoFrames) ||
            !TryGetUInt64(values, "renderedVideoFrames", out var renderedVideoFrames))
        {
            error = "Native helper did not return decoded/rendered frame metrics.";
            return false;
        }

        metrics.DecodedVideoFrames = decodedVideoFrames;
        metrics.HardwareDecodedVideoFrames = GetUInt64(values, "hardwareDecodedVideoFrames");
        metrics.SoftwareDecodedVideoFrames = GetUInt64(values, "softwareDecodedVideoFrames");
        metrics.RenderedVideoFrames = renderedVideoFrames;
        metrics.RenderPasses = GetUInt64(values, "renderPasses");
        metrics.SubmittedAudioFrames = GetUInt64(values, "submittedAudioFrames");
        metrics.QueuedAudioBuffers = GetUInt64(values, "queuedAudioBuffers");
        metrics.DroppedVideoFrames = GetUInt64(values, "droppedVideoFrames");
        metrics.SeekPrerollDroppedFrames = GetUInt64(values, "seekPrerollDroppedFrames");
        metrics.VideoAheadWaitCount = GetUInt64(values, "videoAheadWaitCount");
        metrics.AudioAheadWaitCount = GetUInt64(values, "audioAheadWaitCount");
        metrics.VideoClockWaitCount = GetUInt64(values, "videoClockWaitCount");
        metrics.VideoStarvedPasses = GetUInt64(values, "videoStarvedPasses");
        metrics.AudioStarvedPasses = GetUInt64(values, "audioStarvedPasses");
        metrics.AudioClockTicks = GetInt64(values, "audioClockTicks");
        metrics.VideoPositionTicks = GetInt64(values, "videoPositionTicks");
        seekActualPositionTicks = values.ContainsKey("seekActualPositionTicks")
            ? GetInt64(values, "seekActualPositionTicks")
            : metrics.VideoPositionTicks;
        metrics.RenderIntervalMsP50 = GetDouble(values, "renderIntervalMsP50");
        metrics.RenderIntervalMsP95 = GetDouble(values, "renderIntervalMsP95");
        metrics.RenderIntervalMsP99 = GetDouble(values, "renderIntervalMsP99");
        metrics.MaxFrameGapMs = GetDouble(values, "maxFrameGapMs");
        metrics.RenderIntervalSampleCount = GetUInt64(values, "renderIntervalSampleCount");
        metrics.RenderIntervalOverExpected2MsCount = GetUInt64(values, "renderIntervalOverExpected2MsCount");
        metrics.RenderIntervalOverExpected4MsCount = GetUInt64(values, "renderIntervalOverExpected4MsCount");
        metrics.PresentDurationMsP50 = GetDouble(values, "presentDurationMsP50");
        metrics.PresentDurationMsP95 = GetDouble(values, "presentDurationMsP95");
        metrics.PresentDurationMsP99 = GetDouble(values, "presentDurationMsP99");
        metrics.PresentDurationMsMax = GetDouble(values, "presentDurationMsMax");
        metrics.AudioAheadWaitDurationMsP50 = GetDouble(values, "audioAheadWaitDurationMsP50");
        metrics.AudioAheadWaitDurationMsP95 = GetDouble(values, "audioAheadWaitDurationMsP95");
        metrics.AudioAheadWaitDurationMsP99 = GetDouble(values, "audioAheadWaitDurationMsP99");
        metrics.AudioAheadWaitDurationMsMax = GetDouble(values, "audioAheadWaitDurationMsMax");
        metrics.AudioAheadWaitTargetMsP50 = GetDouble(values, "audioAheadWaitTargetMsP50");
        metrics.AudioAheadWaitTargetMsP95 = GetDouble(values, "audioAheadWaitTargetMsP95");
        metrics.AudioAheadWaitTargetMsP99 = GetDouble(values, "audioAheadWaitTargetMsP99");
        metrics.AudioAheadWaitTargetMsMax = GetDouble(values, "audioAheadWaitTargetMsMax");
        metrics.AudioAheadWaitOversleepMsP50 = GetDouble(values, "audioAheadWaitOversleepMsP50");
        metrics.AudioAheadWaitOversleepMsP95 = GetDouble(values, "audioAheadWaitOversleepMsP95");
        metrics.AudioAheadWaitOversleepMsP99 = GetDouble(values, "audioAheadWaitOversleepMsP99");
        metrics.AudioAheadWaitOversleepMsMax = GetDouble(values, "audioAheadWaitOversleepMsMax");
        metrics.FramePacingSourceFrameRate = GetDouble(values, "framePacingSourceFrameRate");
        metrics.LateFrameDropToleranceMs = GetDouble(values, "lateFrameDropToleranceMs");
        metrics.AudioVideoDriftMsP50 = GetDouble(values, "audioVideoDriftMsP50");
        metrics.AudioVideoDriftMsP95 = GetDouble(values, "audioVideoDriftMsP95");
        metrics.AudioVideoDriftMsP99 = GetDouble(values, "audioVideoDriftMsP99");
        metrics.AudioVideoDriftMsMax = GetDouble(values, "audioVideoDriftMsMax");
        source = new NativeHeadlessSourceInfo
        {
            Codec = GetString(values, "sourceCodec"),
            Width = GetInt32(values, "sourceWidth"),
            Height = GetInt32(values, "sourceHeight"),
            FrameRate = GetDouble(values, "sourceFrameRate"),
            HdrKind = GetString(values, "sourceHdrKind"),
            VideoRange = GetString(values, "sourceVideoRange"),
            ColorPrimaries = GetString(values, "sourceColorPrimaries"),
            ColorTransfer = GetString(values, "sourceColorTransfer"),
            ColorSpace = GetString(values, "sourceColorSpace")
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

    private static long GetInt64(
        Dictionary<string, string> values,
        string key)
    {
        return values.TryGetValue(key, out var raw) &&
            long.TryParse(raw, out var value)
                ? value
                : 0;
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
        NativeHeadlessSourceInfo sourceInfo)
    {
        var source = new EmbyMediaSource
        {
            Id = "native-headless-direct-uri",
            Name = "native-headless-direct-uri",
            DirectStreamUrl = streamUrl,
            Container = "mp4",
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

        var selectedAudioStreamIndex = source.AudioStreams.FirstOrDefault()?.Index;
        return new PlaybackDescriptor(
            itemId: "",
            mediaSource: source,
            availableSources: new[] { source },
            startPositionTicks: 0,
            audioStreamIndex: selectedAudioStreamIndex,
            subtitleStreamIndex: null);
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
        long? positionTicks)
    {
        lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
        {
            Operation = operation,
            Status = status,
            PositionTicks = positionTicks
        });
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
    public int DurationSeconds { get; private set; } = 5;
    public string ReportsDir { get; private set; } = "";
    public string NativeHelperExe { get; private set; } = "";
    public bool ForceSdrOutput { get; private set; }

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
                case "--duration-seconds":
                    if (!int.TryParse(value, out var durationSeconds) || durationSeconds <= 0)
                    {
                        error = "--duration-seconds must be a positive integer.";
                        return false;
                    }

                    options.DurationSeconds = durationSeconds;
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
        double startupDurationMs,
        double processCpuTimeMs,
        double processCpuUtilizationRatio,
        long seekActualPositionTicks,
        string errorMessage)
    {
        ExitCode = exitCode;
        Metrics = metrics;
        Source = source;
        Color = color;
        Display = display;
        StartupDurationMs = startupDurationMs;
        ProcessWallClockMs = startupDurationMs;
        ProcessCpuTimeMs = processCpuTimeMs;
        ProcessCpuUtilizationRatio = processCpuUtilizationRatio;
        SeekActualPositionTicks = seekActualPositionTicks;
        ErrorMessage = errorMessage;
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

    public long SeekActualPositionTicks { get; }

    public string ErrorMessage { get; }

    public static NativeHeadlessHelperResult Succeeded(
        PlaybackQualityMetricsSnapshot metrics,
        NativeHeadlessSourceInfo source,
        NativeHeadlessColorInfo color,
        NativeHeadlessDisplayInfo display,
        double startupDurationMs,
        double processCpuTimeMs,
        double processCpuUtilizationRatio,
        long seekActualPositionTicks)
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
            seekActualPositionTicks,
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
            0,
            errorMessage);
    }
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
