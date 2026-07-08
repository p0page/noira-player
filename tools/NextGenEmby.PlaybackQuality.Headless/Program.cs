using System.Diagnostics;
using System.Reflection;
using NextGenEmby.Core.PlaybackQuality;

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
    private const string NativeWinRtLinkageLimitation =
        "native-headless: current NextGenEmby.Native build is a Windows Store C++/WinRT component with public playback entrypoints bound to UWP projection";
    private const string NativeGraphHostLimitation =
        "native-headless: real App-free playback evidence requires a native graph host or render-surface abstraction before this runner can open PlaybackGraph";

    public static NativeHeadlessHarnessResult Run(NativeHeadlessHarnessOptions options)
    {
        var referenceCase = PlaybackQualityCaptureReferenceCaseFactory.Create(
            options.CaseId,
            itemId: "",
            mediaSourceId: "",
            startPositionTicks: 0,
            forceSdrOutput: false,
            expected: new PlaybackQualityExpected(),
            uri: options.StreamUrl,
            category: "stable",
            severity: "high",
            stability: "stable");
        referenceCase.Purpose.Add("sdr-smoke");
        referenceCase.Purpose.Add("frame-pacing");

        var runResult = PlaybackQualityRuntimeEvidenceCollector.ComposeSkipRunResult(
            referenceCase,
            new PlaybackQualitySkip
            {
                Code = "native-headless.native-link-blocked",
                Reason = "Current NextGenEmby.Native build is a Windows Store C++/WinRT component whose public playback entrypoint is projected through UWP and exposes AttachSurface(SwapChainPanel); App-free native open needs a native graph host or render-surface abstraction before this runner can open PlaybackGraph.",
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
            "NextGenEmby.Core";
    }

    private static string GetSourceRevision()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("NEXTGENEMBY_SOURCE_REVISION");
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

        return true;
    }
}

internal sealed record NativeHeadlessHarnessResult(
    string ReportPath,
    int ExitCode);
