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
            var baseline = ReadJson<PlaybackQualityReport>(options.BaselinePath);
            var candidate = ReadJson<PlaybackQualityReport>(options.CandidatePath);
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

    private static void WriteJson<T>(T value, string outputPath)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Out.WriteLine(json);
            return;
        }

        File.WriteAllText(outputPath, json);
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  playback-quality compare --baseline <report.json> --candidate <report.json> [--previous <comparison.json>...] [--stall-threshold <n>] [--output <comparison.json>]");
        writer.WriteLine("  playback-quality summarize --comparison <comparison.json> [--comparison <comparison.json>...] [--output <suite.json>]");
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
}
