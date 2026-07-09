using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class ModernToolchainHelperContractTests
{
    [Fact]
    public void Modern_Toolchain_Functions_Are_Shared_By_Modern_Entry_Points()
    {
        var helperPath = GetRepositoryPath("tools", "NoiraModernToolchain.ps1");
        Assert.True(File.Exists(helperPath), "Modern toolchain helper must exist.");

        var helper = File.ReadAllText(helperPath);
        var unifiedEntryPoint = ReadRepositoryFile("tools", "Build-Noira.ps1");
        var directModernBuild = ReadRepositoryFile("tools", "Build-NoiraModernUwp.ps1");
        var registerScript = ReadRepositoryFile("tools", "Register-NoiraModernUwp.ps1");

        Assert.Contains("function Test-MsBuildHasModernUwpTargets", helper, StringComparison.Ordinal);
        Assert.Contains("function Resolve-ModernMsBuildPath", helper, StringComparison.Ordinal);
        Assert.Contains("function Assert-DotNetSdkSupportsModernNet", helper, StringComparison.Ordinal);
        Assert.Contains("'--list-sdks'", helper, StringComparison.Ordinal);
        Assert.Contains("[version]'10.0'", helper, StringComparison.Ordinal);
        Assert.Contains("Modern .NET toolchain requires .NET SDK 10", helper, StringComparison.Ordinal);

        AssertUsesToolchainHelper(unifiedEntryPoint);
        AssertUsesToolchainHelper(directModernBuild);
        AssertUsesToolchainHelper(registerScript);
        AssertDoesNotRedefineModernToolchainFunctions(unifiedEntryPoint);
        AssertDoesNotRedefineModernToolchainFunctions(directModernBuild);
        AssertDoesNotRedefineModernToolchainFunctions(registerScript);

        Assert.Contains("Assert-DotNetSdkSupportsModernNet", registerScript, StringComparison.Ordinal);
        Assert.Contains("Resolve-ModernMsBuildPath $MsBuildPath", registerScript, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\Program\MSBuild\Current\Bin\MSBuild.exe", registerScript, StringComparison.Ordinal);
    }

    private static void AssertUsesToolchainHelper(string script)
    {
        Assert.Contains("NoiraModernToolchain.ps1", script, StringComparison.Ordinal);
        Assert.Contains(". $modernToolchainScriptPath", script, StringComparison.Ordinal);
    }

    private static void AssertDoesNotRedefineModernToolchainFunctions(string script)
    {
        Assert.DoesNotContain("function Test-MsBuildHasModernUwpTargets", script, StringComparison.Ordinal);
        Assert.DoesNotContain("function Resolve-ModernMsBuildPath", script, StringComparison.Ordinal);
        Assert.DoesNotContain("function Assert-DotNetSdkSupportsModernNet", script, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        return File.ReadAllText(GetRepositoryPath(segments));
    }

    private static string GetRepositoryPath(params string[] segments)
    {
        return Path.Combine(FindRepositoryRoot(), Path.Combine(segments));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NoiraPlayer.sln")) &&
                File.Exists(Path.Combine(directory.FullName, "tools", "Build-Noira.ps1")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
