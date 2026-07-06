using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyLibraryTests
{
    [Fact]
    public async Task GetLatestItemsAsync_Parses_Movies_And_TV_Items()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            [
              {
                "Id": "movie-1",
                "Name": "Blade Runner",
                "Type": "Movie",
                "Overview": "Replicants and rain.",
                "ProductionYear": 1982,
                "RunTimeTicks": 70200000000,
                "ImageTags": { "Primary": "primary-tag" },
                "BackdropImageTags": [ "backdrop-tag" ]
              }
            ]
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetLatestItemsAsync(Session());

        var item = Assert.Single(items);
        Assert.Equal("movie-1", item.Id);
        Assert.Equal("Movie", item.Type);
        Assert.Equal("primary-tag", item.PrimaryImageTag);
        Assert.Equal("backdrop-tag", item.BackdropImageTag);

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("/Users/user-1/Items/Latest", request.RequestUri!.AbsolutePath);
        Assert.Equal("Emby", request.AuthorizationScheme);
        Assert.Equal(
            "UserId=\"user-1\", Client=\"Next Gen Xbox Emby\", Device=\"Next Gen Xbox Emby\", DeviceId=\"test-device\", Version=\"0.1.0\"",
            request.AuthorizationParameter);
        Assert.Equal("token-123", request.EmbyToken);
    }

    [Fact]
    public void GetImageUrl_Builds_Primary_Image_Url()
    {
        using var http = new HttpClient(new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var client = CreateClient(http);

        var url = client.GetImageUrl(Session(), "movie-1", "Primary", 600);

        Assert.Equal("http://emby.local:8096/Items/movie-1/Images/Primary?maxWidth=600&quality=90&api_key=token-123", url);
    }

    [Fact]
    public async Task GetLatestItemsAsync_Maps_Null_Image_Collections_To_Empty_Tags()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            [
              {
                "Id": "series-1",
                "Name": "Null Images",
                "Type": "Series",
                "ImageTags": null,
                "BackdropImageTags": null
              }
            ]
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetLatestItemsAsync(Session());

        var item = Assert.Single(items);
        Assert.Equal("", item.PrimaryImageTag);
        Assert.Equal("", item.BackdropImageTag);
    }

    [Fact]
    public async Task GetLatestItemsAsync_Escapes_User_Id_And_Builds_Expected_Query()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.GetLatestItemsAsync(Session(userId: "user 1/slash"));

        var request = handler.LastRequest!;
        Assert.Equal("/Users/user%201%2Fslash/Items/Latest", request.RequestUri!.AbsolutePath);
        Assert.Contains("IncludeItemTypes=Movie%2CSeries%2CEpisode", request.RequestUri.Query);
        Assert.Contains("Fields=Overview%2CProductionYear%2CRunTimeTicks%2CPrimaryImageAspectRatio%2CChildCount%2CUserData", request.RequestUri.Query);
        Assert.Contains("Limit=50", request.RequestUri.Query);
        Assert.Contains("EnableImages=true", request.RequestUri.Query);
        Assert.Contains("EnableImageTypes=Primary%2CBackdrop%2CThumb%2CBanner%2CLogo", request.RequestUri.Query);
        Assert.Contains("ImageTypeLimit=1", request.RequestUri.Query);
    }

    [Fact]
    public async Task GetResumeItemsAsync_Uses_Resume_Endpoint_And_Parses_UserData()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "resume-1",
                  "Name": "Half Watched",
                  "Type": "Movie",
                  "RunTimeTicks": 60000000000,
                  "UserData": {
                    "Played": false,
                    "PlaybackPositionTicks": 15000000000,
                    "PlayedPercentage": 25
                  }
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetResumeItemsAsync(Session(userId: "user 1/slash"), 24);

        var item = Assert.Single(items);
        Assert.Equal("resume-1", item.Id);
        Assert.Equal(15000000000, item.UserData.PlaybackPositionTicks);
        Assert.Equal(25, item.UserData.PlayedPercentage);
        Assert.Equal("/Users/user%201%2Fslash/Items/Resume", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("IncludeItemTypes=Movie%2CEpisode", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=24", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Fields=Overview%2CProductionYear%2CRunTimeTicks%2CPrimaryImageAspectRatio%2CChildCount%2CUserData", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task SetFavoriteAsync_Marks_Item_As_Favorite_And_Parses_UserData()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "ItemId": "movie-1",
              "IsFavorite": true,
              "Played": false,
              "PlaybackPositionTicks": 1200000000,
              "PlayedPercentage": 12
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var userData = await client.SetFavoriteAsync(Session(userId: "user 1/slash"), "movie 1/slash", true);

        Assert.True(userData.IsFavorite);
        Assert.False(userData.Played);
        Assert.Equal(1200000000, userData.PlaybackPositionTicks);
        Assert.Equal(12, userData.PlayedPercentage);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/Users/user%201%2Fslash/FavoriteItems/movie%201%2Fslash", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Null(handler.LastRequest.Body);
    }

    [Fact]
    public async Task SetFavoriteAsync_Unmarks_Item_With_Compat_Delete_Post()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "ItemId": "movie-1",
              "IsFavorite": false,
              "Played": true
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var userData = await client.SetFavoriteAsync(Session(), "movie-1", false);

        Assert.False(userData.IsFavorite);
        Assert.True(userData.Played);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/Users/user-1/FavoriteItems/movie-1/Delete", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SetPlayedAsync_Marks_Item_Played_And_Parses_UserData()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "ItemId": "episode-1",
              "IsFavorite": true,
              "Played": true,
              "PlaybackPositionTicks": 0,
              "PlayedPercentage": 100
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var userData = await client.SetPlayedAsync(Session(userId: "user 1/slash"), "episode 1/slash", true);

        Assert.True(userData.Played);
        Assert.True(userData.IsFavorite);
        Assert.Equal(0, userData.PlaybackPositionTicks);
        Assert.Equal(100, userData.PlayedPercentage);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/Users/user%201%2Fslash/PlayedItems/episode%201%2Fslash", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SetPlayedAsync_Marks_Item_Unplayed_With_Compat_Delete_Post()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "ItemId": "episode-1",
              "IsFavorite": false,
              "Played": false
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var userData = await client.SetPlayedAsync(Session(), "episode-1", false);

        Assert.False(userData.Played);
        Assert.False(userData.IsFavorite);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/Users/user-1/PlayedItems/episode-1/Delete", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetUserViewsAsync_Parses_Movie_And_Tv_Libraries()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "movies",
                  "Name": "Movies",
                  "CollectionType": "movies",
                  "ImageTags": { "Primary": "movies-primary" },
                  "BackdropImageTags": [ "movies-backdrop" ]
                },
                {
                  "Id": "tv",
                  "Name": "TV Shows",
                  "CollectionType": "tvshows",
                  "ImageTags": { "Primary": "tv-primary" },
                  "BackdropImageTags": []
                }
              ],
              "TotalRecordCount": 2
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var views = await client.GetUserViewsAsync(Session());

        Assert.Collection(
            views,
            view =>
            {
                Assert.Equal("movies", view.Id);
                Assert.Equal("Movies", view.Name);
                Assert.Equal("movies", view.CollectionType);
                Assert.Equal("movies-primary", view.PrimaryImageTag);
                Assert.Equal("movies-backdrop", view.BackdropImageTag);
            },
            view =>
            {
                Assert.Equal("tv", view.Id);
                Assert.Equal("TV Shows", view.Name);
                Assert.Equal("tvshows", view.CollectionType);
                Assert.Equal("tv-primary", view.PrimaryImageTag);
                Assert.Equal("", view.BackdropImageTag);
            });
        Assert.Equal("/Users/user-1/Views", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetUserViewsAsync_Parses_Landscape_And_Inherited_Library_Images()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "douban",
                  "Name": "Douban Picks",
                  "CollectionType": "movies",
                  "ImageTags": {
                    "Primary": "primary-tag",
                    "Thumb": "thumb-tag",
                    "Banner": "banner-tag",
                    "Logo": "logo-tag"
                  },
                  "BackdropImageTags": [ "backdrop-tag" ],
                  "PrimaryImageItemId": "primary-owner",
                  "ParentBackdropItemId": "backdrop-owner",
                  "ParentThumbItemId": "thumb-owner",
                  "ParentBannerItemId": "banner-owner",
                  "ParentLogoItemId": "logo-owner",
                  "ParentThumbImageTag": "parent-thumb-tag"
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var view = Assert.Single(await client.GetUserViewsAsync(Session()));

        Assert.Equal("thumb-tag", view.ThumbImageTag);
        Assert.Equal("primary-tag", view.PrimaryImageTag);
        Assert.Equal("backdrop-tag", view.BackdropImageTag);
        Assert.Equal("banner-tag", view.BannerImageTag);
        Assert.Equal("logo-tag", view.LogoImageTag);
        Assert.Equal("primary-owner", view.PrimaryImageItemId);
        Assert.Equal("backdrop-owner", view.BackdropImageItemId);
        Assert.Equal("thumb-owner", view.ThumbImageItemId);
        Assert.Equal("banner-owner", view.BannerImageItemId);
        Assert.Equal("logo-owner", view.LogoImageItemId);
    }

    [Fact]
    public async Task GetHomeSectionsAsync_Parses_Server_Configured_Home_Sections()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            [
              {
                "Id": "sec-hot-movies",
                "Name": "热门电影",
                "Subtitle": "Hot Movies",
                "SectionType": "Library",
                "CollectionType": "movies",
                "ViewType": "Poster",
                "ScrollDirection": "Horizontal",
                "ParentItem": {
                  "Id": "movies",
                  "Name": "Hot Movies",
                  "Type": "CollectionFolder",
                  "CollectionType": "movies"
                }
              },
              {
                "Id": null,
                "Name": null,
                "Subtitle": null,
                "SectionType": null,
                "CollectionType": null,
                "ViewType": null,
                "ScrollDirection": null,
                "ParentItem": null
              }
            ]
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var sections = await client.GetHomeSectionsAsync(Session(userId: "user 1/slash"));

        Assert.Collection(
            sections,
            section =>
            {
                Assert.Equal("sec-hot-movies", section.Id);
                Assert.Equal("热门电影", section.Name);
                Assert.Equal("Hot Movies", section.Subtitle);
                Assert.Equal("Library", section.SectionType);
                Assert.Equal("movies", section.CollectionType);
                Assert.Equal("Poster", section.ViewType);
                Assert.Equal("Horizontal", section.ScrollDirection);
                Assert.Equal("movies", section.ParentItem.Id);
                Assert.Equal("Hot Movies", section.ParentItem.Name);
            },
            section =>
            {
                Assert.Equal("", section.Id);
                Assert.Equal("", section.Name);
                Assert.Equal("", section.Subtitle);
                Assert.Equal("", section.SectionType);
                Assert.Equal("", section.CollectionType);
                Assert.Equal("", section.ViewType);
                Assert.Equal("", section.ScrollDirection);
                Assert.Equal("", section.ParentItem.Id);
            });
        Assert.Equal("/Users/user%201%2Fslash/HomeSections", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetHomeSectionsAsync_Requests_Image_Metadata_For_Section_Artwork()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.GetHomeSectionsAsync(Session(userId: "user 1/slash"));

        Assert.Equal("/Users/user%201%2Fslash/HomeSections", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("EnableImages=true", handler.LastRequest.RequestUri.Query);
        Assert.Contains("EnableImageTypes=Primary%2CBackdrop%2CThumb%2CBanner%2CLogo", handler.LastRequest.RequestUri.Query);
        Assert.Contains("ImageTypeLimit=1", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetHomeSectionsAsync_Maps_Section_Owned_Wide_Artwork()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            [
              {
                "Id": "sec-hot-movies",
                "Name": "Hot Movies",
                "CollectionType": "movies",
                "ImageTags": {
                  "Primary": "section-primary",
                  "Thumb": "section-thumb",
                  "Banner": "section-banner",
                  "Logo": "section-logo"
                },
                "BackdropImageTags": [ "section-backdrop" ],
                "PrimaryImageItemId": "section-primary-owner",
                "ParentThumbItemId": "section-thumb-owner",
                "ParentBackdropItemId": "section-backdrop-owner",
                "ParentBannerItemId": "section-banner-owner",
                "ParentLogoItemId": "section-logo-owner",
                "ParentItem": {
                  "Id": "fallback-parent",
                  "Name": "Fallback Parent",
                  "Type": "CollectionFolder",
                  "ImageTags": { "Thumb": "fallback-thumb" },
                  "ParentThumbItemId": "fallback-thumb-owner"
                }
              }
            ]
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var section = Assert.Single(await client.GetHomeSectionsAsync(Session()));

        AssertSectionString(section, "ThumbImageTag", "section-thumb");
        AssertSectionString(section, "PrimaryImageTag", "section-primary");
        AssertSectionString(section, "BackdropImageTag", "section-backdrop");
        AssertSectionString(section, "BannerImageTag", "section-banner");
        AssertSectionString(section, "LogoImageTag", "section-logo");
        AssertSectionString(section, "ThumbImageItemId", "section-thumb-owner");
        AssertSectionString(section, "PrimaryImageItemId", "section-primary-owner");
        AssertSectionString(section, "BackdropImageItemId", "section-backdrop-owner");
        AssertSectionString(section, "BannerImageItemId", "section-banner-owner");
        AssertSectionString(section, "LogoImageItemId", "section-logo-owner");
        Assert.Equal("fallback-thumb", section.ParentItem.ThumbImageTag);
    }

    [Fact]
    public async Task GetHomeSectionItemsAsync_Uses_Section_Items_Endpoint()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "movie-1",
                  "Name": "Section Movie",
                  "Type": "Movie",
                  "ImageTags": { "Primary": "primary-tag" },
                  "BackdropImageTags": [ "backdrop-tag" ]
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetHomeSectionItemsAsync(Session(), "sec hot/movies", 18);

        var item = Assert.Single(items);
        Assert.Equal("movie-1", item.Id);
        Assert.Equal("primary-tag", item.PrimaryImageTag);
        Assert.Equal("backdrop-tag", item.BackdropImageTag);
        Assert.Equal("/Users/user-1/Sections/sec%20hot%2Fmovies/Items", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("Limit=18", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Fields=Overview%2CProductionYear%2CRunTimeTicks%2CPrimaryImageAspectRatio%2CChildCount%2CUserData", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetNextUpItemsAsync_Uses_Shows_NextUp_Endpoint()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "episode-1",
                  "Name": "Next Episode",
                  "Type": "Episode",
                  "SeriesId": "series-1",
                  "IndexNumber": 2,
                  "ParentIndexNumber": 1
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetNextUpItemsAsync(Session(userId: "user 1/slash"), 12);

        var item = Assert.Single(items);
        Assert.Equal("episode-1", item.Id);
        Assert.Equal(1, item.ParentIndexNumber);
        Assert.Equal(2, item.IndexNumber);
        Assert.Equal("/Shows/NextUp", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("UserId=user%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=12", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetLatestItemsAsync_With_Parent_Uses_Latest_Endpoint_For_A_Library()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            [
              {
                "Id": "movie-1",
                "Name": "Latest Library Movie",
                "Type": "Movie"
              }
            ]
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetLatestItemsAsync(Session(), "library 1/slash", "Movie", 16);

        Assert.Equal("movie-1", Assert.Single(items).Id);
        Assert.Equal("/Users/user-1/Items/Latest", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ParentId=library%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Contains("IncludeItemTypes=Movie", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=16", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetItemsAsync_Builds_Library_Query_And_Parses_UserData()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "movie-1",
                  "Name": "Movie One",
                  "Type": "Movie",
                  "ProductionYear": 2024,
                  "RunTimeTicks": 72000000000,
                  "ImageTags": {
                    "Primary": "primary-tag",
                    "Thumb": "thumb-tag",
                    "Banner": "banner-tag",
                    "Logo": "logo-tag"
                  },
                  "BackdropImageTags": [ "backdrop-tag" ],
                  "PrimaryImageItemId": "primary-owner",
                  "ParentBackdropItemId": "backdrop-owner",
                  "ParentThumbItemId": "thumb-owner",
                  "ParentBannerItemId": "banner-owner",
                  "ParentLogoItemId": "logo-owner",
                  "UserData": {
                    "Played": false,
                    "PlaybackPositionTicks": 1230000000,
                    "PlayedPercentage": 17.5
                  }
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetItemsAsync(Session(), new EmbyItemsQuery
        {
            ParentId = "movies",
            IncludeItemTypes = "Movie",
            SortBy = "SortName",
            SortOrder = "Ascending",
            StartIndex = 20,
            Limit = 40,
            Recursive = true,
            Filters = "IsNotFolder"
        });

        var item = Assert.Single(items);
        Assert.Equal("movie-1", item.Id);
        Assert.False(item.UserData.Played);
        Assert.Equal(1230000000, item.UserData.PlaybackPositionTicks);
        Assert.Equal(17.5, item.UserData.PlayedPercentage);
        Assert.Equal("thumb-tag", item.ThumbImageTag);
        Assert.Equal("primary-tag", item.PrimaryImageTag);
        Assert.Equal("backdrop-tag", item.BackdropImageTag);
        Assert.Equal("banner-tag", item.BannerImageTag);
        Assert.Equal("logo-tag", item.LogoImageTag);
        Assert.Equal("primary-owner", item.PrimaryImageItemId);
        Assert.Equal("backdrop-owner", item.BackdropImageItemId);
        Assert.Equal("thumb-owner", item.ThumbImageItemId);
        Assert.Equal("banner-owner", item.BannerImageItemId);
        Assert.Equal("logo-owner", item.LogoImageItemId);
        Assert.Equal("/Users/user-1/Items", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ParentId=movies", handler.LastRequest.RequestUri.Query);
        Assert.Contains("IncludeItemTypes=Movie", handler.LastRequest.RequestUri.Query);
        Assert.Contains("StartIndex=20", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=40", handler.LastRequest.RequestUri.Query);
        Assert.Contains("EnableImages=true", handler.LastRequest.RequestUri.Query);
        Assert.Contains("EnableImageTypes=Primary%2CBackdrop%2CThumb%2CBanner%2CLogo", handler.LastRequest.RequestUri.Query);
        Assert.Contains("ImageTypeLimit=1", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetItemsAsync_Builds_Extended_Library_Query_For_Tv_Media_Collections_And_Filters()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [],
              "TotalRecordCount": 0
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.GetItemsAsync(Session(), new EmbyItemsQuery
        {
            ParentId = "library 1/slash",
            IncludeItemTypes = "Movie,Series,BoxSet,Playlist,MusicAlbum,Audio,Photo,Folder",
            CollectionTypes = "movies,tvshows,boxsets,playlists,music,photos",
            MediaTypes = "Video,Audio,Photo",
            GenreIds = "genre 1/slash",
            PersonIds = "person 1/slash",
            ArtistIds = "artist 1/slash",
            AlbumArtistIds = "album artist/slash",
            Ids = "item 1,item/2",
            IsFavorite = true,
            IsPlayed = false,
            IsFolder = false,
            Limit = 25
        });

        Assert.Equal("/Users/user-1/Items", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ParentId=library%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Contains("IncludeItemTypes=Movie%2CSeries%2CBoxSet%2CPlaylist%2CMusicAlbum%2CAudio%2CPhoto%2CFolder", handler.LastRequest.RequestUri.Query);
        Assert.Contains("CollectionType=movies%2Ctvshows%2Cboxsets%2Cplaylists%2Cmusic%2Cphotos", handler.LastRequest.RequestUri.Query);
        Assert.Contains("MediaTypes=Video%2CAudio%2CPhoto", handler.LastRequest.RequestUri.Query);
        Assert.Contains("GenreIds=genre%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Contains("PersonIds=person%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Contains("ArtistIds=artist%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Contains("AlbumArtistIds=album%20artist%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Ids=item%201%2Citem%2F2", handler.LastRequest.RequestUri.Query);
        Assert.Contains("IsFavorite=true", handler.LastRequest.RequestUri.Query);
        Assert.Contains("IsPlayed=false", handler.LastRequest.RequestUri.Query);
        Assert.Contains("IsFolder=false", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=25", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetItemAsync_Requests_And_Maps_People_For_Details_Rail()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Id": "movie-1",
              "Name": "Movie One",
              "Type": "Movie",
              "People": [
                {
                  "Id": "person-1",
                  "Name": "Actor One",
                  "Role": "Detective",
                  "Type": "Actor",
                  "PrimaryImageTag": "person-primary"
                },
                {
                  "Id": null,
                  "Name": null,
                  "Role": null,
                  "Type": null,
                  "PrimaryImageTag": null
                }
              ]
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var item = await client.GetItemAsync(Session(), "movie-1");

        Assert.Contains("People", handler.LastRequest!.RequestUri!.Query);
        Assert.Collection(
            item.People,
            person =>
            {
                Assert.Equal("person-1", person.Id);
                Assert.Equal("Actor One", person.Name);
                Assert.Equal("Detective", person.Role);
                Assert.Equal("Actor", person.Type);
                Assert.Equal("person-primary", person.PrimaryImageTag);
            },
            person =>
            {
                Assert.Equal("", person.Id);
                Assert.Equal("", person.Name);
                Assert.Equal("", person.Role);
                Assert.Equal("", person.Type);
                Assert.Equal("", person.PrimaryImageTag);
            });
    }

    [Fact]
    public async Task GetSimilarItemsAsync_Uses_Similar_Endpoint_And_Parses_Items()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "movie-2",
                  "Name": "Movie Two",
                  "Type": "Movie",
                  "ImageTags": { "Primary": "primary-tag" }
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetSimilarItemsAsync(Session(userId: "user 1/slash"), "movie 1/slash", 18);

        var item = Assert.Single(items);
        Assert.Equal("movie-2", item.Id);
        Assert.Equal("Movie Two", item.Name);
        Assert.Equal("primary-tag", item.PrimaryImageTag);
        Assert.Equal("/Items/movie%201%2Fslash/Similar", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("UserId=user%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=18", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Fields=Overview%2CProductionYear%2CRunTimeTicks%2CPrimaryImageAspectRatio%2CChildCount%2CUserData", handler.LastRequest.RequestUri.Query);
        Assert.Contains("EnableImages=true", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetItemAncestorsAsync_Uses_Ancestors_Endpoint_And_Parses_Organize_Parents()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            [
              {
                "Id": "boxset-1",
                "Name": "A Collection",
                "Type": "BoxSet",
                "ImageTags": { "Primary": "collection-primary" }
              },
              {
                "Id": "playlist-1",
                "Name": "A Playlist",
                "Type": "Playlist"
              }
            ]
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetItemAncestorsAsync(Session(userId: "user 1/slash"), "movie 1/slash");

        Assert.Collection(
            items,
            item =>
            {
                Assert.Equal("boxset-1", item.Id);
                Assert.Equal("A Collection", item.Name);
                Assert.Equal("BoxSet", item.Type);
                Assert.Equal("collection-primary", item.PrimaryImageTag);
            },
            item =>
            {
                Assert.Equal("playlist-1", item.Id);
                Assert.Equal("A Playlist", item.Name);
                Assert.Equal("Playlist", item.Type);
            });
        Assert.Equal("/Items/movie%201%2Fslash/Ancestors", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("UserId=user%201%2Fslash", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task AddItemToCollectionAsync_Posts_Collection_Item_Ids()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, ""));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.AddItemToCollectionAsync(Session(), "collection 1/slash", "movie 1/slash");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/Collections/collection%201%2Fslash/Items", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("Ids=movie%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Null(handler.LastRequest.Body);
    }

    [Fact]
    public async Task GetPlaylistItemsAsync_Uses_Playlist_Items_Endpoint_And_Maps_Items()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "episode-1",
                  "Name": "Queued Episode",
                  "Type": "Episode",
                  "ImageTags": { "Primary": "primary-tag" },
                  "BackdropImageTags": [ "backdrop-tag" ]
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetPlaylistItemsAsync(Session(userId: "user 1/slash"), "playlist 1/slash", 24);

        var item = Assert.Single(items);
        Assert.Equal("episode-1", item.Id);
        Assert.Equal("Queued Episode", item.Name);
        Assert.Equal("Episode", item.Type);
        Assert.Equal("primary-tag", item.PrimaryImageTag);
        Assert.Equal("backdrop-tag", item.BackdropImageTag);
        Assert.Equal("/Playlists/playlist%201%2Fslash/Items", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("UserId=user%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=24", handler.LastRequest.RequestUri.Query);
        Assert.Contains(
            "Fields=Overview%2CProductionYear%2CRunTimeTicks%2CPrimaryImageAspectRatio%2CChildCount%2CUserData",
            handler.LastRequest.RequestUri.Query);
        Assert.Contains("EnableImages=true", handler.LastRequest.RequestUri.Query);
        Assert.Contains("EnableImageTypes=Primary%2CBackdrop%2CThumb%2CBanner%2CLogo", handler.LastRequest.RequestUri.Query);
        Assert.Contains("ImageTypeLimit=1", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task AddItemToPlaylistAsync_Posts_User_And_Parses_Result()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Id": "playlist-1",
              "ItemAddedCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var result = await client.AddItemToPlaylistAsync(
            Session(userId: "user 1/slash"),
            "playlist 1/slash",
            "movie 1/slash");

        Assert.Equal("playlist-1", result.Id);
        Assert.Equal(1, result.ItemAddedCount);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/Playlists/playlist%201%2Fslash/Items", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("UserId=user%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Ids=movie%201%2Fslash", handler.LastRequest.RequestUri.Query);
        Assert.Null(handler.LastRequest.Body);
    }

    [Fact]
    public async Task SearchItemsAsync_Uses_Caller_Provided_Item_Types()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [],
              "TotalRecordCount": 0
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.SearchItemsAsync(Session(), "pilot", "Episode");

        Assert.Equal("/Users/user-1/Items", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("IncludeItemTypes=Episode", handler.LastRequest.RequestUri.Query);
        Assert.Contains("SearchTerm=pilot", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetItemsAsync_Escapes_Clamps_And_Normalizes_Null_Strings()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": null,
                  "Name": null,
                  "Type": null,
                  "Overview": null,
                  "ParentId": null,
                  "SeriesId": null,
                  "ImageTags": null,
                  "BackdropImageTags": null
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var items = await client.GetItemsAsync(Session(), new EmbyItemsQuery
        {
            ParentId = "parent 1/slash",
            SearchTerm = "pilot + finale",
            IncludeItemTypes = "Movie,Episode",
            StartIndex = -5,
            Limit = 0
        });

        var item = Assert.Single(items);
        Assert.Equal("", item.Id);
        Assert.Equal("", item.Name);
        Assert.Equal("", item.Type);
        Assert.Equal("", item.Overview);
        Assert.Equal("", item.ParentId);
        Assert.Equal("", item.SeriesId);
        Assert.Equal("", item.PrimaryImageTag);
        Assert.Equal("", item.BackdropImageTag);
        Assert.Contains("ParentId=parent%201%2Fslash", handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("SearchTerm=pilot%20%2B%20finale", handler.LastRequest.RequestUri.Query);
        Assert.Contains("IncludeItemTypes=Movie%2CEpisode", handler.LastRequest.RequestUri.Query);
        Assert.Contains("StartIndex=0", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=1", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetUserViewsAsync_Normalizes_Null_Strings()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                { "Id": null, "Name": null, "CollectionType": null }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var view = Assert.Single(await client.GetUserViewsAsync(Session()));

        Assert.Equal("", view.Id);
        Assert.Equal("", view.Name);
        Assert.Equal("", view.CollectionType);
    }

    [Fact]
    public async Task GetItemAsync_And_GetChildrenAsync_Parse_Detail_And_Episodes()
    {
        var calls = 0;
        TestHttpRequestSnapshot? detailRequest = null;
        var handler = new TestHttpMessageHandler(request =>
        {
            calls++;
            if (request.RequestUri!.AbsolutePath == "/Users/user-1/Items/series-1")
            {
                detailRequest = TestHttpRequestSnapshot.From(request, null);
                return TestHttpMessageHandler.Json(
                    HttpStatusCode.OK,
                    """
                    {
                      "Id": "series-1",
                      "Name": "Series One",
                      "Type": "Series",
                      "Overview": "A show.",
                      "ChildCount": 1
                    }
                    """);
            }

            return TestHttpMessageHandler.Json(
                HttpStatusCode.OK,
                """
                {
                  "Items": [
                    {
                      "Id": "episode-1",
                      "Name": "Pilot",
                      "Type": "Episode",
                      "IndexNumber": 1,
                      "ParentIndexNumber": 1
                    }
                  ],
                  "TotalRecordCount": 1
                }
                """);
        });
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var detail = await client.GetItemAsync(Session(), "series-1");
        var episodes = await client.GetChildrenAsync(Session(), "season-1", "Episode");

        Assert.Equal("Series One", detail.Name);
        Assert.Equal(1, detail.ChildCount);
        var episode = Assert.Single(episodes);
        Assert.Equal(1, episode.ParentIndexNumber);
        Assert.Equal(1, episode.IndexNumber);
        Assert.Equal(2, calls);
        Assert.Contains(
            "Fields=Overview%2CProductionYear%2CRunTimeTicks%2CChildCount%2CMediaSources%2CUserData",
            detailRequest!.RequestUri!.Query);
        Assert.Contains("ParentId=season-1", handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("IncludeItemTypes=Episode", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Recursive=false", handler.LastRequest.RequestUri.Query);
        Assert.Contains("SortBy=SortName", handler.LastRequest.RequestUri.Query);
        Assert.Contains("SortOrder=Ascending", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=100", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetSeriesSeasonsAsync_Uses_Shows_Seasons_Endpoint()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "season-1",
                  "Name": "Season 1",
                  "Type": "Season",
                  "IndexNumber": 1
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var season = Assert.Single(await client.GetSeriesSeasonsAsync(Session(), "series 1"));

        Assert.Equal("season-1", season.Id);
        Assert.Equal("Season", season.Type);
        Assert.Equal(1, season.IndexNumber);
        Assert.Equal("/Shows/series%201/Seasons", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("UserId=user-1", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Fields=", handler.LastRequest.RequestUri.Query);
        Assert.Contains("EnableImages=true", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetSeriesEpisodesAsync_Uses_Shows_Episodes_Endpoint()
    {
        var handler = new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(
            HttpStatusCode.OK,
            """
            {
              "Items": [
                {
                  "Id": "episode-1",
                  "Name": "Pilot",
                  "Type": "Episode",
                  "IndexNumber": 1,
                  "ParentIndexNumber": 1,
                  "RunTimeTicks": 18000000000
                }
              ],
              "TotalRecordCount": 1
            }
            """));
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var episode = Assert.Single(await client.GetSeriesEpisodesAsync(Session(), "series 1", "season 1", 40));

        Assert.Equal("episode-1", episode.Id);
        Assert.Equal("Episode", episode.Type);
        Assert.Equal(1, episode.ParentIndexNumber);
        Assert.Equal(1, episode.IndexNumber);
        Assert.Equal("/Shows/series%201/Episodes", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("UserId=user-1", handler.LastRequest.RequestUri.Query);
        Assert.Contains("SeasonId=season%201", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=40", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Fields=", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public void GetImageUrl_Handles_Trailing_Slash_And_Escapes_Components()
    {
        using var http = new HttpClient(new TestHttpMessageHandler(_ => TestHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var client = CreateClient(http);

        var url = client.GetImageUrl(
            Session(serverUrl: "http://emby.local:8096/", accessToken: "token+123/abc"),
            "movie 1",
            "Primary Image",
            600);

        Assert.Equal("http://emby.local:8096/Items/movie%201/Images/Primary%20Image?maxWidth=600&quality=90&api_key=token%2B123%2Fabc", url);
    }

    private static void AssertSectionString(EmbyHomeSection section, string propertyName, string expected)
    {
        var property = typeof(EmbyHomeSection).GetProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(expected, property!.GetValue(section) as string);
    }

    private static EmbyApiClient CreateClient(HttpClient http) => new EmbyApiClient(http, new EmbyClientOptions
    {
        ServerUrl = "http://emby.local:8096",
        DeviceName = "Next Gen Xbox Emby",
        DeviceId = "test-device",
        ClientName = "Next Gen Xbox Emby",
        ClientVersion = "0.1.0"
    });

    private static EmbySession Session(
        string serverUrl = "http://emby.local:8096",
        string userId = "user-1",
        string accessToken = "token-123") => new EmbySession
    {
        ServerUrl = serverUrl,
        UserId = userId,
        UserName = "Alice",
        AccessToken = accessToken
    };
}
