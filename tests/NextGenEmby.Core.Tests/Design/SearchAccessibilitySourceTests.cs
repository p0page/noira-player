using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class SearchAccessibilitySourceTests
{
    [Fact]
    public void Search_Error_Development_Route_Renders_Keyboard_Recovery_State()
    {
        var root = FindRepositoryRoot();
        var mainPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "MainPage.xaml.cs"));
        var searchPageSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "Views",
            "SearchPage.xaml.cs"));
        var projectSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NextGenEmby.App",
            "NextGenEmby.App.csproj"));

        Assert.Contains("case \"search-error\"", mainPageSource);
        Assert.Contains("SearchDevelopmentNavigationRequest", mainPageSource);
        Assert.Contains("SearchDevelopmentNavigationRequest", searchPageSource);
        Assert.Contains("RenderDevelopmentSearchError()", searchPageSource);
        Assert.Contains("\"Unable to search\"", searchPageSource);
        Assert.Contains("\"Check the server connection, then try again.\"", searchPageSource);
        Assert.Contains("showRetry: true", searchPageSource);
        Assert.Contains("Navigation\\SearchDevelopmentNavigationRequest.cs", projectSource);
    }

    [Fact]
    public void Search_Box_Down_Key_Routes_To_Selected_Scope()
    {
        var searchPageSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Views",
            "SearchPage.xaml.cs"));

        Assert.Contains("if (IsDownKey(e.Key))", searchPageSource);
        Assert.Contains("FocusSelectedScope(FocusState.Keyboard)", searchPageSource);
    }

    [Fact]
    public void Search_Empty_State_Left_Right_Keys_Move_Between_Recovery_Actions()
    {
        var searchPageSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Views",
            "SearchPage.xaml.cs"));

        Assert.Contains("TryMoveBetweenEmptyStateActions(e.Key)", searchPageSource);
        Assert.Contains("EmptyRetryButton.Focus(FocusState.Keyboard)", searchPageSource);
        Assert.Contains("EmptyEditButton.Focus(FocusState.Keyboard)", searchPageSource);
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
