using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class ModernDevelopmentWorkflowDocTests
{
    [Fact]
    public void Readme_And_Development_Workflow_Document_Modern_Default_And_No_Legacy_Entry()
    {
        var readme = ReadRepositoryFile("README.md");
        var workflow = ReadRepositoryFile("docs", "development-workflow.md");

        Assert.Contains("Visual Studio 2026", readme, StringComparison.Ordinal);
        Assert.Contains(".NET SDK 10", readme, StringComparison.Ordinal);
        Assert.Contains("Windows SDK 10.0.26100.0", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("Windows SDK 10.0.22621.0", readme, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.sln", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.Legacy.sln", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.Modern.sln", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("VS2022", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("Visual Studio 2022 with UWP and C++ workloads", readme, StringComparison.Ordinal);
        Assert.Contains("tools\\Build-Noira.ps1 -Target Check -Configuration Debug -Platform x64", readme, StringComparison.Ordinal);
        Assert.Contains("tools\\Build-Noira.ps1 -Target Check -Configuration Release -Platform x64", readme, StringComparison.Ordinal);
        Assert.Contains("tools\\Build-Noira.ps1 -Target PlaybackCheck -Configuration Debug -Platform x64", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("tools\\Build-Noira.ps1 -Target MigrationCheck", readme, StringComparison.Ordinal);
        Assert.Contains("tools\\Build-Noira.ps1 -Target CutoverCheck -Platform x64", readme, StringComparison.Ordinal);
        Assert.Contains("primary local readiness gate", readme, StringComparison.Ordinal);
        Assert.Contains("modern-only cutover gate", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("archival reference", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("Register-NoiraLooseApp", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("tools\\Build-Noira.ps1 -Toolchain Legacy", readme, StringComparison.Ordinal);
        Assert.Contains("Native AOT/trimming warnings as blockers", readme, StringComparison.Ordinal);
        Assert.Contains("IL2xxx", readme, StringComparison.Ordinal);
        Assert.Contains("IL3xxx", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Program Files\\Microsoft Visual Studio\\2022", readme, StringComparison.Ordinal);

        Assert.Contains("Modern .NET / VS2026", workflow, StringComparison.Ordinal);
        Assert.Contains(".NET SDK 10", workflow, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.sln", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.Legacy.sln", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.Modern.sln", workflow, StringComparison.Ordinal);
        Assert.Contains("tools\\Build-Noira.ps1 -Target Check -Configuration Debug -Platform x64", workflow, StringComparison.Ordinal);
        Assert.Contains("tools\\Build-Noira.ps1 -Target Check -Configuration Release -Platform x64", workflow, StringComparison.Ordinal);
        Assert.Contains("tools\\Build-Noira.ps1 -Target PlaybackCheck -Configuration Debug -Platform x64", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("tools\\Build-Noira.ps1 -Target MigrationCheck", workflow, StringComparison.Ordinal);
        Assert.Contains("tools\\Build-Noira.ps1 -Target CutoverCheck -Platform x64", workflow, StringComparison.Ordinal);
        Assert.Contains("primary local readiness gate", workflow, StringComparison.Ordinal);
        Assert.Contains("modern-only cutover gate", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("archival reference", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Register-NoiraLooseApp", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("tools\\Build-Noira.ps1 -Toolchain Legacy", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Visual Studio 2022 legacy fallback", workflow, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), Path.Combine(segments)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md")) &&
                File.Exists(Path.Combine(directory.FullName, "NoiraPlayer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
