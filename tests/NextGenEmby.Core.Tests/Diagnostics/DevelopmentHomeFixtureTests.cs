using System;
using System.Collections.Generic;
using System.Linq;
using NextGenEmby.Core.Diagnostics;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentHomeFixtureTests
{
    [Fact]
    public void Create_Covers_Representative_Tv_Home_Rails()
    {
        var fixture = DevelopmentHomeFixture.Create();

        Assert.NotEmpty(fixture.ContinueItems);
        Assert.NotEmpty(fixture.NextUpItems);
        Assert.True(fixture.LibraryViews.Count >= 5);
        Assert.Contains(fixture.ConfiguredRows, row => row.Title == "Hot Movies");
        Assert.Contains(fixture.ConfiguredRows, row => row.Title == "Tonight Picks");
        Assert.Contains(fixture.ConfiguredRows, row => row.Title == "Hot TV Series");
        Assert.Contains(fixture.ConfiguredRows, row => row.Title == "Douban Top Rated");
        Assert.Contains(fixture.ConfiguredRows, row => row.Title == "Netflix");
        Assert.True(fixture.LatestItems.Count >= 8);
    }

    [Fact]
    public void Create_Places_Short_Row_After_Wide_Row_For_Column_Clamp_Validation()
    {
        var fixture = DevelopmentHomeFixture.Create();
        var hotMoviesIndex = FindRowIndex(fixture.ConfiguredRows, "Hot Movies");
        var tonightPicksIndex = FindRowIndex(fixture.ConfiguredRows, "Tonight Picks");

        Assert.True(hotMoviesIndex >= 0, "Hot Movies fixture row is missing.");
        Assert.Equal(hotMoviesIndex + 1, tonightPicksIndex);
        Assert.True(fixture.ConfiguredRows[hotMoviesIndex].Items.Count >= 3);
        Assert.Equal(2, fixture.ConfiguredRows[tonightPicksIndex].Items.Count);
    }

    [Fact]
    public void Create_Does_Not_Depend_On_Packaged_Qa_Artwork()
    {
        var fixture = DevelopmentHomeFixture.Create();

        Assert.Empty(fixture.ArtworkUris);
        Assert.All(fixture.LibraryViews, view => Assert.True(string.IsNullOrWhiteSpace(view.ThumbImageTag)));
        Assert.All(fixture.LatestItems, item => Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageTag)));
        Assert.All(fixture.ContinueItems, item => Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageTag)));
        Assert.All(fixture.ContinueItems, item => Assert.True(string.IsNullOrWhiteSpace(item.BackdropImageTag)));
    }

    [Fact]
    public void Create_Includes_Deterministic_Movie_Without_Artwork_For_Fallback_Validation()
    {
        var fixture = DevelopmentHomeFixture.Create();

        Assert.True(
            fixture.LibraryPreviews.TryGetValue("qa-library-movies", out var movies),
            "Movies preview fixture is missing.");
        var item = Assert.Single(movies, item => item.Id == "qa-movie-no-artwork");

        Assert.Equal("No Poster Signal", item.Name);
        Assert.Equal("Movie", item.Type);
        Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageTag));
        Assert.True(string.IsNullOrWhiteSpace(item.PrimaryImageItemId));
        Assert.True(string.IsNullOrWhiteSpace(item.BackdropImageTag));
        Assert.True(string.IsNullOrWhiteSpace(item.BackdropImageItemId));
        Assert.False(fixture.ArtworkUris.ContainsKey(DevelopmentHomeFixture.ArtworkKey(item.Id, "Primary")));
        Assert.False(fixture.ArtworkUris.ContainsKey(DevelopmentHomeFixture.ArtworkKey(item.Id, "Backdrop")));
        Assert.False(fixture.ArtworkUris.ContainsKey(DevelopmentHomeFixture.ArtworkKey(item.Id, "Thumb")));
    }

    [Fact]
    public void Create_Provides_Dense_Movies_Preview_For_Grid_Wrap_Validation()
    {
        var fixture = DevelopmentHomeFixture.Create();

        Assert.True(
            fixture.LibraryPreviews.TryGetValue("qa-library-movies", out var movies),
            "Movies preview fixture is missing.");

        Assert.True(movies.Count >= 12, "Movies preview should cover multiple rows on TV-width grids.");
        Assert.All(movies, item => Assert.Equal("Movie", item.Type));
        Assert.Equal(movies.Count, movies.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("qa-movie-aurora", movies[0].Id);
        Assert.Equal("qa-movie-no-artwork", movies[1].Id);
    }

    [Fact]
    public void Create_Configured_Rows_Do_Not_Carry_Packaged_Qa_Artwork()
    {
        var fixture = DevelopmentHomeFixture.Create();
        var row = fixture.ConfiguredRows.Single(item => item.Title == "Hot Movies");
        var sectionProperty = row.GetType().GetProperty("Section");

        Assert.NotNull(sectionProperty);
        var section = Assert.IsType<EmbyHomeSection>(sectionProperty!.GetValue(row));
        Assert.Equal("qa-section-hot-movies", section.Id);
        Assert.True(string.IsNullOrWhiteSpace(section.ThumbImageTag));
        Assert.Equal("qa-section-hot-movies", section.ThumbImageItemId);
        Assert.False(fixture.ArtworkUris.ContainsKey(DevelopmentHomeFixture.ArtworkKey(section.Id, "Thumb")));
    }

    private static int FindRowIndex(
        IReadOnlyList<DevelopmentHomeMediaRow> rows,
        string title)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (string.Equals(rows[index].Title, title, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
