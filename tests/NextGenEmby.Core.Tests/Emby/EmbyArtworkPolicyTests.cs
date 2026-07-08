using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyArtworkPolicyTests
{
    [Fact]
    public void SelectHeroArtwork_Prefers_Backdrop_Then_Thumb_Then_Banner_Then_Primary()
    {
        AssertCandidate(
            "movie-1",
            "Backdrop",
            1600,
            EmbyArtworkPolicy.SelectHeroArtwork(new EmbyMediaItem
            {
                Id = "movie-1",
                PrimaryImageTag = "primary-tag",
                BannerImageTag = "banner-tag",
                ThumbImageTag = "thumb-tag",
                BackdropImageTag = "backdrop-tag"
            }, 1600));

        AssertCandidate(
            "thumb-owner",
            "Thumb",
            1600,
            EmbyArtworkPolicy.SelectHeroArtwork(new EmbyMediaItem
            {
                Id = "movie-1",
                PrimaryImageTag = "primary-tag",
                BannerImageTag = "banner-tag",
                ThumbImageTag = "thumb-tag",
                ThumbImageItemId = "thumb-owner"
            }, 1600));

        AssertCandidate(
            "movie-1",
            "Banner",
            1600,
            EmbyArtworkPolicy.SelectHeroArtwork(new EmbyMediaItem
            {
                Id = "movie-1",
                PrimaryImageTag = "primary-tag",
                BannerImageTag = "banner-tag"
            }, 1600));

        AssertCandidate(
            "primary-owner",
            "Primary",
            1600,
            EmbyArtworkPolicy.SelectHeroArtwork(new EmbyMediaItem
            {
                Id = "movie-1",
                PrimaryImageTag = "primary-tag",
                PrimaryImageItemId = "primary-owner"
            }, 1600));
    }

    [Fact]
    public void SelectLibraryWideArtwork_Prefers_Thumb_Then_Backdrop_Then_Banner_Then_Primary()
    {
        AssertCandidate(
            "thumb-owner",
            "Thumb",
            900,
            EmbyArtworkPolicy.SelectLibraryWideArtwork(new EmbyLibraryView
            {
                Id = "library-1",
                PrimaryImageTag = "primary-tag",
                BannerImageTag = "banner-tag",
                BackdropImageTag = "backdrop-tag",
                ThumbImageTag = "thumb-tag",
                ThumbImageItemId = "thumb-owner"
            }, 900));

        AssertCandidate(
            "backdrop-owner",
            "Backdrop",
            900,
            EmbyArtworkPolicy.SelectLibraryWideArtwork(new EmbyLibraryView
            {
                Id = "library-1",
                PrimaryImageTag = "primary-tag",
                BannerImageTag = "banner-tag",
                BackdropImageTag = "backdrop-tag",
                BackdropImageItemId = "backdrop-owner"
            }, 900));

        AssertCandidate(
            "library-1",
            "Banner",
            900,
            EmbyArtworkPolicy.SelectLibraryWideArtwork(new EmbyLibraryView
            {
                Id = "library-1",
                PrimaryImageTag = "primary-tag",
                BannerImageTag = "banner-tag"
            }, 900));

        AssertCandidate(
            "primary-owner",
            "Primary",
            900,
            EmbyArtworkPolicy.SelectLibraryWideArtwork(new EmbyLibraryView
            {
                Id = "library-1",
                PrimaryImageTag = "primary-tag",
                PrimaryImageItemId = "primary-owner"
            }, 900));
    }

    [Fact]
    public void SelectPosterArtwork_Prefers_Primary_Then_Thumb_Then_Backdrop()
    {
        AssertCandidate(
            "primary-owner",
            "Primary",
            520,
            EmbyArtworkPolicy.SelectPosterArtwork(new EmbyMediaItem
            {
                Id = "movie-1",
                PrimaryImageTag = "primary-tag",
                PrimaryImageItemId = "primary-owner",
                ThumbImageTag = "thumb-tag",
                BackdropImageTag = "backdrop-tag"
            }, 520));

        AssertCandidate(
            "thumb-owner",
            "Thumb",
            520,
            EmbyArtworkPolicy.SelectPosterArtwork(new EmbyMediaItem
            {
                Id = "movie-1",
                ThumbImageTag = "thumb-tag",
                ThumbImageItemId = "thumb-owner",
                BackdropImageTag = "backdrop-tag"
            }, 520));

        AssertCandidate(
            "movie-1",
            "Backdrop",
            520,
            EmbyArtworkPolicy.SelectPosterArtwork(new EmbyMediaItem
            {
                Id = "movie-1",
                BackdropImageTag = "backdrop-tag"
            }, 520));
    }

    [Fact]
    public void SelectLogoArtwork_Uses_Item_Logo_When_Available()
    {
        AssertCandidate(
            "logo-owner",
            "Logo",
            640,
            EmbyArtworkPolicy.SelectLogoArtwork(new EmbyMediaItem
            {
                Id = "movie-1",
                LogoImageTag = "logo-tag",
                LogoImageItemId = "logo-owner"
            }, 640));
    }

    [Fact]
    public void SelectLibraryLogoArtwork_Uses_View_Logo_When_Available()
    {
        AssertCandidate(
            "library-logo-owner",
            "Logo",
            640,
            EmbyArtworkPolicy.SelectLibraryLogoArtwork(new EmbyLibraryView
            {
                Id = "library-1",
                LogoImageTag = "logo-tag",
                LogoImageItemId = "library-logo-owner"
            }, 640));
    }

    [Fact]
    public void SelectHomeSectionWideArtwork_Uses_Section_Parent_Item_Artwork()
    {
        AssertCandidate(
            "section-thumb-owner",
            "Thumb",
            900,
            EmbyArtworkPolicy.SelectHomeSectionWideArtwork(new EmbyHomeSection
            {
                Id = "sec-hot-movies",
                Name = "Hot Movies",
                ParentItem = new EmbyMediaItem
                {
                    Id = "hot-movies",
                    PrimaryImageTag = "primary-tag",
                    BannerImageTag = "banner-tag",
                    ThumbImageTag = "thumb-tag",
                    BackdropImageTag = "backdrop-tag",
                    ThumbImageItemId = "section-thumb-owner"
                }
            }, 900));

        AssertCandidate(
            "hot-series",
            "Banner",
            900,
            EmbyArtworkPolicy.SelectHomeSectionWideArtwork(new EmbyHomeSection
            {
                Id = "sec-hot-series",
                ParentItem = new EmbyMediaItem
                {
                    Id = "hot-series",
                    PrimaryImageTag = "primary-tag",
                    BannerImageTag = "banner-tag"
                }
            }, 900));
    }

    [Fact]
    public void SelectHomeSectionWideArtwork_Prefers_Section_Artwork_Before_Parent_Item()
    {
        var section = new EmbyHomeSection
        {
            Id = "sec-hot-movies",
            Name = "Hot Movies",
            ParentItem = new EmbyMediaItem
            {
                Id = "fallback-parent",
                ThumbImageTag = "fallback-thumb",
                ThumbImageItemId = "fallback-thumb-owner"
            }
        };
        SetSectionString(section, "ThumbImageTag", "section-thumb");
        SetSectionString(section, "ThumbImageItemId", "section-thumb-owner");

        AssertCandidate(
            "section-thumb-owner",
            "Thumb",
            900,
            EmbyArtworkPolicy.SelectHomeSectionWideArtwork(section, 900));
    }

    [Fact]
    public void SelectItemWideArtwork_Prefers_Thumb_Then_Backdrop_Then_Banner_Then_Primary()
    {
        AssertCandidate(
            "thumb-owner",
            "Thumb",
            900,
            EmbyArtworkPolicy.SelectItemWideArtwork(new EmbyMediaItem
            {
                Id = "collection-1",
                PrimaryImageTag = "primary-tag",
                BannerImageTag = "banner-tag",
                BackdropImageTag = "backdrop-tag",
                ThumbImageTag = "thumb-tag",
                ThumbImageItemId = "thumb-owner"
            }, 900));

        AssertCandidate(
            "backdrop-owner",
            "Backdrop",
            900,
            EmbyArtworkPolicy.SelectItemWideArtwork(new EmbyMediaItem
            {
                Id = "collection-1",
                PrimaryImageTag = "primary-tag",
                BannerImageTag = "banner-tag",
                BackdropImageTag = "backdrop-tag",
                BackdropImageItemId = "backdrop-owner"
            }, 900));

        AssertCandidate(
            "collection-1",
            "Banner",
            900,
            EmbyArtworkPolicy.SelectItemWideArtwork(new EmbyMediaItem
            {
                Id = "collection-1",
                PrimaryImageTag = "primary-tag",
                BannerImageTag = "banner-tag"
            }, 900));

        AssertCandidate(
            "primary-owner",
            "Primary",
            900,
            EmbyArtworkPolicy.SelectItemWideArtwork(new EmbyMediaItem
            {
                Id = "collection-1",
                PrimaryImageTag = "primary-tag",
                PrimaryImageItemId = "primary-owner"
            }, 900));
    }

    [Fact]
    public void SelectArtwork_Returns_Null_When_No_Usable_Image_Exists()
    {
        Assert.Null(EmbyArtworkPolicy.SelectHeroArtwork(new EmbyMediaItem { Id = "movie-1" }, 1600));
        Assert.Null(EmbyArtworkPolicy.SelectLibraryWideArtwork(new EmbyLibraryView { Id = "library-1" }, 900));
        Assert.Null(EmbyArtworkPolicy.SelectPosterArtwork(new EmbyMediaItem { Id = "movie-1" }, 520));
        Assert.Null(EmbyArtworkPolicy.SelectLogoArtwork(new EmbyMediaItem { Id = "movie-1" }, 640));
        Assert.Null(EmbyArtworkPolicy.SelectLibraryLogoArtwork(new EmbyLibraryView { Id = "library-1" }, 640));
        Assert.Null(EmbyArtworkPolicy.SelectHomeSectionWideArtwork(new EmbyHomeSection { Id = "section-1" }, 900));
        Assert.Null(EmbyArtworkPolicy.SelectItemWideArtwork(new EmbyMediaItem { Id = "collection-1" }, 900));
    }

    private static void AssertCandidate(
        string itemId,
        string imageType,
        int maxWidth,
        EmbyImageCandidate? candidate)
    {
        Assert.NotNull(candidate);
        Assert.Equal(itemId, candidate.ItemId);
        Assert.Equal(imageType, candidate.ImageType);
        Assert.Equal(maxWidth, candidate.MaxWidth);
    }

    private static void SetSectionString(EmbyHomeSection section, string propertyName, string value)
    {
        var property = typeof(EmbyHomeSection).GetProperty(propertyName);
        Assert.NotNull(property);
        property!.SetValue(section, value);
    }
}
