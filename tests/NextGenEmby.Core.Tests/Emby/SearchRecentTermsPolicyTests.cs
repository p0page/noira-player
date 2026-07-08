using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class SearchRecentTermsPolicyTests
{
    [Fact]
    public void Add_Moves_Normalized_Term_To_Front()
    {
        var terms = SearchRecentTermsPolicy.Add(
            new[] { "Terrifier", "Friends" },
            "  Aurora   Protocol  ");

        Assert.Equal(new[] { "Aurora Protocol", "Terrifier", "Friends" }, terms);
    }

    [Fact]
    public void Add_Removes_Existing_Term_Case_Insensitive()
    {
        var terms = SearchRecentTermsPolicy.Add(
            new[] { "Aurora Protocol", "Friends", "News 24" },
            "aurora protocol");

        Assert.Equal(new[] { "aurora protocol", "Friends", "News 24" }, terms);
    }

    [Fact]
    public void Add_Ignores_Blank_Term()
    {
        var terms = SearchRecentTermsPolicy.Add(
            new[] { "Aurora Protocol", "Friends" },
            "   ");

        Assert.Equal(new[] { "Aurora Protocol", "Friends" }, terms);
    }

    [Fact]
    public void Add_Limits_Terms_To_Max_Count()
    {
        var terms = SearchRecentTermsPolicy.Add(
            new[] { "One", "Two", "Three" },
            "Four",
            maxCount: 3);

        Assert.Equal(new[] { "Four", "One", "Two" }, terms);
    }

    [Fact]
    public void Stored_Value_Round_Trip_Filters_Empty_And_Duplicate_Terms()
    {
        var terms = SearchRecentTermsPolicy.FromStoredValue(
            "Aurora Protocol\n\nFriends\naurora protocol\nNews 24");
        var stored = SearchRecentTermsPolicy.ToStoredValue(terms);

        Assert.Equal(new[] { "Aurora Protocol", "Friends", "News 24" }, terms);
        Assert.Equal("Aurora Protocol\nFriends\nNews 24", stored);
    }
}
