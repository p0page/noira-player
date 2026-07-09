using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class ModernPlaybackQualityGateTests
{
    [Fact]
    public void Modern_Playback_Quality_Gate_Uses_App_Hosted_Capture_And_Report_Analysis()
    {
        var script = ReadToolScript("Test-NoiraModernPlaybackQuality.ps1");

        Assert.Contains("Build-NoiraModernUwp.ps1", script, StringComparison.Ordinal);
        Assert.Contains("Register-NoiraModernUwp.ps1", script, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.PlaybackQuality.Cli.csproj", script, StringComparison.Ordinal);
        Assert.Contains("plan-runs", script, StringComparison.Ordinal);
        Assert.Contains("Write-AppQualityRunCommand.ps1", script, StringComparison.Ordinal);
        Assert.Contains("Start-Process \"shell:AppsFolder\\$appUserModelId\"", script, StringComparison.Ordinal);
        Assert.Contains("Export-AppQualityRunReports.ps1", script, StringComparison.Ordinal);
        Assert.Contains("analyze-report-set", script, StringComparison.Ordinal);
        Assert.Contains("modelAnalysis", script, StringComparison.Ordinal);
        Assert.Contains("runtimeMetrics", script, StringComparison.Ordinal);
        Assert.Contains("hasPlaybackSample", script, StringComparison.Ordinal);
        Assert.Contains("RequireQualityPass", script, StringComparison.Ordinal);
        Assert.Contains("qualityResult", script, StringComparison.Ordinal);
        Assert.Contains("failedChecks", script, StringComparison.Ordinal);
        Assert.Contains("result", script, StringComparison.Ordinal);
        Assert.Contains("Remove-DirectoryInsideRoot $capturedRoot $localState", script, StringComparison.Ordinal);
        Assert.Contains("plannedCaseCount", script, StringComparison.Ordinal);
        Assert.Contains("exportedReportCount", script, StringComparison.Ordinal);
        Assert.Contains("$exportSummary.exportedReportCount -ne $cases.Count", script, StringComparison.Ordinal);
        Assert.Contains("$analysisSummary.totalReportCount -ne $cases.Count", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Modern_Playback_Quality_Gate_Cleans_Up_App_Process_On_Failure()
    {
        var script = ReadToolScript("Test-NoiraModernPlaybackQuality.ps1");

        Assert.Contains("function Stop-NoiraAppProcess", script, StringComparison.Ordinal);
        Assert.True(
            script.IndexOf("Stop-NoiraAppProcess", StringComparison.Ordinal) <
            script.IndexOf("$registerOutput = & powershell @registerArguments", StringComparison.Ordinal),
            "The playback-quality gate should stop stale app processes before restaging the loose AppX layout.");
        Assert.Contains("try", script, StringComparison.Ordinal);
        Assert.Contains("finally", script, StringComparison.Ordinal);
        Assert.Contains("$script:playbackQualityGateSucceeded = $true", script, StringComparison.Ordinal);
        Assert.Contains("if (-not $KeepRunning -or -not $script:playbackQualityGateSucceeded)", script, StringComparison.Ordinal);
        Assert.Contains("Stop-NoiraAppProcess", script.Substring(script.IndexOf("finally", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void Modern_Playback_Quality_Gate_Writes_Summary_Before_Strict_Quality_Failure()
    {
        var script = ReadToolScript("Test-NoiraModernPlaybackQuality.ps1");

        Assert.Contains("$strictQualityFailureMessage", script, StringComparison.Ordinal);
        Assert.True(
            script.IndexOf("Set-Content -LiteralPath $OutputPath", StringComparison.Ordinal) <
            script.IndexOf("throw $strictQualityFailureMessage", StringComparison.Ordinal),
            "Strict playback failures should still leave the requested summary report for triage.");
    }

    [Fact]
    public void Unified_Build_Entry_Point_Exposes_Modern_Playback_Check()
    {
        var script = ReadToolScript("Build-Noira.ps1");

        Assert.Contains("[ValidateSet('Build', 'Publish', 'Verify', 'Check', 'PlaybackCheck', 'CutoverCheck')]", script, StringComparison.Ordinal);
        Assert.Contains("Test-NoiraModernPlaybackQuality.ps1", script, StringComparison.Ordinal);
        Assert.Contains("PlaybackCheck", script, StringComparison.Ordinal);
        Assert.DoesNotContain("MigrationCheck", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Legacy toolchain currently supports only the Build target", script, StringComparison.Ordinal);
    }

    private static string ReadToolScript(string fileName)
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", fileName));
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

        throw new InvalidOperationException("Repository root not found.");
    }
}
