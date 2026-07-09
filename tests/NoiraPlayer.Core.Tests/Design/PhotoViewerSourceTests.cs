using System;
using System.IO;
using Xunit;

namespace NoiraPlayer.Core.Tests.Design;

public sealed class PhotoViewerSourceTests
{
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
        parts[2] = "NoiraPlayer.App";
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
