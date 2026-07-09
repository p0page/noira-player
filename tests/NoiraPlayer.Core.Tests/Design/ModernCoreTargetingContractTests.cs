using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class ModernCoreTargetingContractTests
{
    [Fact]
    public void Core_Defaults_To_Modern_Net10_Target()
    {
        var coreProject = ReadRepositoryFile("src", "NoiraPlayer.Core", "NoiraPlayer.Core.csproj");

        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", coreProject, StringComparison.Ordinal);
        Assert.DoesNotContain("netstandard2.0", coreProject, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraEnableModernCoreTarget", coreProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<TargetFrameworks>", coreProject, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference Include=\"System.Text.Json\"", coreProject, StringComparison.Ordinal);
        Assert.Contains("<ProjectGuid>{3E3D8F22-1FD8-4A53-81D4-11998454C03B}</ProjectGuid>", coreProject, StringComparison.Ordinal);
    }

    [Fact]
    public void Modern_Build_Scripts_Do_Not_Need_Core_Target_Enable_Flags()
    {
        var unifiedScript = ReadRepositoryFile("tools", "Build-Noira.ps1");
        var modernScript = ReadRepositoryFile("tools", "Build-NoiraModernUwp.ps1");

        Assert.DoesNotContain("NoiraEnableModernCoreTarget", unifiedScript, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraEnableModernCoreTarget", modernScript, StringComparison.Ordinal);
        Assert.DoesNotContain("if ($Toolchain -eq 'Legacy')", unifiedScript, StringComparison.Ordinal);
        Assert.DoesNotContain("Resolve-LegacyMsBuildPath", unifiedScript, StringComparison.Ordinal);
    }

    [Fact]
    public void Modern_App_Consumes_Core_Project_And_Legacy_App_Project_Is_Removed()
    {
        var modernAppProject = ReadRepositoryFile("src", "NoiraPlayer.App", "NoiraPlayer.App.Modern.csproj");

        Assert.False(
            File.Exists(RepositoryPath("src", "NoiraPlayer.App", "NoiraPlayer.App.csproj")),
            "The old UAP app project should be removed once the modern .NET app is the primary entry.");
        Assert.Contains(@"..\NoiraPlayer.Core\NoiraPlayer.Core.csproj", modernAppProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<SetTargetFramework>", modernAppProject, StringComparison.Ordinal);
        Assert.Contains("<TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>", modernAppProject, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        return File.ReadAllText(RepositoryPath(segments));
    }

    private static string RepositoryPath(params string[] segments)
    {
        return Path.Combine(FindRepositoryRoot(), Path.Combine(segments));
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
