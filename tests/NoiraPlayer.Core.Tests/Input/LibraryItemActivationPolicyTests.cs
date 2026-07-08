using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class LibraryItemActivationPolicyTests
{
    [Theory]
    [InlineData("Movie")]
    [InlineData("Series")]
    [InlineData("Episode")]
    public void ChooseRoute_Opens_Details_For_Standard_Media(string itemType)
    {
        var route = LibraryItemActivationPolicy.ChooseRoute(itemType);

        Assert.Equal(LibraryItemActivationRoute.Details, route);
    }

    [Theory]
    [InlineData("BoxSet")]
    [InlineData("Playlist")]
    public void ChooseRoute_Opens_Organization_Items_As_Browse_Folders(string itemType)
    {
        var route = LibraryItemActivationPolicy.ChooseRoute(itemType);

        Assert.Equal(LibraryItemActivationRoute.BrowseFolder, route);
    }

    [Fact]
    public void ChooseRoute_Opens_Photo_Viewer_For_Photos()
    {
        var route = LibraryItemActivationPolicy.ChooseRoute("Photo");

        Assert.Equal(LibraryItemActivationRoute.PhotoViewer, route);
    }

    [Fact]
    public void ChooseRoute_Opens_Folder_Browse_For_Folders()
    {
        var route = LibraryItemActivationPolicy.ChooseRoute("Folder");

        Assert.Equal(LibraryItemActivationRoute.BrowseFolder, route);
    }

    [Fact]
    public void ChooseRoute_Defaults_To_Details_For_Unknown_Types()
    {
        var route = LibraryItemActivationPolicy.ChooseRoute("");

        Assert.Equal(LibraryItemActivationRoute.Details, route);
    }
}
