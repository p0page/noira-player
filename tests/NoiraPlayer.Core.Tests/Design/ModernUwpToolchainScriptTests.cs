using System;
using System.IO;
using System.Linq;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class ModernUwpToolchainScriptTests
{
    [Fact]
    public void Modern_Build_Script_Fails_Fast_When_DotNet_Sdk_10_Is_Missing()
    {
        var script = ReadToolScript("Build-NoiraModernUwp.ps1");

        Assert.Contains("NoiraModernToolchain.ps1", script, StringComparison.Ordinal);
        Assert.Contains(". $modernToolchainScriptPath", script, StringComparison.Ordinal);
        Assert.Contains("Assert-DotNetSdkSupportsModernNet", script, StringComparison.Ordinal);
        Assert.True(
            script.IndexOf("Assert-DotNetSdkSupportsModernNet", StringComparison.Ordinal) <
            script.IndexOf("Resolve-ModernMsBuildPath $MsBuildPath", StringComparison.Ordinal),
            "Modern SDK preflight should run before resolving MSBuild/building the project.");
    }

    [Fact]
    public void Modern_Build_Script_Fails_Publish_When_Aot_Or_Trim_Warnings_Appear()
    {
        var script = ReadToolScript("Build-NoiraModernUwp.ps1");

        Assert.Contains("Assert-NoAotOrTrimWarnings", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-CapturedProcess", script, StringComparison.Ordinal);
        Assert.Contains("'IL2\\d{3}|IL3\\d{3}'", script, StringComparison.Ordinal);
        Assert.Contains("$Target -eq 'Publish' -and -not $DisableAot", script, StringComparison.Ordinal);
        Assert.Contains("Native AOT/trimming warnings are blockers", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Modern_Verification_Script_Builds_Registers_Launches_And_Captures_Page()
    {
        var script = ReadToolScript("Test-NoiraModernUwp.ps1");

        Assert.Contains("Build-NoiraModernUwp.ps1", script, StringComparison.Ordinal);
        Assert.Contains("'Publish'", script, StringComparison.Ordinal);
        Assert.Contains("Register-NoiraModernUwp.ps1", script, StringComparison.Ordinal);
        Assert.Contains("-SkipBuild", script, StringComparison.Ordinal);
        Assert.Contains("dev-command.json", script, StringComparison.Ordinal);
        Assert.Contains("dev-command-result.txt", script, StringComparison.Ordinal);
        Assert.Contains("Start-Process \"shell:AppsFolder\\$appUserModelId\"", script, StringComparison.Ordinal);
        Assert.Contains("Get-Process NoiraPlayer.App", script, StringComparison.Ordinal);
        Assert.Contains("System.Windows.Forms", script, StringComparison.Ordinal);
        Assert.Contains("CopyFromScreen", script, StringComparison.Ordinal);
        Assert.Contains("screenshotPath", script, StringComparison.Ordinal);
        Assert.Contains("Get-ScreenshotEvidence", script, StringComparison.Ordinal);
        Assert.Contains("screenshotLengthBytes", script, StringComparison.Ordinal);
        Assert.Contains("captureMode = 'desktop-screenshot'", script, StringComparison.Ordinal);
        Assert.Contains("$screenshotFile.Length -le 0", script, StringComparison.Ordinal);
        Assert.Contains("[int]$PostLaunchDelaySeconds = 45", script, StringComparison.Ordinal);
        Assert.Contains("[int]$ScreenshotStabilizationSeconds = 2", script, StringComparison.Ordinal);
        Assert.Contains("PostLaunchDelaySeconds must be greater than zero", script, StringComparison.Ordinal);
        Assert.Contains("ScreenshotStabilizationSeconds must not be negative", script, StringComparison.Ordinal);
        Assert.Contains("postLaunchDelaySeconds = $PostLaunchDelaySeconds", script, StringComparison.Ordinal);
        Assert.Contains("screenshotStabilizationSeconds = $ScreenshotStabilizationSeconds", script, StringComparison.Ordinal);
        Assert.Contains("Start-Sleep -Seconds $ScreenshotStabilizationSeconds", script, StringComparison.Ordinal);
        Assert.Contains("pageEvidence", script, StringComparison.Ordinal);
        Assert.Contains("home-page-evidence.json", script, StringComparison.Ordinal);
        Assert.Contains("Remove-HomePageEvidenceFile", script, StringComparison.Ordinal);
        Assert.Contains("Wait-ForHomePageSemanticEvidence", script, StringComparison.Ordinal);
        Assert.Contains("renderStage", script, StringComparison.Ordinal);
        Assert.Contains("'supplemental'", script, StringComparison.Ordinal);
        Assert.Contains("libraryCount", script, StringComparison.Ordinal);
        Assert.Contains("rowCount", script, StringComparison.Ordinal);
        Assert.Contains("semanticEvidence", script, StringComparison.Ordinal);
        Assert.Contains("semanticEvidenceStatus", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Modern_Verification_Script_Cleans_Up_App_Process_On_Failure()
    {
        var script = ReadToolScript("Test-NoiraModernUwp.ps1");

        Assert.Contains("function Stop-NoiraAppProcess", script, StringComparison.Ordinal);
        Assert.True(
            script.IndexOf("Stop-NoiraAppProcess", StringComparison.Ordinal) <
            script.IndexOf("$registerOutput = & powershell @registerArguments", StringComparison.Ordinal),
            "The verification gate should stop a stale app process before registration restages the loose AppX layout.");
        Assert.Contains("try", script, StringComparison.Ordinal);
        Assert.Contains("finally", script, StringComparison.Ordinal);
        Assert.Contains("if (-not $KeepRunning)", script, StringComparison.Ordinal);
        Assert.Contains("Stop-NoiraAppProcess", script.Substring(script.IndexOf("finally", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void Home_Page_Writes_Privacy_Safe_Semantic_Evidence_For_Modern_Page_Gate()
    {
        var homePage = ReadAppSource("Views", "HomePage.xaml.cs");

        Assert.Contains("HomePageEvidenceFileName = \"home-page-evidence.json\"", homePage, StringComparison.Ordinal);
        Assert.Contains("WriteHomePageEvidenceAsync", homePage, StringComparison.Ordinal);
        Assert.Contains("Windows.Data.Json.JsonObject", homePage, StringComparison.Ordinal);
        Assert.Contains("renderStage", homePage, StringComparison.Ordinal);
        Assert.Contains("supplemental", homePage, StringComparison.Ordinal);
        Assert.Contains("libraryCount", homePage, StringComparison.Ordinal);
        Assert.Contains("libraryPreviewCount", homePage, StringComparison.Ordinal);
        Assert.Contains("libraryPreviewMissingCount", homePage, StringComparison.Ordinal);
        Assert.Contains("rowCount", homePage, StringComparison.Ordinal);
        Assert.Contains("homeSectionCount", homePage, StringComparison.Ordinal);
        Assert.Contains("previewEvidenceStatus", homePage, StringComparison.Ordinal);
        Assert.Contains("InteractiveRequestMaxAttempts", homePage, StringComparison.Ordinal);
        Assert.Contains("RequiredInteractiveRequestMaxAttempts", homePage, StringComparison.Ordinal);
        Assert.Contains("TryLoadRequiredListAsync(() => client.GetUserViewsAsync(session))", homePage, StringComparison.Ordinal);
        Assert.Contains("TryGetRequiredListOrEmptyAsync", homePage, StringComparison.Ordinal);
        Assert.Contains("interactiveRequestMaxAttempts", homePage, StringComparison.Ordinal);
        Assert.Contains("requiredInteractiveRequestMaxAttempts", homePage, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "view.Name",
            homePage.Substring(homePage.IndexOf("private async Task WriteHomePageEvidenceAsync", StringComparison.Ordinal)),
            StringComparison.Ordinal);
    }

    private static string ReadToolScript(string fileName)
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", fileName));
    }

    private static string ReadAppSource(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot(), "src", "NoiraPlayer.App" }.Concat(pathParts).ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "Build-NoiraModernUwp.ps1")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
