using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class PlaybackPageInputRoutingSourceTests
{
    [Fact]
    public void Playback_Page_Separates_Desktop_Keyboard_From_App_Level_Gamepad_Input()
    {
        var root = FindRepositoryRoot();
        var pageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml.cs"));
        var pageXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "PlaybackPage.xaml"));

        Assert.Contains("CoreWindow.KeyDown += PlaybackPage_OnCoreWindowKeyDown", pageSource, StringComparison.Ordinal);
        Assert.Contains("CoreWindow.KeyDown -= PlaybackPage_OnCoreWindowKeyDown", pageSource, StringComparison.Ordinal);
        Assert.Contains("InputContext.NativePlayback", pageSource, StringComparison.Ordinal);
        Assert.Contains("PlaybackPage_OnGamepadInput", pageSource, StringComparison.Ordinal);
        Assert.Contains("IsGamepadVirtualKey(args.VirtualKey)", pageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Gamepad.Gamepads", pageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHandler(KeyDownEvent", pageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyDown=\"Page_OnKeyDown\"", pageXaml, StringComparison.Ordinal);

        var unloadStart = pageSource.IndexOf("private async void PlaybackPage_OnUnloaded", StringComparison.Ordinal);
        var unloadEnd = pageSource.IndexOf("private async void SourceBox_OnSelectionChanged", unloadStart, StringComparison.Ordinal);
        Assert.True(unloadStart >= 0);
        Assert.True(unloadEnd > unloadStart);
        var unloadSource = pageSource.Substring(unloadStart, unloadEnd - unloadStart);
        Assert.Contains("DetachPlaybackInput();", unloadSource, StringComparison.Ordinal);
        Assert.True(
            unloadSource.IndexOf("DetachPlaybackInput();", StringComparison.Ordinal) <
            unloadSource.IndexOf("await ", StringComparison.Ordinal));
        Assert.Contains("if (_inputRegistration != null)", pageSource, StringComparison.Ordinal);
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
