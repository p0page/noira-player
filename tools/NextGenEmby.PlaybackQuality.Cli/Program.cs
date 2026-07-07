using System.Text.Json;
using System.Text.Json.Serialization;
using NextGenEmby.Core.Diagnostics;
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

            if (string.Equals(args[0], "analyze-report", StringComparison.OrdinalIgnoreCase))
            {
                return RunAnalyzeReport(args);
            }

            if (string.Equals(args[0], "analyze-report-set", StringComparison.OrdinalIgnoreCase))
            {
                return RunAnalyzeReportSet(args);
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

            if (string.Equals(args[0], "plan-runs", StringComparison.OrdinalIgnoreCase))
            {
                return RunPlanRuns(args);
            }

            if (string.Equals(args[0], "evaluate-candidate", StringComparison.OrdinalIgnoreCase))
            {
                return RunEvaluateCandidate(args);
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

    private static int RunAnalyzeReport(string[] args)
    {
        var options = ParseAnalyzeReportOptions(args);
        var envelope = ReadPlaybackQualityReportEnvelope(
            options.ReportPath,
            Path.GetFileName(options.ReportPath));
        var analysis = AnalyzeReport(envelope);
        WriteJson(analysis, options.OutputPath);
        return 0;
    }

    private static int RunAnalyzeReportSet(string[] args)
    {
        var options = ParseAnalyzeReportSetOptions(args);
        var envelopes = ReadPlaybackQualityReportEnvelopes(options.ReportsDirectory);
        var analyzedEnvelopes = EnsureModelAnalysis(envelopes);
        WriteJson(CreateReportAnalysisSummary(analyzedEnvelopes), options.OutputPath);
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
        WriteJson(CompareSuite(options), options.OutputPath);
        return 0;
    }

    private static PlaybackQualityComparisonSuite CompareSuite(
        CompareSuiteOptions options)
    {
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

        return PlaybackQualityComparisonSuiteAggregator.Summarize(comparisons);
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
        var reports = ReadPlaybackQualityReportSetEntries(options.ReportsDirectory);

        var validation = PlaybackQualityReferenceReportSetValidator.Validate(
            manifest,
            reports);
        WriteJson(validation, options.OutputPath);
        return validation.IsValid ? 0 : 2;
    }

    private static int RunPlanRuns(string[] args)
    {
        var options = ParsePlanRunsOptions(args);
        var manifest = ReadJson<PlaybackQualityReferenceManifest>(options.ManifestPath);
        var validation = PlaybackQualityReferenceManifestValidator.Validate(manifest);
        var plan = CreateRunPlan(validation, options);
        WriteJson(plan, options.OutputPath);
        return validation.IsValid ? 0 : 2;
    }

    private static int RunEvaluateCandidate(string[] args)
    {
        var options = ParseEvaluateCandidateOptions(args);
        var manifest = ReadJson<PlaybackQualityReferenceManifest>(options.ManifestPath);
        var baselineEnvelopes = RefreshIncompleteModelAnalysis(
            ReadPlaybackQualityReportEnvelopes(options.BaselineDirectory));
        var baselineReports = ExtractReports(baselineEnvelopes);
        var baselineReportEntries = ExtractReportSetEntries(baselineEnvelopes);
        var candidateEnvelopes = RefreshIncompleteModelAnalysis(
            ReadPlaybackQualityReportEnvelopes(options.CandidateDirectory));
        var candidateReports = ExtractReports(candidateEnvelopes);
        var candidateReportEntries = ExtractReportSetEntries(candidateEnvelopes);
        var baselineReportAnalysis = CreateReportAnalysisSummary(baselineEnvelopes);
        var candidateReportAnalysis = CreateReportAnalysisSummary(candidateEnvelopes);
        var evaluation = new CandidateEvaluationOutput
        {
            ManifestValidation = PlaybackQualityReferenceManifestValidator.Validate(manifest),
            BaselineReportSetValidation = PlaybackQualityReferenceReportSetValidator.Validate(
                manifest,
                baselineReportEntries),
            CandidateReportSetValidation = PlaybackQualityReferenceReportSetValidator.Validate(
                manifest,
                candidateReportEntries),
            BaselineReportAnalysis = baselineReportAnalysis,
            CandidateReportAnalysis = candidateReportAnalysis
        };

        if (!evaluation.ManifestValidation.IsValid)
        {
            AddUnique(evaluation.Blockers, "manifest.invalid");
        }

        var manifestCoverageGate = CreateManifestCoverageGate(
            evaluation.ManifestValidation.Coverage);
        if (manifestCoverageGate.Status == "blocked")
        {
            AddUnique(evaluation.Blockers, "manifest-coverage.incomplete");
            CopyValues(manifestCoverageGate.Blockers, evaluation.Blockers);
        }

        if (!evaluation.BaselineReportSetValidation.IsValid)
        {
            AddUnique(evaluation.Blockers, "baseline-report-set.invalid");
        }

        if (!evaluation.CandidateReportSetValidation.IsValid)
        {
            AddUnique(evaluation.Blockers, "candidate-report-set.invalid");
        }

        var baselineReportAnalysisGate = CreateReportAnalysisGate(
            "baseline-report-analysis",
            baselineReportAnalysis);
        if (baselineReportAnalysisGate.Status == "blocked")
        {
            AddUnique(evaluation.Blockers, "baseline-report-analysis.blocked");
            CopyValues(baselineReportAnalysisGate.Blockers, evaluation.Blockers);
        }

        var candidateReportAnalysisGate = CreateReportAnalysisGate(
            "candidate-report-analysis",
            candidateReportAnalysis);
        if (candidateReportAnalysisGate.Status == "blocked")
        {
            AddUnique(evaluation.Blockers, "candidate-report-analysis.blocked");
            CopyValues(candidateReportAnalysisGate.Blockers, evaluation.Blockers);
        }

        evaluation.EvidenceGates.Add(CreateManifestGate(evaluation.ManifestValidation));
        evaluation.EvidenceGates.Add(manifestCoverageGate);
        evaluation.EvidenceGates.Add(CreateReportSetGate(
            "baseline-report-set",
            "baseline-report-set.invalid",
            evaluation.BaselineReportSetValidation));
        evaluation.EvidenceGates.Add(CreateReportSetGate(
            "candidate-report-set",
            "candidate-report-set.invalid",
            evaluation.CandidateReportSetValidation));
        evaluation.EvidenceGates.Add(baselineReportAnalysisGate);
        evaluation.EvidenceGates.Add(candidateReportAnalysisGate);

        if (evaluation.Blockers.Count == 0)
        {
            evaluation.Suite = CompareSuite(options);
            evaluation.Action = evaluation.Suite.Action;
            evaluation.Risk = evaluation.Suite.Risk;
            CopyValues(evaluation.Suite.Reasons, evaluation.Reasons);
            CopyValues(evaluation.Suite.Blockers, evaluation.Blockers);
            evaluation.EvidenceGates.Add(CreateSuiteGate(evaluation.Suite));
        }
        else
        {
            evaluation.Action = "collect-comparable-evidence";
            evaluation.Risk = "high";
            AddUnique(
                evaluation.Reasons,
                "candidate evaluation has blocked evidence gates");
            evaluation.EvidenceGates.Add(CreateSkippedSuiteGate());
        }

        ApplyDefaultGateRisks(evaluation.EvidenceGates);
        evaluation.ActiveGate = SelectActiveGate(evaluation.EvidenceGates);
        WriteJson(evaluation, options.OutputPath);
        return evaluation.Blockers.Count == 0 ? 0 : 2;
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

    private static AnalyzeReportOptions ParseAnalyzeReportOptions(string[] args)
    {
        var options = new AnalyzeReportOptions();
        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--report":
                    options.ReportPath = ReadValue(args, ref index, arg);
                    break;
                case "--output":
                    options.OutputPath = ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException("Unknown analyze-report option: " + arg);
            }
        }

        if (string.IsNullOrWhiteSpace(options.ReportPath))
        {
            throw new ArgumentException("Missing required option --report.");
        }

        return options;
    }

    private static AnalyzeReportSetOptions ParseAnalyzeReportSetOptions(string[] args)
    {
        var options = new AnalyzeReportSetOptions();
        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--reports-dir":
                    options.ReportsDirectory = ReadValue(args, ref index, arg);
                    break;
                case "--output":
                    options.OutputPath = ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException("Unknown analyze-report-set option: " + arg);
            }
        }

        if (string.IsNullOrWhiteSpace(options.ReportsDirectory))
        {
            throw new ArgumentException("Missing required option --reports-dir.");
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

        ValidateCompareSuiteOptions(options);
        return options;
    }

    private static PlanRunsOptions ParsePlanRunsOptions(string[] args)
    {
        var options = new PlanRunsOptions();
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
                case "--duration":
                    options.DurationSeconds = int.Parse(
                        ReadValue(args, ref index, arg),
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--purpose":
                    options.Purposes.Add(ReadValue(args, ref index, arg));
                    break;
                case "--max-tier":
                    options.MaxTier = int.Parse(
                        ReadValue(args, ref index, arg),
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--output":
                    options.OutputPath = ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException("Unknown plan-runs option: " + arg);
            }
        }

        if (string.IsNullOrWhiteSpace(options.ManifestPath))
        {
            throw new ArgumentException("Missing required option --manifest.");
        }

        if (options.DurationSeconds < 10 || options.DurationSeconds > 600)
        {
            throw new ArgumentException("--duration must be between 10 and 600 seconds.");
        }

        if (options.MaxTier.HasValue &&
            (options.MaxTier.Value < 0 || options.MaxTier.Value > 4))
        {
            throw new ArgumentException("--max-tier must be between 0 and 4.");
        }

        return options;
    }

    private static EvaluateCandidateOptions ParseEvaluateCandidateOptions(string[] args)
    {
        var options = new EvaluateCandidateOptions();
        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--manifest":
                    options.ManifestPath = ReadValue(args, ref index, arg);
                    break;
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
                    throw new ArgumentException("Unknown evaluate-candidate option: " + arg);
            }
        }

        ValidateCompareSuiteOptions(options);
        if (string.IsNullOrWhiteSpace(options.ManifestPath))
        {
            throw new ArgumentException("Missing required option --manifest.");
        }

        return options;
    }

    private static void ValidateCompareSuiteOptions(CompareSuiteOptions options)
    {
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

    private static List<PlaybackQualityReport> ReadPlaybackQualityReports(string directory)
    {
        return ExtractReports(ReadPlaybackQualityReportEnvelopes(directory));
    }

    private static List<PlaybackQualityReferenceReportSetEntry> ReadPlaybackQualityReportSetEntries(string directory)
    {
        return ExtractReportSetEntries(ReadPlaybackQualityReportEnvelopes(directory));
    }

    private static List<PlaybackQualityReport> ExtractReports(
        List<PlaybackQualityReportEnvelope> envelopes)
    {
        var reports = new List<PlaybackQualityReport>();
        foreach (var envelope in envelopes)
        {
            reports.Add(envelope.Report);
        }

        return reports;
    }

    private static List<PlaybackQualityReferenceReportSetEntry> ExtractReportSetEntries(
        List<PlaybackQualityReportEnvelope> envelopes)
    {
        var entries = new List<PlaybackQualityReferenceReportSetEntry>();
        foreach (var envelope in envelopes)
        {
            var entry = new PlaybackQualityReferenceReportSetEntry(envelope.Report)
            {
                HasSignalPresenceEvidence = envelope.HasSignalPresenceEvidence
            };
            foreach (var signal in envelope.PresentSignals)
            {
                AddUnique(entry.PresentSignals, signal);
            }

            entries.Add(entry);
        }

        return entries;
    }

    private static List<PlaybackQualityReportEnvelope> ReadPlaybackQualityReportEnvelopes(
        string directory)
    {
        var envelopes = new List<PlaybackQualityReportEnvelope>();
        foreach (var item in EnumerateJsonFilesByRelativePath(directory))
        {
            envelopes.Add(ReadPlaybackQualityReportEnvelope(item.Value, item.Key));
        }

        return envelopes;
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
        return ReadPlaybackQualityReportEnvelope(path, Path.GetFileName(path)).Report;
    }

    private static PlaybackQualityReportEnvelope ReadPlaybackQualityReportEnvelope(
        string path,
        string relativePath)
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
                var report = reportElement.Deserialize<PlaybackQualityReport>(JsonOptions) ??
                    throw new InvalidOperationException(
                        "Could not parse report property in JSON file: " + path);
                PlaybackQualityModelAnalysis? modelAnalysis = null;
                if (TryGetPropertyIgnoreCase(
                    document.RootElement,
                    "modelAnalysis",
                    out var modelAnalysisElement) &&
                    modelAnalysisElement.ValueKind == JsonValueKind.Object)
                {
                    modelAnalysis =
                        modelAnalysisElement.Deserialize<PlaybackQualityModelAnalysis>(JsonOptions);
                }

                return new PlaybackQualityReportEnvelope(
                    relativePath,
                    report,
                    modelAnalysis,
                    CollectPresentReportSignals(reportElement),
                    hasSignalPresenceEvidence: true);
            }

            var rawReport = document.RootElement.Deserialize<PlaybackQualityReport>(JsonOptions) ??
                throw new InvalidOperationException("Could not parse JSON file: " + path);
            return new PlaybackQualityReportEnvelope(
                relativePath,
                rawReport,
                null,
                CollectPresentReportSignals(document.RootElement),
                hasSignalPresenceEvidence: true);
        }
    }

    private static List<PlaybackQualityReportEnvelope> EnsureModelAnalysis(
        List<PlaybackQualityReportEnvelope> envelopes)
    {
        var analyzed = new List<PlaybackQualityReportEnvelope>();
        foreach (var envelope in envelopes)
        {
            var modelAnalysis = HasUsableModelAnalysis(envelope.ModelAnalysis)
                ? envelope.ModelAnalysis
                : AnalyzeReport(envelope);
            analyzed.Add(new PlaybackQualityReportEnvelope(
                envelope.RelativePath,
                envelope.Report,
                modelAnalysis,
                envelope.PresentSignals,
                envelope.HasSignalPresenceEvidence));
        }

        return analyzed;
    }

    private static List<PlaybackQualityReportEnvelope> RefreshIncompleteModelAnalysis(
        List<PlaybackQualityReportEnvelope> envelopes)
    {
        var refreshed = new List<PlaybackQualityReportEnvelope>();
        foreach (var envelope in envelopes)
        {
            var modelAnalysis = envelope.ModelAnalysis;
            if (modelAnalysis != null && !HasUsableModelAnalysis(modelAnalysis))
            {
                modelAnalysis = AnalyzeReport(envelope);
            }

            refreshed.Add(new PlaybackQualityReportEnvelope(
                envelope.RelativePath,
                envelope.Report,
                modelAnalysis,
                envelope.PresentSignals,
                envelope.HasSignalPresenceEvidence));
        }

        return refreshed;
    }

    private static PlaybackQualityModelAnalysis AnalyzeReport(
        PlaybackQualityReportEnvelope envelope)
    {
        return envelope.HasSignalPresenceEvidence
            ? PlaybackQualityReportAnalyzer.Analyze(envelope.Report, envelope.PresentSignals)
            : PlaybackQualityReportAnalyzer.Analyze(envelope.Report);
    }

    private static bool HasUsableModelAnalysis(PlaybackQualityModelAnalysis? modelAnalysis)
    {
        return modelAnalysis != null &&
            modelAnalysis.AnalyzerVersion == PlaybackQualityReportAnalyzer.CurrentAnalyzerVersion &&
            !string.IsNullOrWhiteSpace(modelAnalysis.RunId) &&
            !string.IsNullOrWhiteSpace(modelAnalysis.Result);
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

    private static List<string> CollectPresentReportSignals(JsonElement reportElement)
    {
        var signals = new List<string>();
        foreach (var descriptor in PlaybackQualitySignalCatalog.ReportSignals)
        {
            AddNestedPresentSignal(
                signals,
                reportElement,
                descriptor.Section,
                descriptor.Property,
                descriptor.Signal);
        }

        return signals;
    }

    private static void AddNestedPresentSignal(
        List<string> signals,
        JsonElement reportElement,
        string sectionName,
        string propertyName,
        string signal)
    {
        if (reportElement.ValueKind != JsonValueKind.Object ||
            !TryGetPropertyIgnoreCase(reportElement, sectionName, out var section) ||
            section.ValueKind != JsonValueKind.Object ||
            !TryGetPropertyIgnoreCase(section, propertyName, out _))
        {
            return;
        }

        AddUnique(signals, signal);
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

    private static PlaybackQualityRunPlan CreateRunPlan(
        PlaybackQualityReferenceManifestValidation validation,
        PlanRunsOptions options)
    {
        var plan = new PlaybackQualityRunPlan
        {
            DurationSeconds = options.DurationSeconds,
            ReportsDirectory = options.ReportsDirectory,
            ManifestValidation = validation,
            Filters = CreateRunPlanFilters(options)
        };
        AddUnique(
            plan.EvidenceRequirements,
            "capture one PlaybackQualityRunResult envelope per planned case");
        AddUnique(
            plan.EvidenceRequirements,
            "write each report to reportPath using runId as the comparison key");
        AddUnique(
            plan.EvidenceRequirements,
            "run validate-report-set before compare-suite or evaluate-candidate");
        AddUnique(
            plan.EvidenceRequirements,
            "capture every requiredSignals entry or let analyze-report classify it as missing evidence");

        if (!validation.IsValid)
        {
            return plan;
        }

        foreach (var referenceCase in validation.Cases)
        {
            if (!MatchesRunPlanFilters(referenceCase, options))
            {
                continue;
            }

            var relativePath = GetRunIdComparisonRelativePath(referenceCase.CaseId);
            var hasEmbyItem = !string.IsNullOrWhiteSpace(referenceCase.ItemId);
            var planCase = new PlaybackQualityRunPlanCase
            {
                CaseId = referenceCase.CaseId,
                RunId = referenceCase.CaseId,
                SourceUri = referenceCase.Uri,
                Tier = referenceCase.Tier,
                DurationSeconds = options.DurationSeconds,
                CaptureMode = hasEmbyItem ? "emby-item" : "direct-uri",
                ReportRelativePath = relativePath,
                ReportPath = string.IsNullOrWhiteSpace(options.ReportsDirectory)
                    ? relativePath
                    : Path.Combine(options.ReportsDirectory, relativePath),
                Expected = CloneExpected(referenceCase.Expected)
            };

            if (hasEmbyItem)
            {
                planCase.DevCommand = CreateQualityRunCommand(
                    referenceCase,
                    options.DurationSeconds);
            }

            foreach (var purpose in referenceCase.Purpose)
            {
                AddUnique(planCase.Purpose, purpose);
            }

            foreach (var signal in PlaybackQualityRequiredSignalPolicy.CreateRequiredSignals(referenceCase))
            {
                AddUnique(planCase.RequiredSignals, signal);
            }

            plan.Cases.Add(planCase);
        }

        plan.CaseCount = plan.Cases.Count;
        return plan;
    }

    private static PlaybackQualityRunPlanFilters CreateRunPlanFilters(
        PlanRunsOptions options)
    {
        var filters = new PlaybackQualityRunPlanFilters
        {
            MaxTier = options.MaxTier
        };
        CopyValues(options.Purposes, filters.Purposes);
        return filters;
    }

    private static bool MatchesRunPlanFilters(
        PlaybackQualityReferenceCase referenceCase,
        PlanRunsOptions options)
    {
        if (options.MaxTier.HasValue && referenceCase.Tier > options.MaxTier.Value)
        {
            return false;
        }

        if (options.Purposes.Count == 0)
        {
            return true;
        }

        foreach (var purpose in referenceCase.Purpose)
        {
            foreach (var requestedPurpose in options.Purposes)
            {
                if (string.Equals(purpose, requestedPurpose, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static DevelopmentNavigationCommand CreateQualityRunCommand(
        PlaybackQualityReferenceCase referenceCase,
        int durationSeconds)
    {
        return new DevelopmentNavigationCommand
        {
            Route = "quality-run",
            ItemId = referenceCase.ItemId,
            MediaSourceId = referenceCase.MediaSourceId,
            StartPositionTicks = Math.Max(0, referenceCase.StartPositionTicks),
            ForceSdrOutput = referenceCase.ForceSdrOutput,
            RunId = referenceCase.CaseId,
            DurationSeconds = durationSeconds,
            Expected = CloneExpected(referenceCase.Expected)
        };
    }

    private static PlaybackQualityExpected CloneExpected(PlaybackQualityExpected source)
    {
        if (source == null)
        {
            return new PlaybackQualityExpected();
        }

        return new PlaybackQualityExpected
        {
            Codec = source.Codec,
            Width = source.Width,
            Height = source.Height,
            FrameRate = source.FrameRate,
            HdrKind = source.HdrKind,
            HdrPlaybackStrategy = source.HdrPlaybackStrategy,
            IsHdr = source.IsHdr,
            IsDirectPlayable = source.IsDirectPlayable,
            IsDolbyVision = source.IsDolbyVision,
            DolbyVisionProfile = source.DolbyVisionProfile,
            DolbyVisionCompatibilityId = source.DolbyVisionCompatibilityId,
            HasHdr10BaseLayer = source.HasHdr10BaseLayer,
            HasHlgBaseLayer = source.HasHlgBaseLayer,
            HdrOutput = source.HdrOutput,
            DxgiInput = source.DxgiInput,
            DxgiOutput = source.DxgiOutput,
            MaxStartupDurationMs = source.MaxStartupDurationMs,
            MinRenderedVideoFrames = source.MinRenderedVideoFrames,
            MaxDroppedFrames = source.MaxDroppedFrames,
            MaxFrameGapMs = source.MaxFrameGapMs,
            MaxRenderIntervalMsP95 = source.MaxRenderIntervalMsP95,
            MaxRenderIntervalMsP99 = source.MaxRenderIntervalMsP99,
            MaxAudioVideoDriftMsP95 = source.MaxAudioVideoDriftMsP95,
            MaxVideoStarvedPasses = source.MaxVideoStarvedPasses,
            MaxAudioStarvedPasses = source.MaxAudioStarvedPasses,
            RequireValidatedConversion = source.RequireValidatedConversion,
            RequireMatchedDisplayRefreshRate = source.RequireMatchedDisplayRefreshRate
        };
    }

    private static CandidateEvaluationGate CreateManifestGate(
        PlaybackQualityReferenceManifestValidation validation)
    {
        var gate = new CandidateEvaluationGate
        {
            Name = "manifest",
            Status = validation.IsValid ? "pass" : "blocked",
            Action = validation.IsValid ? "continue" : "fix-manifest",
            Summary = validation.IsValid
                ? "reference manifest is valid"
                : "reference manifest is invalid"
        };

        if (!validation.IsValid)
        {
            AddUnique(gate.Blockers, "manifest.invalid");
            foreach (var error in validation.Errors)
            {
                AddUnique(gate.Signals, error.Signal);
                AddUnique(gate.CaseIds, error.CaseId);
            }

            MarkWeakGateConfidence(
                gate,
                "manifest validation failed before comparison evidence was usable");
        }

        return gate;
    }

    private static CandidateEvaluationGate CreateManifestCoverageGate(
        PlaybackQualityReferenceManifestCoverage coverage)
    {
        if (coverage == null)
        {
            coverage = new PlaybackQualityReferenceManifestCoverage();
        }

        var isReady = coverage.Status == "ready" && coverage.IsCoreEvaluationReady;
        var gate = new CandidateEvaluationGate
        {
            Name = "manifest-coverage",
            Status = isReady ? "pass" : "blocked",
            Action = isReady ? "continue" : "collect-comparable-evidence",
            Summary = isReady
                ? "reference corpus covers playback Core evaluation purposes"
                : "reference corpus is missing playback Core evaluation purposes"
        };

        if (isReady)
        {
            CopyValues(coverage.CoveredPurposes, gate.Signals);
            return gate;
        }

        AddUnique(gate.Blockers, "manifest-coverage.incomplete");
        AddUnique(gate.SuggestedNextActions, coverage.SuggestedNextAction);
        PlaybackQualityCodeTargetCatalog.AddForFailureArea(
            gate.CodeTargets,
            "evidence-collection");
        foreach (var purpose in coverage.MissingPurposes)
        {
            AddUnique(gate.Signals, purpose);
        }

        MarkWeakGateConfidence(
            gate,
            "manifest coverage is incomplete before comparison evidence is usable");
        return gate;
    }

    private static CandidateEvaluationGate CreateReportSetGate(
        string name,
        string blocker,
        PlaybackQualityReferenceReportSetValidation validation)
    {
        var gate = new CandidateEvaluationGate
        {
            Name = name,
            Status = validation.IsValid ? "pass" : "blocked",
            Action = validation.IsValid ? "continue" : "fix-" + name,
            Summary = validation.IsValid
                ? name + " matches reference manifest"
                : name + " does not match reference manifest"
        };

        if (!validation.IsValid)
        {
            AddUnique(gate.Blockers, blocker);
            foreach (var error in validation.Errors)
            {
                AddUnique(gate.Signals, error.Signal);
                AddUnique(gate.CaseIds, error.CaseId);
                CopyValues(error.CodeTargets, gate.CodeTargets);
                AddUnique(gate.SuggestedNextActions, error.SuggestedNextAction);
            }

            if (gate.CodeTargets.Count == 0)
            {
                PlaybackQualityCodeTargetCatalog.AddForFailureArea(
                    gate.CodeTargets,
                    "evidence-collection");
            }

            MarkWeakGateConfidence(
                gate,
                name + " validation failed before comparison evidence was usable");
        }

        return gate;
    }

    private static CandidateEvaluationGate CreateReportAnalysisGate(
        string name,
        ReportAnalysisSummary summary)
    {
        var label = name.Replace("-", " ", StringComparison.Ordinal);
        var gate = new CandidateEvaluationGate
        {
            Name = name,
            Status = "pass",
            Action = "continue",
            Summary = label + " has no optimization blockers"
        };

        foreach (var item in summary.Cases)
        {
            if (!item.IsBlocked)
            {
                continue;
            }

            gate.Status = "blocked";
            gate.Action = "fix-" + name;
            gate.Summary = label + " has optimization blockers";
            foreach (var blocker in item.Blockers)
            {
                AddUnique(gate.Blockers, blocker);
            }

            foreach (var signal in item.Signals)
            {
                AddUnique(gate.Signals, signal);
            }

            foreach (var area in item.FailureAreas)
            {
                AddUnique(gate.FailureAreas, area);
            }

            foreach (var area in item.TargetFailureAreas)
            {
                AddUnique(gate.TargetFailureAreas, area);
            }

            CopyValues(item.CodeTargets, gate.CodeTargets);
            CopyValues(item.SuggestedNextActions, gate.SuggestedNextActions);
            AddUnique(gate.CaseIds, item.CaseId);
            AddUnique(gate.TargetCaseIds, item.CaseId);
        }

        if (gate.Status == "blocked" && gate.CodeTargets.Count == 0)
        {
            PlaybackQualityCodeTargetCatalog.AddForFailureArea(
                gate.CodeTargets,
                "evidence-collection");
        }

        if (gate.Status == "blocked")
        {
            MarkWeakGateConfidence(
                gate,
                label + " blocked playback Core optimization evidence");
        }

        if (summary.AnalyzedReportCount == 0)
        {
            gate.Summary =
                label + " is unavailable; continuing with report-set and suite evidence";
        }

        return gate;
    }

    private static ReportAnalysisSummary CreateReportAnalysisSummary(
        List<PlaybackQualityReportEnvelope> envelopes)
    {
        var summary = new ReportAnalysisSummary
        {
            TotalReportCount = envelopes.Count
        };

        foreach (var envelope in envelopes)
        {
            var item = new ReportAnalysisCase
            {
                CaseId = GetReportEnvelopeCaseId(envelope),
                HasModelAnalysis = envelope.ModelAnalysis != null
            };

            if (envelope.ModelAnalysis == null)
            {
                item.Status = "unavailable";
                summary.UnavailableReportCount++;
                summary.Cases.Add(item);
                continue;
            }

            summary.AnalyzedReportCount++;
            var optimizationGate = envelope.ModelAnalysis.OptimizationGate;
            item.Status = string.IsNullOrWhiteSpace(optimizationGate.Status)
                ? "unknown"
                : optimizationGate.Status;
            item.CanOptimizePlaybackCore = optimizationGate.CanOptimizePlaybackCore;
            item.IsBlocked = IsOptimizationGateBlocked(optimizationGate);
            if (item.IsBlocked)
            {
                summary.BlockedReportCount++;
            }

            CopyValues(envelope.ModelAnalysis.FailureAreas, item.FailureAreas);
            CopyValues(envelope.ModelAnalysis.EvidenceSignals, item.Signals);
            CopyValues(optimizationGate.Blockers, item.Blockers);
            CopyValues(optimizationGate.BlockerSignals, item.Signals);
            CopyValues(optimizationGate.TargetFailureAreas, item.TargetFailureAreas);
            AddModelAnalysisCodeTargets(envelope.ModelAnalysis, item.CodeTargets);
            AddModelAnalysisSuggestedNextActions(
                envelope.ModelAnalysis,
                item.SuggestedNextActions);
            PlaybackQualityCodeTargetCatalog.AddForSignals(
                item.CodeTargets,
                optimizationGate.BlockerSignals);
            PlaybackQualityCodeTargetCatalog.AddForFailureAreas(
                item.CodeTargets,
                item.TargetFailureAreas);
            PlaybackQualityCodeTargetCatalog.AddForFailureAreas(
                item.CodeTargets,
                item.FailureAreas);
            if (item.IsBlocked && item.CodeTargets.Count == 0)
            {
                PlaybackQualityCodeTargetCatalog.AddForFailureArea(
                    item.CodeTargets,
                    "evidence-collection");
            }

            AddReportAnalysisSummaryEvidence(summary, item);
            summary.Cases.Add(item);
        }

        AddReportAnalysisTargets(summary);
        return summary;
    }

    private static void AddReportAnalysisSummaryEvidence(
        ReportAnalysisSummary summary,
        ReportAnalysisCase item)
    {
        CopyValues(item.Blockers, summary.Blockers);
        CopyValues(item.Signals, summary.Signals);
        CopyValues(item.FailureAreas, summary.FailureAreas);
        CopyValues(item.TargetFailureAreas, summary.TargetFailureAreas);
        CopyValues(item.CodeTargets, summary.CodeTargets);
        CopyValues(item.SuggestedNextActions, summary.SuggestedNextActions);
    }

    private static void AddReportAnalysisTargets(ReportAnalysisSummary summary)
    {
        var targetArea = GetHighestPriorityArea(summary.TargetFailureAreas);
        if (string.IsNullOrWhiteSpace(targetArea))
        {
            targetArea = GetHighestPriorityArea(summary.FailureAreas);
        }

        if (string.IsNullOrWhiteSpace(targetArea))
        {
            return;
        }

        summary.TargetFailureAreas.Clear();
        AddUnique(summary.TargetFailureAreas, targetArea);
        PlaybackQualityCodeTargetCatalog.AddForFailureArea(
            summary.CodeTargets,
            targetArea);
        foreach (var item in summary.Cases)
        {
            if (item.TargetFailureAreas.Contains(targetArea) ||
                item.FailureAreas.Contains(targetArea))
            {
                AddUnique(summary.TargetCaseIds, item.CaseId);
            }
        }
    }

    private static void AddModelAnalysisCodeTargets(
        PlaybackQualityModelAnalysis analysis,
        List<string> codeTargets)
    {
        foreach (var step in analysis.TriageSteps)
        {
            CopyValues(step.CodeTargets, codeTargets);
        }

        foreach (var hint in analysis.InvestigationHints)
        {
            CopyValues(hint.CodeTargets, codeTargets);
        }
    }

    private static void AddModelAnalysisSuggestedNextActions(
        PlaybackQualityModelAnalysis analysis,
        List<string> suggestedNextActions)
    {
        AddUnique(suggestedNextActions, analysis.SuggestedNextAction);
        foreach (var step in analysis.TriageSteps)
        {
            AddUnique(suggestedNextActions, step.SuggestedAction);
        }

        foreach (var hint in analysis.InvestigationHints)
        {
            AddUnique(suggestedNextActions, hint.SuggestedAction);
        }
    }

    private static string GetHighestPriorityArea(List<string> areas)
    {
        var priorityAreas = new[]
        {
            "unsupported-source",
            "color-pipeline",
            "startup",
            "buffering",
            "av-sync",
            "frame-pacing",
            "unknown"
        };

        foreach (var area in priorityAreas)
        {
            if (areas.Contains(area))
            {
                return area;
            }
        }

        return "";
    }

    private static bool IsOptimizationGateBlocked(
        PlaybackQualityOptimizationGate optimizationGate)
    {
        if (string.Equals(optimizationGate.Status, "blocked", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !optimizationGate.CanOptimizePlaybackCore &&
            optimizationGate.Blockers.Count > 0 &&
            !string.Equals(
                optimizationGate.Status,
                "not-needed",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string GetReportEnvelopeCaseId(PlaybackQualityReportEnvelope envelope)
    {
        if (!string.IsNullOrWhiteSpace(envelope.ModelAnalysis?.RunId))
        {
            return envelope.ModelAnalysis.RunId;
        }

        if (!string.IsNullOrWhiteSpace(envelope.Report.RunId))
        {
            return envelope.Report.RunId;
        }

        return envelope.RelativePath;
    }

    private static CandidateEvaluationGate CreateSuiteGate(
        PlaybackQualityComparisonSuite suite)
    {
        var gate = new CandidateEvaluationGate
        {
            Name = "suite",
            Status = suite.Blockers.Count == 0 ? "pass" : "blocked",
            Action = suite.Action,
            Risk = suite.Risk,
            Summary = "comparison suite action: " + suite.Action
        };

        CopyValues(suite.Blockers, gate.Blockers);
        CopyValues(suite.Signals, gate.Signals);
        CopyValues(suite.FailureAreas, gate.FailureAreas);
        CopyValues(suite.TargetFailureAreas, gate.TargetFailureAreas);
        CopyValues(suite.TargetCaseIds, gate.TargetCaseIds);
        CopyValues(suite.CodeTargets, gate.CodeTargets);
        ApplySuiteGateConfidence(gate, suite);
        ApplySuiteGateResultCounts(gate, suite);
        CopySuiteNextActions(suite.NextActions, gate.NextActions);
        gate.Environment = suite.Environment;
        foreach (var summary in suite.Cases)
        {
            AddUnique(gate.CaseIds, summary.CaseId);
            AddUnique(gate.SuggestedNextActions, summary.SuggestedNextAction);
        }

        return gate;
    }

    private static CandidateEvaluationGate CreateSkippedSuiteGate()
    {
        var gate = new CandidateEvaluationGate
        {
            Name = "suite",
            Status = "skipped",
            Action = "collect-comparable-evidence",
            Risk = "high",
            Summary = "comparison suite skipped because an earlier evidence gate failed"
        };
        AddUnique(gate.Blockers, "suite.skipped");
        AddUnique(
            gate.SuggestedNextActions,
            "Resolve earlier evidence gates before running the comparison suite.");
        var nextAction = new PlaybackQualitySuiteNextAction
        {
            Rank = 1,
            Action = gate.Action,
            Risk = "high"
        };
        AddUnique(nextAction.Blockers, "suite.skipped");
        AddUnique(
            nextAction.Reasons,
            "comparison suite was skipped because earlier evidence gates failed");
        gate.NextActions.Add(nextAction);
        MarkWeakGateConfidence(
            gate,
            "comparison suite was skipped because earlier evidence gates failed");
        return gate;
    }

    private static void ApplySuiteGateConfidence(
        CandidateEvaluationGate gate,
        PlaybackQualityComparisonSuite suite)
    {
        gate.Confidence.TotalCount = suite.TotalComparisonCount;
        gate.Confidence.StrongCount = suite.StrongConfidenceCount;
        gate.Confidence.PartialCount = suite.PartialConfidenceCount;
        gate.Confidence.WeakCount = suite.WeakConfidenceCount;
        gate.Confidence.InsufficientEvidenceCount = suite.InsufficientEvidenceCount;

        if (suite.WeakConfidenceCount > 0 || suite.InsufficientEvidenceCount > 0)
        {
            gate.Confidence.Level = "weak";
        }
        else if (suite.PartialConfidenceCount > 0)
        {
            gate.Confidence.Level = "partial";
        }
        else if (suite.StrongConfidenceCount > 0)
        {
            gate.Confidence.Level = "strong";
        }
        else
        {
            gate.Confidence.Level = "weak";
        }

        CopyValues(suite.Signals, gate.Confidence.Signals);
        CopyValues(suite.Reasons, gate.Confidence.Reasons);
        CopyValues(suite.Blockers, gate.Confidence.Reasons);
    }

    private static void CopySuiteNextActions(
        List<PlaybackQualitySuiteNextAction> source,
        List<PlaybackQualitySuiteNextAction> target)
    {
        foreach (var action in source)
        {
            var clone = new PlaybackQualitySuiteNextAction
            {
                Rank = action.Rank,
                Action = action.Action,
                Risk = action.Risk,
                FailureArea = action.FailureArea
            };

            CopyValues(action.CaseIds, clone.CaseIds);
            CopyValues(action.Signals, clone.Signals);
            CopyValues(action.Reasons, clone.Reasons);
            CopyValues(action.Blockers, clone.Blockers);
            CopyValues(action.CodeTargets, clone.CodeTargets);
            target.Add(clone);
        }
    }

    private static void ApplySuiteGateResultCounts(
        CandidateEvaluationGate gate,
        PlaybackQualityComparisonSuite suite)
    {
        gate.ResultCounts.TotalCount = suite.TotalComparisonCount;
        gate.ResultCounts.ImprovedCount = suite.ImprovedCount;
        gate.ResultCounts.RegressedCount = suite.RegressedCount;
        gate.ResultCounts.MixedCount = suite.MixedCount;
        gate.ResultCounts.UnchangedCount = suite.UnchangedCount;
        gate.ResultCounts.InsufficientEvidenceCount = suite.InsufficientEvidenceCount;
        gate.ResultCounts.PolicyChangeCount = suite.PolicyChangeCount;
    }

    private static void MarkWeakGateConfidence(
        CandidateEvaluationGate gate,
        string reason)
    {
        gate.Confidence.Level = "weak";
        AddUnique(gate.Confidence.Reasons, reason);
        CopyValues(gate.Signals, gate.Confidence.Signals);
        CopyValues(gate.Blockers, gate.Confidence.Reasons);
    }

    private static void ApplyDefaultGateRisks(
        List<CandidateEvaluationGate> gates)
    {
        foreach (var gate in gates)
        {
            if (!string.IsNullOrWhiteSpace(gate.Risk))
            {
                continue;
            }

            gate.Risk = string.Equals(gate.Status, "pass", StringComparison.OrdinalIgnoreCase)
                ? "low"
                : "high";
        }
    }

    private static CandidateEvaluationGate SelectActiveGate(
        List<CandidateEvaluationGate> gates)
    {
        foreach (var gate in gates)
        {
            if (!string.Equals(gate.Status, "pass", StringComparison.OrdinalIgnoreCase))
            {
                return gate;
            }
        }

        if (gates.Count == 0)
        {
            return new CandidateEvaluationGate();
        }

        return gates[gates.Count - 1];
    }

    private static void CopyValues(List<string> source, List<string> target)
    {
        foreach (var value in source)
        {
            AddUnique(target, value);
        }
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
        {
            values.Add(value);
        }
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  playback-quality analyze-report --report <report.json> [--output <analysis.json>]");
        writer.WriteLine("  playback-quality analyze-report-set --reports-dir <reports-dir> [--output <analysis-summary.json>]");
        writer.WriteLine("  playback-quality compare --baseline <report.json> --candidate <report.json> [--previous <comparison.json>...] [--stall-threshold <n>] [--output <comparison.json>]");
        writer.WriteLine("  playback-quality summarize --comparison <comparison.json> [--comparison <comparison.json>...] [--output <suite.json>]");
        writer.WriteLine("  playback-quality compare-suite --baseline-dir <reports-dir> --candidate-dir <reports-dir> [--match-by relative-path|run-id] [--previous-comparisons-dir <comparison-dir>] [--comparisons-dir <comparison-dir>] [--stall-threshold <n>] [--output <suite.json>]");
        writer.WriteLine("  playback-quality validate-manifest --manifest <reference-manifest.json> [--output <validation.json>]");
        writer.WriteLine("  playback-quality validate-report-set --manifest <reference-manifest.json> --reports-dir <reports-dir> [--output <validation.json>]");
        writer.WriteLine("  playback-quality plan-runs --manifest <reference-manifest.json> [--reports-dir <reports-dir>] [--duration <seconds>] [--purpose <purpose>...] [--max-tier <0-4>] [--output <run-plan.json>]");
        writer.WriteLine("  playback-quality evaluate-candidate --manifest <reference-manifest.json> --baseline-dir <reports-dir> --candidate-dir <reports-dir> [--match-by relative-path|run-id] [--previous-comparisons-dir <comparison-dir>] [--comparisons-dir <comparison-dir>] [--stall-threshold <n>] [--output <evaluation.json>]");
    }

    private sealed class CompareOptions
    {
        public string BaselinePath { get; set; } = "";
        public string CandidatePath { get; set; } = "";
        public string OutputPath { get; set; } = "";
        public int StallComparisonCountThreshold { get; set; } = 2;
        public List<string> PreviousComparisonPaths { get; } = new List<string>();
    }

    private sealed class AnalyzeReportOptions
    {
        public string ReportPath { get; set; } = "";
        public string OutputPath { get; set; } = "";
    }

    private sealed class AnalyzeReportSetOptions
    {
        public string ReportsDirectory { get; set; } = "";
        public string OutputPath { get; set; } = "";
    }

    private sealed class SummarizeOptions
    {
        public string OutputPath { get; set; } = "";
        public List<string> ComparisonPaths { get; } = new List<string>();
    }

    private class CompareSuiteOptions
    {
        public string BaselineDirectory { get; set; } = "";
        public string CandidateDirectory { get; set; } = "";
        public string ComparisonsDirectory { get; set; } = "";
        public string PreviousComparisonsDirectory { get; set; } = "";
        public string OutputPath { get; set; } = "";
        public string MatchBy { get; set; } = "relative-path";
        public int StallComparisonCountThreshold { get; set; } = 2;
    }

    private sealed class EvaluateCandidateOptions : CompareSuiteOptions
    {
        public string ManifestPath { get; set; } = "";

        public EvaluateCandidateOptions()
        {
            MatchBy = "run-id";
        }
    }

    private sealed class PlanRunsOptions
    {
        public string ManifestPath { get; set; } = "";
        public string ReportsDirectory { get; set; } = "";
        public string OutputPath { get; set; } = "";
        public int DurationSeconds { get; set; } = 30;
        public int? MaxTier { get; set; }
        public List<string> Purposes { get; } = new List<string>();
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

    private sealed class CandidateEvaluationOutput
    {
        public string Action { get; set; } = "collect-comparable-evidence";
        public string Risk { get; set; } = "high";
        public List<string> Reasons { get; } = new List<string>();
        public List<string> Blockers { get; } = new List<string>();
        public CandidateEvaluationGate ActiveGate { get; set; } = new CandidateEvaluationGate();
        public List<CandidateEvaluationGate> EvidenceGates { get; } =
            new List<CandidateEvaluationGate>();
        public PlaybackQualityReferenceManifestValidation ManifestValidation { get; set; } =
            new PlaybackQualityReferenceManifestValidation();
        public PlaybackQualityReferenceReportSetValidation BaselineReportSetValidation { get; set; } =
            new PlaybackQualityReferenceReportSetValidation();
        public PlaybackQualityReferenceReportSetValidation CandidateReportSetValidation { get; set; } =
            new PlaybackQualityReferenceReportSetValidation();
        public ReportAnalysisSummary BaselineReportAnalysis { get; set; } =
            new ReportAnalysisSummary();
        public ReportAnalysisSummary CandidateReportAnalysis { get; set; } =
            new ReportAnalysisSummary();
        public PlaybackQualityComparisonSuite Suite { get; set; } =
            new PlaybackQualityComparisonSuite();
    }

    private sealed class PlaybackQualityReportEnvelope
    {
        public PlaybackQualityReportEnvelope(
            string relativePath,
            PlaybackQualityReport report,
            PlaybackQualityModelAnalysis? modelAnalysis,
            IEnumerable<string>? presentSignals = null,
            bool hasSignalPresenceEvidence = false)
        {
            RelativePath = relativePath;
            Report = report;
            ModelAnalysis = modelAnalysis;
            HasSignalPresenceEvidence = hasSignalPresenceEvidence;
            if (presentSignals != null)
            {
                foreach (var signal in presentSignals)
                {
                    AddUnique(PresentSignals, signal);
                }
            }
        }

        public string RelativePath { get; }
        public PlaybackQualityReport Report { get; }
        public PlaybackQualityModelAnalysis? ModelAnalysis { get; }
        public bool HasSignalPresenceEvidence { get; }
        public List<string> PresentSignals { get; } = new List<string>();
    }

    private sealed class ReportAnalysisSummary
    {
        public int TotalReportCount { get; set; }
        public int AnalyzedReportCount { get; set; }
        public int UnavailableReportCount { get; set; }
        public int BlockedReportCount { get; set; }
        public List<string> Blockers { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
        public List<string> FailureAreas { get; } = new List<string>();
        public List<string> TargetFailureAreas { get; } = new List<string>();
        public List<string> TargetCaseIds { get; } = new List<string>();
        public List<string> CodeTargets { get; } = new List<string>();
        public List<string> SuggestedNextActions { get; } = new List<string>();
        public List<ReportAnalysisCase> Cases { get; } =
            new List<ReportAnalysisCase>();
    }

    private sealed class ReportAnalysisCase
    {
        public string CaseId { get; set; } = "";
        public bool HasModelAnalysis { get; set; }
        public string Status { get; set; } = "";
        public bool CanOptimizePlaybackCore { get; set; }
        public bool IsBlocked { get; set; }
        public List<string> Blockers { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
        public List<string> FailureAreas { get; } = new List<string>();
        public List<string> TargetFailureAreas { get; } = new List<string>();
        public List<string> CodeTargets { get; } = new List<string>();
        public List<string> SuggestedNextActions { get; } = new List<string>();
    }

    private sealed class CandidateEvaluationGate
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public string Action { get; set; } = "";
        public string Risk { get; set; } = "";
        public string Summary { get; set; } = "";
        public CandidateEvaluationGateConfidence Confidence { get; set; } =
            new CandidateEvaluationGateConfidence();
        public CandidateEvaluationGateResultCounts ResultCounts { get; set; } =
            new CandidateEvaluationGateResultCounts();
        public List<string> Blockers { get; } = new List<string>();
        public List<string> Signals { get; } = new List<string>();
        public PlaybackQualityComparisonSuiteEnvironment? Environment { get; set; }
        public List<string> FailureAreas { get; } = new List<string>();
        public List<string> TargetFailureAreas { get; } = new List<string>();
        public List<string> TargetCaseIds { get; } = new List<string>();
        public List<string> CaseIds { get; } = new List<string>();
        public List<string> CodeTargets { get; } = new List<string>();
        public List<string> SuggestedNextActions { get; } = new List<string>();
        public List<PlaybackQualitySuiteNextAction> NextActions { get; } =
            new List<PlaybackQualitySuiteNextAction>();
    }

    private sealed class CandidateEvaluationGateConfidence
    {
        public string Level { get; set; } = "";
        public int TotalCount { get; set; }
        public int StrongCount { get; set; }
        public int PartialCount { get; set; }
        public int WeakCount { get; set; }
        public int InsufficientEvidenceCount { get; set; }
        public List<string> Signals { get; } = new List<string>();
        public List<string> Reasons { get; } = new List<string>();
    }

    private sealed class CandidateEvaluationGateResultCounts
    {
        public int TotalCount { get; set; }
        public int ImprovedCount { get; set; }
        public int RegressedCount { get; set; }
        public int MixedCount { get; set; }
        public int UnchangedCount { get; set; }
        public int InsufficientEvidenceCount { get; set; }
        public int PolicyChangeCount { get; set; }
    }

    private sealed class PlaybackQualityRunPlan
    {
        public int SchemaVersion { get; set; } = 1;
        public int CaseCount { get; set; }
        public int DurationSeconds { get; set; }
        public string ReportsDirectory { get; set; } = "";
        public PlaybackQualityRunPlanFilters Filters { get; set; } =
            new PlaybackQualityRunPlanFilters();
        public List<string> EvidenceRequirements { get; } = new List<string>();
        public List<PlaybackQualityRunPlanCase> Cases { get; } =
            new List<PlaybackQualityRunPlanCase>();
        public PlaybackQualityReferenceManifestValidation ManifestValidation { get; set; } =
            new PlaybackQualityReferenceManifestValidation();
    }

    private sealed class PlaybackQualityRunPlanFilters
    {
        public List<string> Purposes { get; } = new List<string>();
        public int? MaxTier { get; set; }
    }

    private sealed class PlaybackQualityRunPlanCase
    {
        public string CaseId { get; set; } = "";
        public string RunId { get; set; } = "";
        public string SourceUri { get; set; } = "";
        public int Tier { get; set; }
        public List<string> Purpose { get; } = new List<string>();
        public int DurationSeconds { get; set; }
        public string CaptureMode { get; set; } = "";
        public string ReportRelativePath { get; set; } = "";
        public string ReportPath { get; set; } = "";
        public List<string> RequiredSignals { get; } = new List<string>();
        public PlaybackQualityExpected Expected { get; set; } =
            new PlaybackQualityExpected();
        public DevelopmentNavigationCommand? DevCommand { get; set; }
    }
}
