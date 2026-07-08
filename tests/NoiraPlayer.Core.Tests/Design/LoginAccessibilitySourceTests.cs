using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class LoginAccessibilitySourceTests
{
    [Fact]
    public void Login_Page_Default_And_Failed_Login_Focus_Stay_In_Editable_Form()
    {
        var root = FindRepositoryRoot();
        var mainPageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "MainPage.xaml.cs"));
        var loginPageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "LoginPage.xaml.cs"));

        Assert.Contains("pageType == typeof(LoginPage)", mainPageSource);
        Assert.Contains("ShellContentMode.Login", mainPageSource);
        Assert.Contains("Page, ITvContentFocusTarget", loginPageSource);
        Assert.Contains("public bool FocusDefaultContent()", loginPageSource);
        Assert.Contains("FocusFailedLoginField();", loginPageSource);
        Assert.Contains("ServerUrlBox.Focus(FocusState.Keyboard)", loginPageSource);
        Assert.Contains("UserNameBox.Focus(FocusState.Keyboard)", loginPageSource);
        Assert.Contains("PasswordBox.Focus(FocusState.Keyboard)", loginPageSource);
    }

    [Fact]
    public void Login_Page_Controls_Route_Directional_Focus_Through_Form_Order()
    {
        var root = FindRepositoryRoot();
        var loginXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "LoginPage.xaml"));
        var loginPageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "LoginPage.xaml.cs"));

        Assert.DoesNotContain("KeyDown=\"LoginControl_OnKeyDown\"", loginXaml);
        Assert.Contains("RegisterLoginDirectionalFocusHandlers();", loginPageSource);
        Assert.Contains("control.AddHandler(KeyDownEvent", loginPageSource);
        Assert.Contains("handledEventsToo: true", loginPageSource);
        Assert.Contains("private void LoginControl_OnKeyDown", loginPageSource);
        Assert.Contains("VirtualKey.Down", loginPageSource);
        Assert.Contains("VirtualKey.Up", loginPageSource);
        Assert.Contains("MoveLoginFocus(sender, 1)", loginPageSource);
        Assert.Contains("MoveLoginFocus(sender, -1)", loginPageSource);
        Assert.Contains("new Control[] { ServerUrlBox, UserNameBox, PasswordBox, ConnectButton }", loginPageSource);
    }

    [Fact]
    public void Login_Page_Uses_Matte_Utility_Form_Styles()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "App.xaml"));
        var loginXaml = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "LoginPage.xaml"));
        var loginPageSource = File.ReadAllText(Path.Combine(root, "src", "NoiraPlayer.App", "Views", "LoginPage.xaml.cs"));

        Assert.Contains("TvUtilityFormPanelStyle", appXaml);
        Assert.Contains("TvUtilityFieldTextBoxStyle", appXaml);
        Assert.Contains("TvUtilityFieldPasswordBoxStyle", appXaml);
        Assert.Contains("TvUtilityCommandButtonStyle", appXaml);
        Assert.Contains("Style=\"{StaticResource TvUtilityFormPanelStyle}\"", loginXaml);
        Assert.Contains("Style=\"{StaticResource TvUtilityFieldTextBoxStyle}\"", loginXaml);
        Assert.Contains("Style=\"{StaticResource TvUtilityFieldPasswordBoxStyle}\"", loginXaml);
        Assert.Contains("Style=\"{StaticResource TvUtilityCommandButtonStyle}\"", loginXaml);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(ConnectButton)", loginPageSource);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "DESIGN.md")) &&
                File.Exists(Path.Combine(directory.FullName, "src", "NoiraPlayer.App", "MainPage.xaml.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
