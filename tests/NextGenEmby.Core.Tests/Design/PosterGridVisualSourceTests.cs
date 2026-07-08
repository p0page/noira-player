using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class PosterGridVisualSourceTests
{
    [Fact]
    public void Shared_Poster_Grid_Items_Use_Matte_Backplate_Focus_Instead_Of_System_Rings()
    {
        var appXaml = ReadAppSource("App.xaml");
        var libraryXaml = ReadAppSource("Views", "LibraryPage.xaml");
        var searchXaml = ReadAppSource("Views", "SearchPage.xaml");

        Assert.Contains("TvPosterSelectedBackplateBrush", appXaml);
        Assert.Contains("TvPosterFocusedItemScale", appXaml);
        Assert.Contains("<Setter Property=\"UseSystemFocusVisuals\" Value=\"False\" />", appXaml);
        Assert.Contains("PosterSelectedBackplate", libraryXaml);
        Assert.Contains("PosterSelectedBackplate", searchXaml);
        Assert.Contains("PosterArtworkFrame", libraryXaml);
        Assert.Contains("PosterArtworkFrame", searchXaml);
    }

    [Fact]
    public void Library_And_Search_Poster_Templates_Place_Metadata_Below_Artwork()
    {
        var libraryXaml = ReadAppSource("Views", "LibraryPage.xaml");
        var searchXaml = ReadAppSource("Views", "SearchPage.xaml");

        Assert.Contains("PosterMetadataPanel", libraryXaml);
        Assert.Contains("PosterMetadataPanel", searchXaml);
        Assert.DoesNotContain("VerticalAlignment=\"Bottom\"\r\n                                    Background=\"{StaticResource AppCardScrimBrush}\"", libraryXaml);
        Assert.DoesNotContain("VerticalAlignment=\"Bottom\"\r\n                                    Background=\"{StaticResource AppCardScrimBrush}\"", searchXaml);
    }

    [Fact]
    public void Library_Poster_Grid_Has_Intentional_No_Artwork_Fallback()
    {
        var libraryXaml = ReadAppSource("Views", "LibraryPage.xaml");
        var librarySource = ReadAppSource("Views", "LibraryPage.xaml.cs");

        Assert.Contains("Text=\"{Binding Initials}\"", libraryXaml);
        Assert.Contains("TvPosterFallbackInitialsTextStyle", libraryXaml);
        Assert.Contains("Initials = CreateInitials(Title)", librarySource);
    }

    [Fact]
    public void Poster_Fallback_Initials_Are_Shared_And_Ignore_Title_Punctuation()
    {
        var librarySource = ReadAppSource("Views", "LibraryPage.xaml.cs");
        var searchSource = ReadAppSource("Views", "SearchPage.xaml.cs");
        var detailsSource = ReadAppSource("Views", "MediaDetailsPage.xaml.cs");
        var helperSource = ReadCoreSource("Media", "PosterFallbackInitials.cs");

        Assert.Contains("PosterFallbackInitials.Create(value)", librarySource);
        Assert.Contains("PosterFallbackInitials.Create(value)", searchSource);
        Assert.Contains("PosterFallbackInitials.Create(CreateDisplayName(item))", detailsSource);
        Assert.Contains("char.IsLetterOrDigit(character)", helperSource);
        Assert.DoesNotContain("value.Trim().Substring(0, 1)", librarySource);
        Assert.DoesNotContain("value.Trim().Substring(0, 1)", searchSource);
        Assert.DoesNotContain("name.Substring(0, 1)", detailsSource);
    }

    [Fact]
    public void Development_Routes_Include_Deterministic_Movies_Fixture()
    {
        var navigationSource = ReadCoreSource("Diagnostics", "DevelopmentNavigationCommand.cs");
        var mainPageSource = ReadAppSource("MainPage.xaml.cs");

        Assert.Contains("case \"movies-fixture\":", navigationSource);
        Assert.Contains("case \"movies-fixture\":", mainPageSource);
        Assert.Contains("CreateMoviesFixtureNavigationRequest", mainPageSource);
    }

    [Fact]
    public void Qa_Artwork_Generator_Uses_Fictional_Movie_Poster_Compositions()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "Generate-HomeQaArtworkAssets.ps1"));

        Assert.Contains("New-QaPosterArtwork", script);
        Assert.Contains("Draw-PosterTypography", script);
        Assert.Contains("New-QaWideArtwork", script);
        Assert.DoesNotContain("#3BD5FF", script);
        Assert.DoesNotContain("#E0B86A", script);
        Assert.DoesNotContain("#FF6B6B", script);
    }

    [Fact]
    public void Qa_Artwork_Generator_Uses_Cinematic_Scene_Primitives_Instead_Of_Abstract_Test_Cards()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "Generate-HomeQaArtworkAssets.ps1"));

        Assert.Contains("Draw-CinematicPosterScene", script);
        Assert.Contains("Draw-CinematicWideScene", script);
        Assert.Contains("Draw-FilmBillingBlock", script);
        Assert.Contains("Draw-AtmosphereTexture", script);
        Assert.DoesNotContain("Watch-ready wide artwork", script);
    }

    private static string ReadAppSource(params string[] segments)
    {
        var parts = new string[segments.Length + 3];
        parts[0] = FindRepositoryRoot();
        parts[1] = "src";
        parts[2] = "NextGenEmby.App";
        Array.Copy(segments, 0, parts, 3, segments.Length);
        return File.ReadAllText(Path.Combine(parts));
    }

    private static string ReadCoreSource(params string[] segments)
    {
        var parts = new string[segments.Length + 3];
        parts[0] = FindRepositoryRoot();
        parts[1] = "src";
        parts[2] = "NextGenEmby.Core";
        Array.Copy(segments, 0, parts, 3, segments.Length);
        return File.ReadAllText(Path.Combine(parts));
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
