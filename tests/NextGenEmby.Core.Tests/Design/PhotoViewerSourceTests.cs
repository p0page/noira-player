using System;
using System.IO;
using Xunit;

namespace NextGenEmby.Core.Tests.Design;

public sealed class PhotoViewerSourceTests
{
    [Fact]
    public void Photos_Fixture_Development_Route_Renders_Positive_Library_State()
    {
        var mainPageSource = ReadAppSource("MainPage.xaml.cs");

        Assert.Contains("case \"photos-fixture\"", mainPageSource);
        Assert.Contains("DevelopmentPhotosFixture.Create()", mainPageSource);
        Assert.Contains("\"Photo,Folder\"", mainPageSource);
        Assert.Contains("new LibraryNavigationQuery(mediaTypes: \"Photo\", requireItemTypeMatch: true)", mainPageSource);
        Assert.Contains("fixture.Items", mainPageSource);
        Assert.Contains("fixture.ArtworkUris", mainPageSource);
    }

    [Fact]
    public void Photo_Viewer_Uses_Development_Image_Uri_Before_Session_Load()
    {
        var source = ReadAppSource("Views", "PhotoViewerPage.xaml.cs");
        var developmentUriIndex = source.IndexOf("DevelopmentImageUri", StringComparison.Ordinal);
        var sessionLoadIndex = source.IndexOf("_sessionStore.LoadAsync()", StringComparison.Ordinal);

        Assert.True(developmentUriIndex >= 0, "PhotoViewerPage should inspect DevelopmentImageUri.");
        Assert.True(sessionLoadIndex >= 0, "PhotoViewerPage should still support session-backed photos.");
        Assert.True(developmentUriIndex < sessionLoadIndex, "DevelopmentImageUri should be used before session loading.");
        Assert.Contains("new BitmapImage(new Uri(_request.DevelopmentImageUri))", source);
    }

    [Fact]
    public void Photos_Library_Uses_Photo_Specific_Grid_Recipe()
    {
        var appXaml = ReadAppSource("App.xaml");
        var libraryXaml = ReadAppSource("Views", "LibraryPage.xaml");
        var librarySource = ReadAppSource("Views", "LibraryPage.xaml.cs");

        Assert.Contains("TvPhotoCardWidth", appXaml);
        Assert.Contains("Width=\"{Binding CardWidth}\"", libraryXaml);
        Assert.Contains("Height=\"{Binding CardHeight}\"", libraryXaml);
        Assert.Contains("Width=\"{Binding ArtworkWidth}\"", libraryXaml);
        Assert.Contains("Height=\"{Binding ArtworkHeight}\"", libraryXaml);
        Assert.Contains("Height=\"{Binding MetadataHeight}\"", libraryXaml);
        Assert.Contains("IsPhotoSurfaceRequest(_request)", librarySource);
        Assert.Contains("usePhotoRecipe", librarySource);
        Assert.Contains("CardWidth = usePhotoRecipe ? PhotoCardWidth : PosterCardWidth", librarySource);
        Assert.Contains("ArtworkHeight = usePhotoRecipe ? PhotoArtworkHeight : PosterArtworkHeight", librarySource);
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
