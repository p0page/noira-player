using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class ModernToolTargetingContractTests
{
    [Fact]
    public void Test_And_Playback_Tool_Projects_Default_To_Net10()
    {
        var testsProject = ReadRepositoryFile("tests", "NoiraPlayer.Core.Tests", "NoiraPlayer.Core.Tests.csproj");
        var headlessProject = ReadRepositoryFile("tools", "NoiraPlayer.PlaybackQuality.Headless", "NoiraPlayer.PlaybackQuality.Headless.csproj");
        var cliProject = ReadRepositoryFile("tools", "NoiraPlayer.PlaybackQuality.Cli", "NoiraPlayer.PlaybackQuality.Cli.csproj");

        AssertModernToolTarget(testsProject);
        AssertModernToolTarget(headlessProject);
        AssertModernToolTarget(cliProject);
    }

    [Fact]
    public void Modern_Build_And_Playback_Gates_Run_Tooling_On_Net10()
    {
        var buildScript = ReadRepositoryFile("tools", "Build-Noira.ps1");
        var playbackScript = ReadRepositoryFile("tools", "Test-NoiraModernPlaybackQuality.ps1");
        var playbackCliSmokeScript = ReadRepositoryFile("tools", "quality-run", "run-playback-quality-cli-smoke-test.ps1");

        Assert.DoesNotContain("NoiraEnableModernToolTarget", buildScript, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraEnableModernCoreTarget", buildScript, StringComparison.Ordinal);
        Assert.Contains("'-f'", buildScript, StringComparison.Ordinal);
        Assert.Contains("'net10.0'", buildScript, StringComparison.Ordinal);

        Assert.DoesNotContain("NoiraEnableModernToolTarget", playbackScript, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraEnableModernCoreTarget", playbackScript, StringComparison.Ordinal);
        Assert.Contains("'--framework'", playbackScript, StringComparison.Ordinal);
        Assert.Contains("'net10.0'", playbackScript, StringComparison.Ordinal);

        Assert.Contains("bin\\Debug\\net10.0\\NoiraPlayer.PlaybackQuality.Cli.dll", playbackCliSmokeScript, StringComparison.Ordinal);
        Assert.DoesNotContain("bin\\Debug\\net9.0\\NoiraPlayer.PlaybackQuality.Cli.dll", playbackCliSmokeScript, StringComparison.Ordinal);
    }

    private static void AssertModernToolTarget(string project)
    {
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("net9.0", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraEnableModernToolTarget", project, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraEnableModernCoreTarget", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<TargetFrameworks>", project, StringComparison.Ordinal);
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
            if (File.Exists(Path.Combine(directory.FullName, "NoiraPlayer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
