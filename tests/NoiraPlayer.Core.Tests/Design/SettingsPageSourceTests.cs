using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class SettingsPageSourceTests
{
    [Fact]
    public void Settings_Page_Renders_Controller_Reachable_Sign_Out_Action()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "App.xaml"));
        var settingsXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "SettingsPage.xaml"));
        var settingsSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "SettingsPage.xaml.cs"));

        Assert.Contains("TvUtilityCommandButtonStyle", appXaml);
        Assert.Contains("TvUtilityDangerButtonStyle", appXaml);
        Assert.Contains("x:Name=\"SignOutButton\"", settingsXaml);
        Assert.Contains("AutomationProperties.Name=\"Sign out of Emby\"", settingsXaml);
        Assert.Contains("Style=\"{StaticResource TvUtilityCommandButtonStyle}\"", settingsXaml);
        Assert.Contains("x:Name=\"SignOutConfirmLayer\"", settingsXaml);
        Assert.Contains("x:Name=\"ConfirmSignOutButton\"", settingsXaml);
        Assert.Contains("x:Name=\"CancelSignOutButton\"", settingsXaml);
        Assert.Contains("Style=\"{StaticResource TvUtilityDangerButtonStyle}\"", settingsXaml);
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

    [Fact]
    public void Settings_Page_Uses_Matte_Utility_Focus_And_Secondary_Diagnostics()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "App.xaml"));
        var settingsXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "SettingsPage.xaml"));
        var settingsSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "SettingsPage.xaml.cs"));

        Assert.Contains("TvUtilityCommandButtonStyle", appXaml);
        Assert.Contains("TvUtilityDangerButtonStyle", appXaml);
        Assert.Contains("TvDiagnosticsPanelStyle", appXaml);
        Assert.Contains("Style=\"{StaticResource TvUtilityCommandButtonStyle}\"", settingsXaml);
        Assert.Contains("Style=\"{StaticResource TvUtilityDangerButtonStyle}\"", settingsXaml);
        Assert.Contains("Style=\"{StaticResource TvDiagnosticsPanelStyle}\"", settingsXaml);
        Assert.Contains("<Setter Property=\"UseSystemFocusVisuals\" Value=\"False\" />", appXaml);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(SignOutButton)", settingsSource);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(CancelSignOutButton)", settingsSource);
        Assert.Contains("MatteButtonFocusVisuals.PrepareDangerButton(ConfirmSignOutButton)", settingsSource);
    }

    [Fact]
    public void Settings_Diagnostics_Details_Are_Behind_Controller_Reachable_Disclosure()
    {
        var root = FindRepositoryRoot();
        var settingsXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "SettingsPage.xaml"));
        var settingsSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "SettingsPage.xaml.cs"));
        var diagnosticsPanel = SliceFrom(settingsXaml, "x:Name=\"DiagnosticsToggleButton\"", "</Border>");

        Assert.Contains("x:Name=\"DiagnosticsToggleButton\"", settingsXaml);
        Assert.Contains("Click=\"DiagnosticsToggleButton_OnClick\"", settingsXaml);
        Assert.Contains("Style=\"{StaticResource TvUtilityCommandButtonStyle}\"", diagnosticsPanel);
        Assert.Contains("x:Name=\"DiagnosticsDetailsPanel\"", diagnosticsPanel);
        Assert.Contains("Visibility=\"Collapsed\"", diagnosticsPanel);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(DiagnosticsToggleButton)", settingsSource);
        Assert.Contains("new Control[] { SignOutButton, ThumbstickSeekPreviewCheckBox, DiagnosticsToggleButton }", settingsSource);
        Assert.Contains("DiagnosticsToggleButton_OnClick", settingsSource);
        Assert.Contains("DiagnosticsDetailsPanel.Visibility = _diagnosticsExpanded ? Visibility.Visible : Visibility.Collapsed", settingsSource);
    }

    private static string SliceFrom(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing source marker " + startMarker);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, "Missing source marker " + endMarker);
        return source.Substring(start, end - start);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "DESIGN.md")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.App", "Views", "SettingsPage.xaml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
