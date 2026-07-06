using System.Text.Json;
using System.Text.Json.Serialization;
using NextGenEmby.Core.PlaybackQuality;

namespace NextGenEmby.PlaybackQuality.Cli;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        WriteIndented = true
    };

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                WriteUsage(Console.Out);
                return args.Length == 0 ? 1 : 0;
            }

            if (string.Equals(args[0], "compare", StringComparison.OrdinalIgnoreCase))
            {
                return RunCompare(args);
            }

            if (string.Equals(args[0], "summarize", StringComparison.OrdinalIgnoreCase))
            {
                return RunSummarize(args);
            }

            if (string.Equals(args[0], "compare-suite", StringComparison.OrdinalIgnoreCase))
            {
                return RunCompareSuite(args);
            }

            if (string.Equals(args[0], "validate-manifest", StringComparison.OrdinalIgnoreCase))
            {
                return RunValidateManifest(args);
            }

            if (string.Equals(args[0], "validate-report-set", StringComparison.OrdinalIgnoreCase))
            {
                return RunValidateReportSet(args);
            }

            throw new ArgumentException("Unknown command: " + args[0]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static int RunCompare(string[] args)
    {
        var options = ParseCompareOptions(args);
        var baseline = ReadPlaybackQualityReport(options.BaselinePath);
        var candidate = ReadPlaybackQualityReport(options.CandidatePath);
        var context = new PlaybackQualityComparisonContext
        {
            StallComparisonCountThreshold = options.StallComparisonCountThreshold
        };

        foreach (var previousPath in options.PreviousComparisonPaths)
        {
            context.PreviousComparisons.Add(ReadJson<PlaybackQualityRunComparison>(previousPath));
        }

        var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate, context);
        WriteJson(comparison, options.OutputPath);
        return 0;
    }

    private static int RunSummarize(string[] args)
    {
        var options = ParseSummarizeOptions(args);
        var comparisons = new List<PlaybackQualityRunComparison>();
        foreach (var comparisonPath in options.ComparisonPaths)
        {
            comparisons.Add(ReadJson<PlaybackQualityRunComparison>(comparisonPath));
        }

        WriteJson(PlaybackQualityComparisonSuiteAggregator.Summarize(comparisons), options.OutputPath);
        return 0;
    }

    private static int RunCompareSuite(string[] args)
    {
        var options = ParseCompareSuiteOptions(args);
        var reportPairs = FindReportPairs(options);
        var comparisons = new List<PlaybackQualityRunComparison>();

        foreach (var pair in reportPairs)
        {
            var baseline = ReadPlaybackQualityReport(pair.BaselinePath);
            var candidate = ReadPlaybackQualityReport(pair.CandidatePath);
            var context = new PlaybackQualityComparisonContext
            {
                StallComparisonCountThreshold = options.StallComparisonCountThreshold
            };
            AddPreviousComparisonIfPresent(options, pair, context);

            var comparison = PlaybackQualityRunComparator.Compare(baseline, candidate, context);
            comparison.CaseId = pair.CaseId;
            comparisons.Add(comparison);

            if (!string.IsNullOrWhiteSpace(options.ComparisonsDirectory))
            {
                var comparisonOutputPath = Path.Combine(
                    options.ComparisonsDirectory,
                    pair.OutputRelativePath);
                WriteJson(comparison, comparisonOutputPath);
            }
        }

        WriteJson(
            PlaybackQualityComparisonSuiteAggregator.Summarize(comparisons),
            options.OutputPath);
        return 0;
    }

    private static int RunValidateManifest(string[] args)
    {
        var options = ParseValidateManifestOptions(args);
        var manifest = ReadJson<PlaybackQualityReferenceManifest>(options.ManifestPath);
        var validation = PlaybackQualityReferenceManifestValidator.Validate(manifest);
        WriteJson(validation, options.OutputPath);
        return validation.IsValid ? 0 : 2;
    }

    private static int RunValidateReportSet(string[] args)
    {
        var options = ParseValidateReportSetOptions(args);
        var manifest = ReadJson<PlaybackQualityReferenceManifest>(options.ManifestPath);
        var reports = new List<PlaybackQualityReport>();
        foreach (var path in EnumerateJsonFilesByRelativePath(options.ReportsDirectory).Values)
        {
            reports.Add(ReadPlaybackQualityReport(path));
        }

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            reports);
        WriteJson(validation, options.OutputPath);
        return validation.IsValid ? 0 : 2;
    }

    private static CompareOptions ParseCompareOptions(string[] args)
    {
        var options = new CompareOptions();
        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--baseline":
                    options.BaselinePath = ReadValue(args, ref index, arg);
                    break;
                case "--candidate":
                    options.CandidatePath = ReadValue(args, ref index, arg);
                    break;
                case "--previous":
                    options.PreviousComparisonPaths.Add(ReadValue(args, ref index, arg));
                    break;
                case "--stall-threshold":
                    options.StallComparisonCountThreshold = int.Parse(
                        ReadValue(args, ref index, arg),
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--output":
                    options.OutputPath = ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException("Unknown compare option: " + arg);
            }
        }

        if (string.IsNullOrWhiteSpace(options.BaselinePath))
        {
            throw new ArgumentException("Missing required option --baseline.");
        }

        if (string.IsNullOrWhiteSpace(options.CandidatePath))
        {
            throw new ArgumentException("Missing required option --candidate.");
        }

        if (options.StallComparisonCountThreshold < 1)
        {
            throw new ArgumentException("--stall-threshold must be at least 1.");
        }

        return options;
    }

    private static SummarizeOptions ParseSummarizeOptions(string[] args)
    {
        var options = new SummarizeOptions();
        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--comparison":
                    options.ComparisonPaths.Add(ReadValue(args, ref index, arg));
                    break;
                case "--output":
                    options.OutputPath = ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException("Unknown summarize option: " + arg);
            }
        }

        if (options.ComparisonPaths.Count == 0)
        {
            throw new ArgumentException("Missing required option --comparison.");
        }

        return options;
    }

    private static CompareSuiteOptions ParseCompareSuiteOptions(string[] args)
    {
        var options = new CompareSuiteOptions();
        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--baseline-dir":
                    options.BaselineDirectory = ReadValue(args, ref index, arg);
                    break;
                case "--candidate-dir":
                    options.CandidateDirectory = ReadValue(args, ref index, arg);
                    break;
                case "--comparisons-dir":
                    options.ComparisonsDirectory = ReadValue(args, ref index, arg);
                    break;
                case "--previous-comparisons-dir":
                    options.PreviousComparisonsDirectory = ReadValue(args, ref index, arg);
                    break;
                case "--match-by":
                    options.MatchBy = ReadValue(args, ref index, arg);
                    break;
                case "--stall-threshold":
                    options.StallComparisonCountThreshold = int.Parse(
                        ReadValue(args, ref index, arg),
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--output":
                    options.OutputPath = ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException("Unknown compare-suite option: " + arg);
            }
        }

        if (string.IsNullOrWhiteSpace(options.BaselineDirectory))
        {
            throw new ArgumentException("Missing required option --baseline-dir.");
        }

        if (string.IsNullOrWhiteSpace(options.CandidateDirectory))
        {
            throw new ArgumentException("Missing required option --candidate-dir.");
        }

        if (options.StallComparisonCountThreshold < 1)
        {
            throw new ArgumentException("--stall-threshold must be at least 1.");
        }

        if (options.MatchBy != "relative-path" && options.MatchBy != "run-id")
        {
            throw new ArgumentException("--match-by must be relative-path or run-id.");
        }

        return options;
    }

    private static ValidateManifestOptions ParseValidateManifestOptions(string[] args)
    {
        var options = new ValidateManifestOptions();
        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--manifest":
                    options.ManifestPath = ReadValue(args, ref index, arg);
                    break;
                case "--output":
                    options.OutputPath = ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException("Unknown validate-manifest option: " + arg);
            }
        }

        if (string.IsNullOrWhiteSpace(options.ManifestPath))
        {
            throw new ArgumentException("Missing required option --manifest.");
        }

        return options;
    }

    private static ValidateReportSetOptions ParseValidateReportSetOptions(string[] args)
    {
        var options = new ValidateReportSetOptions();
        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--manifest":
                    options.ManifestPath = ReadValue(args, ref index, arg);
                    break;
                case "--reports-dir":
                    options.ReportsDirectory = ReadValue(args, ref index, arg);
                    break;
                case "--output":
                    options.OutputPath = ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException("Unknown validate-report-set option: " + arg);
            }
        }

        if (string.IsNullOrWhiteSpace(options.ManifestPath))
        {
            throw new ArgumentException("Missing required option --manifest.");
        }

        if (string.IsNullOrWhiteSpace(options.ReportsDirectory))
        {
            throw new ArgumentException("Missing required option --reports-dir.");
        }

        if (!Directory.Exists(options.ReportsDirectory))
        {
            throw new DirectoryNotFoundException(
                "Reports directory not found: " + options.ReportsDirectory);
        }

        return options;
    }

    private static void AddPreviousComparisonIfPresent(
        CompareSuiteOptions options,
        ReportPair pair,
        PlaybackQualityComparisonContext context)
    {
        if (string.IsNullOrWhiteSpace(options.PreviousComparisonsDirectory))
        {
            return;
        }

        if (!Directory.Exists(options.PreviousComparisonsDirectory))
        {
            throw new DirectoryNotFoundException(
                "Previous comparisons directory not found: " +
                options.PreviousComparisonsDirectory);
        }

        var previousPath = Path.Combine(
            options.PreviousComparisonsDirectory,
            pair.OutputRelativePath);
        if (File.Exists(previousPath))
        {
            context.PreviousComparisons.Add(
                ReadJson<PlaybackQualityRunComparison>(previousPath));
        }
    }

    private static List<ReportPair> FindReportPairs(CompareSuiteOptions options)
    {
        if (options.MatchBy == "run-id")
        {
            return FindReportPairsByRunId(options.BaselineDirectory, options.CandidateDirectory);
        }

        return FindReportPairsByRelativePath(options.BaselineDirectory, options.CandidateDirectory);
    }

    private static List<ReportPair> FindReportPairsByRelativePath(
        string baselineDirectory,
        string candidateDirectory)
    {
        if (!Directory.Exists(baselineDirectory))
        {
            throw new DirectoryNotFoundException("Baseline directory not found: " + baselineDirectory);
        }

        if (!Directory.Exists(candidateDirectory))
        {
            throw new DirectoryNotFoundException("Candidate directory not found: " + candidateDirectory);
        }

        var baselineFiles = EnumerateJsonFilesByRelativePath(baselineDirectory);
        var candidateFiles = EnumerateJsonFilesByRelativePath(candidateDirectory);
        var pairs = new List<ReportPair>();

        foreach (var baselineFile in baselineFiles)
        {
            if (!candidateFiles.TryGetValue(baselineFile.Key, out var candidatePath))
            {
                throw new FileNotFoundException(
                    "Candidate report not found for relative path: " + baselineFile.Key);
            }

            pairs.Add(new ReportPair(
                baselineFile.Key,
                baselineFile.Key,
                baselineFile.Value,
                candidatePath));
        }

        foreach (var candidateFile in candidateFiles)
        {
            if (!baselineFiles.ContainsKey(candidateFile.Key))
            {
                throw new FileNotFoundException(
                    "Baseline report not found for relative path: " + candidateFile.Key);
            }
        }

        if (pairs.Count == 0)
        {
            throw new ArgumentException("No JSON reports found in baseline directory.");
        }

        pairs.Sort((left, right) => string.Compare(
            left.CaseId,
            right.CaseId,
            StringComparison.OrdinalIgnoreCase));
        return pairs;
    }

    private static List<ReportPair> FindReportPairsByRunId(
        string baselineDirectory,
        string candidateDirectory)
    {
        if (!Directory.Exists(baselineDirectory))
        {
            throw new DirectoryNotFoundException("Baseline directory not found: " + baselineDirectory);
        }

        if (!Directory.Exists(candidateDirectory))
        {
            throw new DirectoryNotFoundException("Candidate directory not found: " + candidateDirectory);
        }

        var baselineFiles = EnumerateJsonFilesByRelativePath(baselineDirectory);
        var candidateFiles = EnumerateJsonFilesByRelativePath(candidateDirectory);
        var baselineByRunId = CreateRunIdReportFileMap(baselineFiles, "baseline");
        var candidateByRunId = CreateRunIdReportFileMap(candidateFiles, "candidate");
        var pairs = new List<ReportPair>();

        foreach (var baselineFile in baselineByRunId)
        {
            if (!candidateByRunId.TryGetValue(baselineFile.Key, out var candidatePath))
            {
                throw new FileNotFoundException(
                    "Candidate report not found for runId: " + baselineFile.Key);
            }

            pairs.Add(new ReportPair(
                baselineFile.Key,
                GetRunIdComparisonRelativePath(baselineFile.Key),
                baselineFile.Value,
                candidatePath));
        }

        foreach (var candidateFile in candidateByRunId)
        {
            if (!baselineByRunId.ContainsKey(candidateFile.Key))
            {
                throw new FileNotFoundException(
                    "Baseline report not found for runId: " + candidateFile.Key);
            }
        }

        if (pairs.Count == 0)
        {
            throw new ArgumentException("No JSON reports found in baseline directory.");
        }

        pairs.Sort((left, right) => string.Compare(
            left.CaseId,
            right.CaseId,
            StringComparison.OrdinalIgnoreCase));
        return pairs;
    }

    private static Dictionary<string, string> CreateRunIdReportFileMap(
        Dictionary<string, string> files,
        string role)
    {
        var byRunId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var report = ReadPlaybackQualityReport(file.Value);
            if (string.IsNullOrWhiteSpace(report.RunId))
            {
                throw new ArgumentException(
                    role + " report is missing runId: " + file.Key);
            }

            if (byRunId.ContainsKey(report.RunId))
            {
                throw new ArgumentException(
                    role + " report runId is duplicated: " + report.RunId);
            }

            byRunId.Add(report.RunId, file.Value);
        }

        return byRunId;
    }

    private static string GetRunIdComparisonRelativePath(string runId)
    {
        return runId.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? runId
            : runId + ".json";
    }

    private static Dictionary<string, string> EnumerateJsonFilesByRelativePath(string directory)
    {
        var fullRoot = Path.GetFullPath(directory);
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.EnumerateFiles(
            fullRoot,
            "*.json",
            SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(fullRoot, path);
            files.Add(relativePath, path);
        }

        return files;
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Missing value for " + optionName + ".");
        }

        index++;
        return args[index];
    }

    private static T ReadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File not found: " + path, path);
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions) ??
            throw new InvalidOperationException("Could not parse JSON file: " + path);
    }

    private static PlaybackQualityReport ReadPlaybackQualityReport(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File not found: " + path, path);
        }

        var json = File.ReadAllText(path);
        using (var document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                TryGetPropertyIgnoreCase(document.RootElement, "report", out var reportElement) &&
                reportElement.ValueKind == JsonValueKind.Object)
            {
                return reportElement.Deserialize<PlaybackQualityReport>(JsonOptions) ??
                    throw new InvalidOperationException(
                        "Could not parse report property in JSON file: " + path);
            }
        }

        return JsonSerializer.Deserialize<PlaybackQualityReport>(json, JsonOptions) ??
            throw new InvalidOperationException("Could not parse JSON file: " + path);
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement property)
    {
        foreach (var item in element.EnumerateObject())
        {
            if (string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = item.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static void WriteJson<T>(T value, string outputPath)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Out.WriteLine(json);
            return;
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, json);
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  playback-quality compare --baseline <report.json> --candidate <report.json> [--previous <comparison.json>...] [--stall-threshold <n>] [--output <comparison.json>]");
        writer.WriteLine("  playback-quality summarize --comparison <comparison.json> [--comparison <comparison.json>...] [--output <suite.json>]");
        writer.WriteLine("  playback-quality compare-suite --baseline-dir <reports-dir> --candidate-dir <reports-dir> [--match-by relative-path|run-id] [--previous-comparisons-dir <comparison-dir>] [--comparisons-dir <comparison-dir>] [--stall-threshold <n>] [--output <suite.json>]");
        writer.WriteLine("  playback-quality validate-manifest --manifest <reference-manifest.json> [--output <validation.json>]");
        writer.WriteLine("  playback-quality validate-report-set --manifest <reference-manifest.json> --reports-dir <reports-dir> [--output <validation.json>]");
    }

    private sealed class CompareOptions
    {
        public string BaselinePath { get; set; } = "";
        public string CandidatePath { get; set; } = "";
        public string OutputPath { get; set; } = "";
        public int StallComparisonCountThreshold { get; set; } = 2;
        public List<string> PreviousComparisonPaths { get; } = new List<string>();
    }

    private sealed class SummarizeOptions
    {
        public string OutputPath { get; set; } = "";
        public List<string> ComparisonPaths { get; } = new List<string>();
    }

    private sealed class CompareSuiteOptions
    {
        public string BaselineDirectory { get; set; } = "";
        public string CandidateDirectory { get; set; } = "";
        public string ComparisonsDirectory { get; set; } = "";
        public string PreviousComparisonsDirectory { get; set; } = "";
        public string OutputPath { get; set; } = "";
        public string MatchBy { get; set; } = "relative-path";
        public int StallComparisonCountThreshold { get; set; } = 2;
    }

    private sealed class ValidateManifestOptions
    {
        public string ManifestPath { get; set; } = "";
        public string OutputPath { get; set; } = "";
    }

    private sealed class ValidateReportSetOptions
    {
        public string ManifestPath { get; set; } = "";
        public string ReportsDirectory { get; set; } = "";
        public string OutputPath { get; set; } = "";
    }

    private sealed class ReportPair
    {
        public ReportPair(
            string caseId,
            string outputRelativePath,
            string baselinePath,
            string candidatePath)
        {
            CaseId = caseId;
            OutputRelativePath = outputRelativePath;
            BaselinePath = baselinePath;
            CandidatePath = candidatePath;
        }

        public string CaseId { get; }
        public string OutputRelativePath { get; }
        public string BaselinePath { get; }
        public string CandidatePath { get; }
    }
}
