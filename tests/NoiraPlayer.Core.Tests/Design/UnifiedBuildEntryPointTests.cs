using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class UnifiedBuildEntryPointTests
{
    [Fact]
    public void Unified_Build_Entry_Point_Is_Modern_Only()
    {
        var script = ReadToolScript("Build-Noira.ps1");

        Assert.DoesNotContain("$Toolchain", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[ValidateSet('Modern', 'Legacy')]", script, StringComparison.Ordinal);
        Assert.Contains("[ValidateSet('Build', 'Publish', 'Verify', 'Check', 'PlaybackCheck', 'CutoverCheck')]", script, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.sln", script, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.Legacy.sln", script, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.Modern.sln", script, StringComparison.Ordinal);
        Assert.Contains("Build-NoiraModernUwp.ps1", script, StringComparison.Ordinal);
        Assert.Contains("Test-NoiraModernUwp.ps1", script, StringComparison.Ordinal);
        Assert.Contains("Test-NoiraModernPlaybackQuality.ps1", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-CoreTests", script, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.Core.Tests.csproj", script, StringComparison.Ordinal);
        Assert.Contains("'dotnet' @(", script, StringComparison.Ordinal);
        Assert.Contains("'-c'", script, StringComparison.Ordinal);
        Assert.Contains("$Configuration", script, StringComparison.Ordinal);
        Assert.Contains("Resolve-ModernMsBuildPath", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Resolve-LegacyMsBuildPath", script, StringComparison.Ordinal);
        Assert.Contains("NoiraModernToolchain.ps1", script, StringComparison.Ordinal);
        Assert.Contains(". $modernToolchainScriptPath", script, StringComparison.Ordinal);
        Assert.Contains("Assert-DotNetSdkSupportsModernNet", script, StringComparison.Ordinal);
        Assert.Contains("[int]$PostLaunchDelaySeconds = 45", script, StringComparison.Ordinal);
        Assert.Contains("[switch]$RequirePlaybackQualityPass", script, StringComparison.Ordinal);
        Assert.Contains("'-PostLaunchDelaySeconds'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Legacy toolchain currently supports only the Build target", script, StringComparison.Ordinal);
        Assert.Contains("Check", script, StringComparison.Ordinal);
        Assert.Contains("PlaybackCheck", script, StringComparison.Ordinal);
        Assert.Contains("CutoverCheck", script, StringComparison.Ordinal);
        Assert.DoesNotContain("MigrationCheck", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-MigrationCheck", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-CutoverCheck", script, StringComparison.Ordinal);
        Assert.Contains("'modern-cutover-debug-check'", script, StringComparison.Ordinal);
        Assert.Contains("'modern-cutover-release-check'", script, StringComparison.Ordinal);
        Assert.Contains("'modern-cutover-playback-check'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("'legacy-debug-build'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("migrationCheckSucceeded = $true", script, StringComparison.Ordinal);
        Assert.Contains("AppxPackageSigningEnabled=false", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Cutover_Check_Summary_Surfaces_Home_And_Playback_Evidence()
    {
        var script = ReadToolScript("Build-Noira.ps1");

        Assert.Contains("Read-MigrationChildReport", script, StringComparison.Ordinal);
        Assert.Contains("New-HomePageGateSummary", script, StringComparison.Ordinal);
        Assert.Contains("New-PlaybackGateSummary", script, StringComparison.Ordinal);
        Assert.Contains("homePageEvidence", script, StringComparison.Ordinal);
        Assert.Contains("playbackEvidence", script, StringComparison.Ordinal);
        Assert.Contains("strictPlaybackQualityResult", script, StringComparison.Ordinal);
        Assert.Contains("semanticEvidenceStatus", script, StringComparison.Ordinal);
        Assert.Contains("interactiveRequestMaxAttempts", script, StringComparison.Ordinal);
        Assert.Contains("libraryPreviewMissingCount", script, StringComparison.Ordinal);
        Assert.Contains("previewEvidenceStatus", script, StringComparison.Ordinal);
        Assert.Contains("qualityResult", script, StringComparison.Ordinal);
        Assert.Contains("startupDurationMs", script, StringComparison.Ordinal);
        Assert.Contains("plannedCaseCount", script, StringComparison.Ordinal);
        Assert.Contains("analyzedReportCount", script, StringComparison.Ordinal);
        Assert.Contains("failedChecks", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Playback_Check_Can_Require_Strict_Playback_Quality_For_Switch_Readiness()
    {
        var script = ReadToolScript("Build-Noira.ps1");

        Assert.Contains("[switch]$RequirePlaybackQualityPass", script, StringComparison.Ordinal);
        Assert.Contains("'-RequireQualityPass'", script, StringComparison.Ordinal);
        Assert.Contains("requirePlaybackQualityPass", script, StringComparison.Ordinal);
        Assert.Contains("playbackQualityGatePolicy", script, StringComparison.Ordinal);
        Assert.Contains("strict-pass-required", script, StringComparison.Ordinal);
        Assert.DoesNotContain("evidence-only", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Cutover_Check_Proves_Modern_Path_Without_Legacy_Safety_Net()
    {
        var script = ReadToolScript("Build-Noira.ps1");

        Assert.Contains("Invoke-CutoverCheck", script, StringComparison.Ordinal);
        Assert.Contains("cutoverCheckSucceeded = $true", script, StringComparison.Ordinal);
        Assert.Contains("legacyValidationIncluded = $false", script, StringComparison.Ordinal);
        Assert.Contains("modernStandalone = $true", script, StringComparison.Ordinal);
        Assert.Contains("playbackQualityGatePolicy = 'strict-pass-required'", script, StringComparison.Ordinal);

        var cutoverSection = script.Substring(script.IndexOf("function Invoke-CutoverCheck", StringComparison.Ordinal));
        cutoverSection = cutoverSection.Substring(0, cutoverSection.IndexOf("function Invoke-SolutionBuild", StringComparison.Ordinal));
        Assert.DoesNotContain("'-Toolchain'", cutoverSection, StringComparison.Ordinal);
        Assert.DoesNotContain("'Legacy'", cutoverSection, StringComparison.Ordinal);
        Assert.Contains("Invoke-CutoverPlaybackCheck", cutoverSection, StringComparison.Ordinal);
        Assert.Contains("'-RequirePlaybackQualityPass'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Cutover_Check_Retries_Strict_Playback_Smoke_For_Startup_Variance()
    {
        var script = ReadToolScript("Build-Noira.ps1");

        Assert.Contains("$CutoverPlaybackQualityMaxAttempts = 3", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-CutoverPlaybackCheck", script, StringComparison.Ordinal);
        Assert.Contains("playbackAttemptCount", script, StringComparison.Ordinal);
        Assert.Contains("for ($attempt = 1; $attempt -le $CutoverPlaybackQualityMaxAttempts; $attempt++)", script, StringComparison.Ordinal);
        Assert.Contains("Strict playback-quality cutover attempt", script, StringComparison.Ordinal);
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
