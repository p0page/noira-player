using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class WebViewGamepadInputSourceTests
{
    [Fact]
    public void App_Owns_One_Gamepad_Registry_And_Global_Input_Router()
    {
        var root = FindRepositoryRoot();
        var appSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "App.xaml.cs"));
        var mainPageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "MainPage.xaml.cs"));
        var registrySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Input",
            "GamepadDeviceRegistry.cs"));

        Assert.Contains("GamepadDeviceRegistry", appSource, StringComparison.Ordinal);
        Assert.Contains("GlobalInputRouter", appSource, StringComparison.Ordinal);
        Assert.Contains("Gamepad.GamepadAdded +=", registrySource, StringComparison.Ordinal);
        Assert.Contains("Gamepad.GamepadRemoved +=", registrySource, StringComparison.Ordinal);
        Assert.Contains("Gamepad.Gamepads", registrySource, StringComparison.Ordinal);
        Assert.Contains("GetCurrentReading()", registrySource, StringComparison.Ordinal);
        Assert.Contains("GamepadInputPump<Gamepad>", registrySource, StringComparison.Ordinal);
        Assert.Contains("Dictionary<Gamepad, GamepadInputNormalizerState>", registrySource, StringComparison.Ordinal);
        Assert.Contains("GamepadInputFailureStage.DeviceReading", registrySource, StringComparison.Ordinal);
        Assert.Contains("GamepadInputFailureStage.StateTransition", registrySource, StringComparison.Ordinal);
        Assert.Contains("GamepadInputFailureStage.Consumer", registrySource, StringComparison.Ordinal);
        Assert.True(
            registrySource.IndexOf("Gamepad.GamepadAdded +=", StringComparison.Ordinal) <
            registrySource.IndexOf("foreach (var gamepad in Gamepad.Gamepads)", StringComparison.Ordinal));
        Assert.DoesNotContain("Gamepad.Gamepads", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("InputContext.BrowseWeb", mainPageSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Web_Input_Transport_Is_Ready_Gated_And_Centrally_Serialized()
    {
        var root = FindRepositoryRoot();
        var mainPageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "MainPage.xaml.cs"));
        var serializerSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Web",
            "WebHostInputMessageSerializer.cs"));
        var controlSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Web",
            "WebHostControlMessage.cs"));

        Assert.Contains("host.ready", controlSource, StringComparison.Ordinal);
        Assert.Contains("_webInputReady", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("WebHostInputMessageSerializer.Serialize", mainPageSource, StringComparison.Ordinal);
        Assert.Contains("\\\"version\\\":1", serializerSource, StringComparison.Ordinal);
        Assert.Contains("input.Sequence", serializerSource, StringComparison.Ordinal);
        Assert.Contains("input.TimestampMilliseconds", serializerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("{\"type\":\"host.input\"", mainPageSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaving_Browse_Marks_The_Cached_Web_Transport_Not_Ready()
    {
        var root = FindRepositoryRoot();
        var mainPageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "MainPage.xaml.cs"));
        var unloadStart = mainPageSource.IndexOf("private void MainPage_OnUnloaded", StringComparison.Ordinal);
        var unloadEnd = mainPageSource.IndexOf("private async Task InitializeWebViewAsync", unloadStart, StringComparison.Ordinal);

        Assert.True(unloadStart >= 0);
        Assert.True(unloadEnd > unloadStart);
        var unloadSource = mainPageSource.Substring(unloadStart, unloadEnd - unloadStart);
        Assert.Contains("MarkWebInputNotReady();", unloadSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_Web_Shell_Activation_Requests_Home_Without_Interrupting_Native_Playback()
    {
        var root = FindRepositoryRoot();
        var appSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "App.xaml.cs"));
        var mainPageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "MainPage.xaml.cs"));

        Assert.Contains("else if (rootFrame.Content is MainPage mainPage)", appSource, StringComparison.Ordinal);
        Assert.Contains("mainPage.NavigateHome();", appSource, StringComparison.Ordinal);
        Assert.Contains("activated-home", mainPageSource, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.App", "MainPage.xaml.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
