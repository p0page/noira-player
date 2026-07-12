using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class PlaybackPageInputRoutingSourceTests
{
    [Fact]
    public void Playback_Page_Uses_CoreWindow_As_The_Single_Global_Key_Handler()
    {
        var root = FindRepositoryRoot();
        var pageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs"));
        var pageXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml"));

        Assert.Contains("CoreWindow.KeyDown += PlaybackPage_OnCoreWindowKeyDown", pageSource, StringComparison.Ordinal);
        Assert.Contains("CoreWindow.KeyDown -= PlaybackPage_OnCoreWindowKeyDown", pageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHandler(KeyDownEvent", pageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyDown=\"Page_OnKeyDown\"", pageXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Hybrid_Shell_Debug_Command_Bypasses_Web_Ui_For_All_Quality_Run_Sources()
    {
        var root = FindRepositoryRoot();
        var mainPageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "MainPage.xaml.cs"));
        var launchRequestSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Navigation",
            "PlaybackLaunchRequest.cs"));

        Assert.Contains("TryRunDevelopmentPlaybackCommandAsync", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("command.Route != \"quality-run\"", mainPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "command.Route != \"quality-run\" ||\r\n                    string.IsNullOrWhiteSpace(command.StreamUrl)",
            mainPageSource,
            StringComparison.Ordinal);
        Assert.Contains("await file.DeleteAsync(StorageDeleteOption.PermanentDelete);", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("PlaybackLaunchRequest.FromDevelopmentQualityRun", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("FromDevelopmentQualityRun", launchRequestSource, StringComparison.Ordinal);
        Assert.DoesNotContain("typeof(HomePage)", mainPageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("typeof(LoginPage)", mainPageSource, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
