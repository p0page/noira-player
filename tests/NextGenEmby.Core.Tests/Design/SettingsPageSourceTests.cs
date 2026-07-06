using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class SettingsPageSourceTests
{
    [Fact]
    public void Settings_Page_Renders_Controller_Reachable_Sign_Out_Action()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "App.xaml"));
        var settingsXaml = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "Views", "SettingsPage.xaml"));
        var settingsSource = File.ReadAllText(Path.Combine(root, "src", "NextGenEmby.App", "Views", "SettingsPage.xaml.cs"));

        Assert.Contains("TvSettingsAccountActionButtonStyle", appXaml);
        Assert.Contains("TvSettingsDangerButtonStyle", appXaml);
        Assert.Contains("x:Name=\"SignOutButton\"", settingsXaml);
        Assert.Contains("AutomationProperties.Name=\"Sign out of Emby\"", settingsXaml);
        Assert.Contains("Style=\"{StaticResource TvSettingsAccountActionButtonStyle}\"", settingsXaml);
        Assert.Contains("x:Name=\"SignOutConfirmLayer\"", settingsXaml);
        Assert.Contains("x:Name=\"ConfirmSignOutButton\"", settingsXaml);
        Assert.Contains("x:Name=\"CancelSignOutButton\"", settingsXaml);
        Assert.Contains("Style=\"{StaticResource TvSettingsDangerButtonStyle}\"", settingsXaml);
        Assert.Contains("SignOutButton_OnClick", settingsSource);
        Assert.Contains("ConfirmSignOutButton_OnClick", settingsSource);
        Assert.Contains("CancelSignOutButton_OnClick", settingsSource);
        Assert.Contains("RegisterSettingsDirectionalFocusHandlers()", settingsSource);
        Assert.Contains("SettingsControl_OnKeyDown", settingsSource);
        Assert.Contains("MoveSettingsFocus(sender, 1)", settingsSource);
        Assert.Contains("MoveSettingsFocus(sender, -1)", settingsSource);
        Assert.Contains("await _sessionStore.ClearAsync()", settingsSource);
        Assert.Contains("Frame.Navigate(typeof(LoginPage))", settingsSource);
        Assert.Contains("Frame.BackStack.Clear()", settingsSource);
        Assert.Contains("CancelSignOutButton.Focus(FocusState.Keyboard)", settingsSource);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "DESIGN.md")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NextGenEmby.App", "Views", "SettingsPage.xaml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
