using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class LibraryPageSourceTests
{
    [Fact]
    public void Library_Navigation_Request_Can_Carry_Development_Fixture_Items()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Navigation",
            "LibraryNavigationRequest.cs"));

        Assert.Contains("WithDevelopmentFixture", source);
        Assert.Contains("DevelopmentItems", source);
        Assert.Contains("DevelopmentArtworkUris", source);
    }

    [Fact]
    public void Library_Page_Renders_Development_Fixture_Items_Without_Session()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "NextGenEmby.App",
            "Views",
            "LibraryPage.xaml.cs"));

        Assert.Contains("request.DevelopmentItems.Count > 0", source);
        Assert.Contains("CreateDevelopmentGridItems", source);
        Assert.Contains("DevelopmentHomeFixture.ArtworkKey", source);
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
