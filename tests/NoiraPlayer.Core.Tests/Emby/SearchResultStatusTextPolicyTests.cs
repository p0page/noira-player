using NoiraPlayer.Core.Emby;
using Xunit;

namespace NoiraPlayer.Core.Tests.Emby;

public sealed class SearchResultStatusTextPolicyTests
{
    [Theory]
    [InlineData(1, "Live TV", "1 result / Live TV")]
    [InlineData(2, "Movies", "2 results / Movies")]
    [InlineData(12, "All", "12 results / All")]
    public void Create_Uses_Singular_Only_For_One_Result(
        int count,
        string scopeLabel,
        string expected)
    {
        Assert.Equal(expected, SearchResultStatusTextPolicy.Create(count, scopeLabel));
    }
}
