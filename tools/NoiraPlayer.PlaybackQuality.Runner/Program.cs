using System.Reflection;
using System.Text;
using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.PlaybackQuality;

namespace NoiraPlayer.PlaybackQuality.Runner;

internal static class Program
{
    private const string CollectorVersion = "native-manifest-runner-v0.1";
    private const string ServerUrlVariable = "NOIRAPLAYER_QA_SERVER_URL";
    private const string UsernameVariable = "NOIRAPLAYER_QA_USERNAME";
    private const string PasswordVariable = "NOIRAPLAYER_QA_PASSWORD";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("command-invalid");
        }

        if (string.Equals(args[0], "write-source-resolution-error", StringComparison.Ordinal))
        {
            return WriteSourceResolutionError(args);
        }

        if (!string.Equals(args[0], "resolve-emby-source", StringComparison.Ordinal))
        {
            return Fail("command-invalid");
        }

        if (!TryReadOption(args, "--item-id", out var itemId) ||
            string.IsNullOrWhiteSpace(itemId))
        {
            return Fail("item-id-missing");
        }

        TryReadOption(args, "--media-source-id", out var mediaSourceId);
        var serverUrl = Environment.GetEnvironmentVariable(ServerUrlVariable) ?? "";
        var username = Environment.GetEnvironmentVariable(UsernameVariable) ?? "";
        var password = Environment.GetEnvironmentVariable(PasswordVariable) ?? "";
        if (string.IsNullOrWhiteSpace(serverUrl) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return Fail("credentials-missing");
        }

        try
        {
            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            var client = new EmbyApiClient(
                http,
                new EmbyClientOptions
                {
                    ServerUrl = serverUrl,
                    ClientName = "Noira QA",
                    ClientVersion = "0.1.0",
                    DeviceName = "Playback Quality Runner",
                    DeviceId = "noira-playback-quality-runner"
                });
            var session = await client.AuthenticateAsync(username, password).ConfigureAwait(false);
            var sources = await client.GetPlaybackInfoAsync(session, itemId, mediaSourceId).ConfigureAwait(false);
            var selected = string.IsNullOrWhiteSpace(mediaSourceId)
                ? sources.FirstOrDefault(source => !string.IsNullOrWhiteSpace(source.DirectStreamUrl))
                : sources.FirstOrDefault(source =>
                    string.Equals(source.Id, mediaSourceId, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(source.DirectStreamUrl));
            if (selected == null)
            {
                return Fail("media-source-not-found");
            }

            if (!Uri.TryCreate(selected.DirectStreamUrl, UriKind.Absolute, out var streamUri) ||
                (streamUri.Scheme != Uri.UriSchemeHttp && streamUri.Scheme != Uri.UriSchemeHttps))
            {
                return Fail("direct-stream-url-invalid");
            }

            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(selected.DirectStreamUrl));
            Console.Out.WriteLine("resolved-source-base64:" + encoded);
            return 0;
        }
        catch
        {
            return Fail("emby-request-failed");
        }
    }

    private static int WriteSourceResolutionError(string[] args)
    {
        if (!TryReadOption(args, "--case-id", out var caseId) ||
            string.IsNullOrWhiteSpace(caseId) ||
            !TryReadOption(args, "--source-locator", out var sourceLocator) ||
            string.IsNullOrWhiteSpace(sourceLocator) ||
            !TryReadOption(args, "--reports-dir", out var reportsDir) ||
            string.IsNullOrWhiteSpace(reportsDir) ||
            !TryReadOption(args, "--error-code", out var errorCode) ||
            string.IsNullOrWhiteSpace(errorCode))
        {
            return Fail("error-report-options-invalid");
        }

        try
        {
            var startedAt = DateTimeOffset.UtcNow;
            var referenceCase = new PlaybackQualityReferenceCase
            {
                CaseId = caseId.Trim(),
                Uri = sourceLocator.Trim(),
                Category = "stable",
                Severity = "high",
                Stability = "stable"
            };
            referenceCase.Purpose.Add("error-handling");
            var runResult = PlaybackQualityRuntimeEvidenceCollector.ComposeErrorRunResult(
                referenceCase,
                new PlaybackQualityError
                {
                    Code = "manifest-runner.source-resolution-failed",
                    Message = "The private Emby source could not be resolved: " + SanitizeErrorCode(errorCode) + ".",
                    Operation = "resolve-emby-source",
                    ExceptionType = "source-resolution",
                    FailureClass = PlaybackQualityFailureClassification.ExternalServiceOrProtocolIssue,
                    FailureArea = "unsupported-source",
                    IsTerminal = true,
                    IsRetriable = true
                },
                CreateEnvironment(),
                execution: new PlaybackQualityExecutionEvidence
                {
                    AttemptId = Guid.NewGuid().ToString("N"),
                    Runner = "native-manifest-runner",
                    EvidenceLevel = PlaybackQualityEvidenceLevel.Orchestration,
                    Status = PlaybackQualityExecutionStatus.Failed,
                    SourceLocatorHash = PlaybackQualitySourceFingerprint.Compute(sourceLocator.Trim()),
                    OpenedSourceHash = "",
                    StartedAtUtc = startedAt.ToString("O"),
                    DurationMs = Math.Max(0, (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                    SourceOpenAttempted = false,
                    SourceOpened = false,
                    NativeGraphOpened = false,
                    DemuxStarted = false,
                    DecoderOpened = false,
                    PlaybackSampleObserved = false
                });

            var relativePath = PlaybackQualityCapturedReportPath.GetReportRelativePath(caseId.Trim());
            var reportPath = Path.GetFullPath(Path.Combine(reportsDir, relativePath));
            var reportsRoot = Path.GetFullPath(reportsDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!reportPath.StartsWith(reportsRoot, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("report-path-invalid");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? reportsRoot);
            File.WriteAllText(reportPath, PlaybackQualityReportSerializer.Serialize(runResult));
            Console.Out.WriteLine("report-written");
            return 0;
        }
        catch
        {
            return Fail("error-report-write-failed");
        }
    }

    private static PlaybackQualityEnvironment CreateEnvironment()
    {
        var assembly = typeof(PlaybackQualityRuntimeEvidenceCollector).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "";
        var revision = Environment.GetEnvironmentVariable("NOIRAPLAYER_SOURCE_REVISION") ?? "";
        if (string.IsNullOrWhiteSpace(revision))
        {
            var separator = informationalVersion.LastIndexOf('+');
            revision = separator >= 0 && separator + 1 < informationalVersion.Length
                ? informationalVersion[(separator + 1)..]
                : "unknown-source-revision";
        }

        return new PlaybackQualityEnvironment
        {
            CollectorVersion = CollectorVersion,
            PlayerCoreVersion = assembly.GetName().Version?.ToString() ?? informationalVersion,
            SourceRevision = revision,
#if DEBUG
            BuildConfiguration = "Debug"
#else
            BuildConfiguration = "Release"
#endif
        };
    }

    private static string SanitizeErrorCode(string value)
    {
        var normalized = new string(value.Trim()
            .Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_')
            .Take(80)
            .ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "resolver-failed" : normalized;
    }

    private static bool TryReadOption(string[] args, string name, out string value)
    {
        value = "";
        for (var index = 1; index < args.Length; index++)
        {
            if (!string.Equals(args[index], name, StringComparison.Ordinal))
            {
                continue;
            }

            if (index + 1 >= args.Length)
            {
                return false;
            }

            value = args[index + 1].Trim();
            return true;
        }

        return false;
    }

    private static int Fail(string code)
    {
        Console.Error.WriteLine("resolver-error:" + code);
        return 2;
    }
}
