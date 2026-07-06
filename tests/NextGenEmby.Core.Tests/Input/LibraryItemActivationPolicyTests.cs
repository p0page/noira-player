using NextGenEmby.Core.Input;
using Xunit;

namespace NextGenEmby.Core.Tests.Input;

public sealed class LibraryItemActivationPolicyTests
{
    [Theory]
    [InlineData("Movie")]
    [InlineData("Series")]
    [InlineData("Episode")]
    [InlineData("BoxSet")]
    [InlineData("Playlist")]
    public void ChooseRoute_Opens_Details_For_Standard_Media(string itemType)
    {
        var route = LibraryItemActivationPolicy.ChooseRoute(itemType);

        Assert.Equal(LibraryItemActivationRoute.Details, route);
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
