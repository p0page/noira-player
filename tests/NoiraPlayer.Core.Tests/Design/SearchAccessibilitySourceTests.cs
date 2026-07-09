using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class SearchAccessibilitySourceTests
{
    [Fact]
    public void Search_Error_Development_Route_Renders_Keyboard_Recovery_State()
    {
        var root = FindRepositoryRoot();
        var mainPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "MainPage.xaml.cs"));
        var searchPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "SearchPage.xaml.cs"));
        var projectSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "NoiraPlayer.App.csproj"));
        var requestSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Navigation",
            "SearchDevelopmentNavigationRequest.cs"));

        Assert.Contains("case \"search-error\"", mainPageSource);
        Assert.Contains("SearchDevelopmentNavigationRequest", mainPageSource);
        Assert.Contains("SearchDevelopmentNavigationRequest", searchPageSource);
        Assert.Contains("RenderDevelopmentSearchError()", searchPageSource);
        Assert.Contains("\"Unable to search\"", searchPageSource);
        Assert.Contains("\"Check the server connection, then try again.\"", searchPageSource);
        Assert.Contains("showRetry: true", searchPageSource);
        Assert.Contains("Navigation\\SearchDevelopmentNavigationRequest.cs", projectSource);
        Assert.DoesNotContain("RecentTerms", requestSource);
        Assert.DoesNotContain("_developmentRequest.RecentTerms.Count > 0", searchPageSource);
    }

    [Fact]
    public void Search_Box_Down_Key_Routes_To_Selected_Scope()
    {
        var searchPageSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
            "Views",
            "SearchPage.xaml.cs"));

        Assert.Contains("if (IsDownKey(e.Key))", searchPageSource);
        Assert.Contains("FocusSelectedScope(FocusState.Keyboard)", searchPageSource);
    }

    [Fact]
    public void Search_Box_Registers_Handled_Textbox_Enter_For_Submit()
    {
        var root = FindRepositoryRoot();
        var searchPageXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "SearchPage.xaml"));
        var searchPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "SearchPage.xaml.cs"));

        Assert.DoesNotContain("KeyDown=\"SearchBox_OnKeyDown\"", searchPageXaml);
        Assert.Contains("SearchBox.AddHandler(KeyDownEvent, new KeyEventHandler(SearchBox_OnKeyDown), true);", searchPageSource);
        Assert.Contains("e.Key == VirtualKey.Enter", searchPageSource);
        Assert.Contains("await SearchAsync();", searchPageSource);
    }

    [Fact]
    public void Search_Box_Does_Not_Submit_Handled_GamepadA_Textbox_Activation()
    {
        var searchPageSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
            "Views",
            "SearchPage.xaml.cs"));

        Assert.Contains("var keyAlreadyHandled = e.Handled;", searchPageSource);
        Assert.Contains("(!keyAlreadyHandled && e.Key == VirtualKey.GamepadA)", searchPageSource);
        Assert.DoesNotContain("e.Key == VirtualKey.Enter || e.Key == VirtualKey.GamepadA", searchPageSource);
    }

    [Fact]
    public void Search_Empty_State_Left_Right_Keys_Move_Between_Recovery_Actions()
    {
        var searchPageSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NoiraPlayer.App",
            "Views",
            "SearchPage.xaml.cs"));

        Assert.Contains("TryMoveBetweenEmptyStateActions(e.Key)", searchPageSource);
        Assert.Contains("EmptyRetryButton.Focus(FocusState.Keyboard)", searchPageSource);
        Assert.Contains("EmptyEditButton.Focus(FocusState.Keyboard)", searchPageSource);
    }

    [Fact]
    public void Search_Page_Renders_Recent_Terms_As_Controller_Targets()
    {
        var root = FindRepositoryRoot();
        var searchPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "SearchPage.xaml.cs"));
        var searchPageXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "SearchPage.xaml"));
        var projectSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "NoiraPlayer.App.csproj"));

        Assert.Contains("RecentSearchTermStore", searchPageSource);
        Assert.Contains("RenderRecentTerms()", searchPageSource);
        Assert.Contains("RecentTerm_OnClick", searchPageSource);
        Assert.Contains("FocusFirstRecentTerm", searchPageSource);
        Assert.Contains("MoveRecentTermFocus", searchPageSource);
        Assert.Contains("_recentSearchTermStore.Load()", searchPageSource);
        Assert.Contains("_recentSearchTermStore.Add(term)", searchPageSource);
        Assert.Contains("RecentSearchesPanel", searchPageXaml);
        Assert.Contains("RecentSearchTermsPanel", searchPageXaml);
        Assert.Contains("Storage\\RecentSearchTermStore.cs", projectSource);
    }

    [Fact]
    public void Search_Recent_Term_Buttons_Use_Shared_Tv_Style_Tokens()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "App.xaml"));
        var searchPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "SearchPage.xaml.cs"));

        Assert.Contains("TvSearchRecentTermMinHeight", appXaml);
        Assert.Contains("TvSearchRecentTermMinWidth", appXaml);
        Assert.Contains("TvSearchRecentTermMaxWidth", appXaml);
        Assert.Contains("TvSearchRecentTermPadding", appXaml);
        Assert.Contains("TvSearchRecentTermButtonStyle", appXaml);
        Assert.Contains("ApplyRecentTermButtonStyle(button)", searchPageSource);
        Assert.DoesNotContain("MinWidth = 112", searchPageSource);
        Assert.DoesNotContain("MaxWidth = 260", searchPageSource);
    }

    [Fact]
    public void Search_Utility_Controls_Use_Matte_Form_And_Command_Styles()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "App.xaml"));
        var searchPageXaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "SearchPage.xaml"));
        var searchPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NoiraPlayer.App",
            "Views",
            "SearchPage.xaml.cs"));

        Assert.Contains("TvUtilityFieldTextBoxStyle", appXaml);
        Assert.Contains("TvUtilityCommandButtonStyle", appXaml);
        Assert.Contains("TvUtilitySearchScopeButtonStyle", appXaml);
        Assert.Contains("Style=\"{StaticResource TvUtilityFieldTextBoxStyle}\"", searchPageXaml);
        Assert.Contains("Style=\"{StaticResource TvUtilityCommandButtonStyle}\"", searchPageXaml);
        Assert.Contains("ApplyScopeButtonStyle(button)", searchPageSource);
        Assert.Contains("UpdateScopeButtonVisual(button", searchPageSource);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(SearchActionButton)", searchPageSource);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(EmptyEditButton)", searchPageSource);
        Assert.Contains("MatteButtonFocusVisuals.PrepareCommandButton(EmptyRetryButton)", searchPageSource);
        Assert.DoesNotContain("UseSystemFocusVisuals=\"True\"", searchPageXaml);
        Assert.DoesNotContain("UseSystemFocusVisuals = true", searchPageSource);
        Assert.DoesNotContain("AppAccentBrush", searchPageSource);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "Generate-AppIconAssets.ps1")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
