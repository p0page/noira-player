using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class ModernUwpSolutionContractTests
{
    [Fact]
    public void Primary_Solution_Is_Modern_And_Legacy_Entry_Files_Are_Removed()
    {
        var primarySolution = ReadRepositoryFile("NoiraPlayer.sln");

        Assert.Contains("# Visual Studio Version 18", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.App.Modern.csproj", primarySolution, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.App.csproj", primarySolution, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.Legacy.sln", primarySolution, StringComparison.Ordinal);
        Assert.DoesNotContain("NoiraPlayer.Modern.sln", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.Core.csproj", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.Native.vcxproj", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.Core.Tests.csproj", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.PlaybackQuality.Headless.csproj", primarySolution, StringComparison.Ordinal);
        Assert.Contains("NoiraPlayer.PlaybackQuality.Cli.csproj", primarySolution, StringComparison.Ordinal);
        Assert.Contains("Debug|x64", primarySolution, StringComparison.Ordinal);
        Assert.Contains("Release|x64", primarySolution, StringComparison.Ordinal);
        Assert.DoesNotContain("Debug|x86", primarySolution, StringComparison.Ordinal);
        Assert.False(File.Exists(RepositoryPath("NoiraPlayer.Legacy.sln")), "The VS2022 legacy solution should be removed from the modernized tree.");
        Assert.False(File.Exists(RepositoryPath("src", "NoiraPlayer.App", "NoiraPlayer.App.csproj")), "The old UAP app project should be removed from the modernized tree.");
        Assert.False(File.Exists(RepositoryPath("tools", "Register-NoiraLooseApp.ps1")), "The old VS2022 loose deploy helper should be removed from the modernized tree.");
        Assert.False(File.Exists(RepositoryPath("tools", "Register-NoiraLooseApp.tests.ps1")), "The old VS2022 loose deploy helper tests should be removed with the helper.");
        Assert.True(File.Exists(RepositoryPath("tools", "Register-NoiraModernUwp.ps1")), "The modern loose deploy helper should remain the supported registration entry.");
    }

    [Fact]
    public void Modern_App_Project_Remains_Uwp_Msix_Appcontainer_And_Xbox_Compatible()
    {
        var modernProject = ReadRepositoryFile("src", "NoiraPlayer.App", "NoiraPlayer.App.Modern.csproj");
        var manifest = ReadRepositoryFile("src", "NoiraPlayer.App", "Package.appxmanifest");

        Assert.Contains("<NoiraPlatformCompatibility>UWP-MSIX-AppContainer-Xbox</NoiraPlatformCompatibility>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<UseUwp>true</UseUwp>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<EnableMsixTooling>true</EnableMsixTooling>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<ApplicationManifest>Package.appxmanifest</ApplicationManifest>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<Platforms>x64</Platforms>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>", modernProject, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"Microsoft.UI.Xaml\" Version=\"2.8.7\" />", modernProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.WindowsAppSDK", modernProject, StringComparison.Ordinal);
        Assert.DoesNotContain("WinUI3", modernProject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<TargetDeviceFamily Name=\"Windows.Universal\" MinVersion=\"10.0.19041.0\" MaxVersionTested=\"10.0.26100.0\" />", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxVersionTested=\"10.0.22621.0\"", manifest, StringComparison.Ordinal);
        Assert.Contains("<rescap:Capability Name=\"hevcPlayback\" />", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Project_Uses_VS2026_Cpp_Toolset_And_Modern_Windows_Sdk()
    {
        var nativeProject = ReadRepositoryFile("src", "NoiraPlayer.Native", "NoiraPlayer.Native.vcxproj");

        Assert.Contains("<MinimumVisualStudioVersion>18.0</MinimumVisualStudioVersion>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<PlatformToolset>v145</PlatformToolset>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<WindowsTargetPlatformVersion>10.0.26100.0</WindowsTargetPlatformVersion>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<WindowsTargetPlatformMinVersion>10.0.19041.0</WindowsTargetPlatformMinVersion>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<LanguageStandard>stdcpp20</LanguageStandard>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<CompileAsWinRT>false</CompileAsWinRT>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("<CppWinRTEnableLegacyCoroutines>false</CppWinRTEnableLegacyCoroutines>", nativeProject, StringComparison.Ordinal);
        Assert.Contains("/utf-8", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<PlatformToolset>v143</PlatformToolset>", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<MinimumVisualStudioVersion>17.0</MinimumVisualStudioVersion>", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<LanguageStandard>stdcpp17</LanguageStandard>", nativeProject, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_Project_Uses_Modern_CppWinRT_And_Current_Ffmpeg_Packages()
    {
        var nativeProject = ReadRepositoryFile("src", "NoiraPlayer.Native", "NoiraPlayer.Native.vcxproj");
        var packagesConfig = ReadRepositoryFile("src", "NoiraPlayer.Native", "packages.config");

        Assert.Contains("<package id=\"Microsoft.Windows.CppWinRT\" version=\"3.0.260520.1\" targetFramework=\"native\" />", packagesConfig, StringComparison.Ordinal);
        Assert.Contains("<package id=\"FFmpegInteropX.UWP.FFmpeg\" version=\"8.1.2\" targetFramework=\"native\" />", packagesConfig, StringComparison.Ordinal);
        Assert.Contains("packages\\Microsoft.Windows.CppWinRT.3.0.260520.1\\build\\native\\Microsoft.Windows.CppWinRT.props", nativeProject, StringComparison.Ordinal);
        Assert.Contains("packages\\Microsoft.Windows.CppWinRT.3.0.260520.1\\build\\native\\Microsoft.Windows.CppWinRT.targets", nativeProject, StringComparison.Ordinal);
        Assert.Contains("packages\\FFmpegInteropX.UWP.FFmpeg.8.1.2\\build\\native\\FFmpegInteropX.UWP.FFmpeg.targets", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Windows.CppWinRT.2.0.220531.1", nativeProject, StringComparison.Ordinal);
        Assert.DoesNotContain("version=\"2.0.220531.1\"", packagesConfig, StringComparison.Ordinal);
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
