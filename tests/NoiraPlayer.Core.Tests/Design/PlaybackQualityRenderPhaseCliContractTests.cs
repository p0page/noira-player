using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class PlaybackQualityRenderPhaseCliContractTests
{
    [Fact]
    public void Render_Phase_Command_Strictly_Validates_Each_Manifest_Report_Set()
    {
        var source = ReadCliSource();
        var command = ExtractMethod(source, "private static int RunCompareRenderPhases", "private static PlaybackQualityComparisonSuite CompareSuite");

        Assert.Contains("SelectManifestReportSetEntries", command, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityReferenceReportSetValidator.Validate", command, StringComparison.Ordinal);
        Assert.Contains("PlaybackQualityRenderPhaseComparator.Compare", command, StringComparison.Ordinal);
        Assert.Contains("baseline-report-set.invalid", command, StringComparison.Ordinal);
        Assert.Contains("candidate-report-set.invalid", command, StringComparison.Ordinal);
        Assert.DoesNotContain("PlaybackQualityRunComparator.Compare", command, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Phase_Command_Has_No_Overall_Candidate_Decision_Authority()
    {
        var source = ReadCliSource();
        var outputModel = ExtractMethod(source, "private sealed class RenderPhaseDiagnosticSuiteOutput", "private sealed class RenderPhaseDiagnosticRepeat");

        Assert.Contains("DecisionAuthority", outputModel, StringComparison.Ordinal);
        Assert.Contains("= \"none\"", outputModel, StringComparison.Ordinal);
        Assert.DoesNotContain("accept-candidate", outputModel, StringComparison.Ordinal);
        Assert.DoesNotContain("reject-candidate", outputModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Phase_Command_Requires_Paired_Repeat_Directories()
    {
        var source = ReadCliSource();
        var parser = ExtractMethod(source, "private static CompareRenderPhasesOptions ParseCompareRenderPhasesOptions", "private static PlanRunsOptions ParsePlanRunsOptions");

        Assert.Contains("BaselineDirectories.Count == 0", parser, StringComparison.Ordinal);
        Assert.Contains("BaselineDirectories.Count != options.CandidateDirectories.Count", parser, StringComparison.Ordinal);
        Assert.Contains("paired order", parser, StringComparison.Ordinal);
    }

    private static string ReadCliSource()
    {
        return File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "NoiraPlayer.PlaybackQuality.Cli",
            "Program.cs"));
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing source marker: " + startMarker);
        Assert.True(end > start, "Missing source marker: " + endMarker);
        return source.Substring(start, end - start);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NoiraPlayer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
