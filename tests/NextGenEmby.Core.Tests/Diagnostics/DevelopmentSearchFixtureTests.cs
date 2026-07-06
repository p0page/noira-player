using System;
using System.Linq;
using NextGenEmby.Core.Diagnostics;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Diagnostics;

public sealed class DevelopmentSearchFixtureTests
{
    [Fact]
    public void CreateItemsForScope_Returns_Items_For_Every_Search_Surface()
    {
        foreach (var scope in EmbySearchScopePolicy.AllScopes)
        {
            var items = DevelopmentSearchFixture.CreateItemsForScope(scope.Key);

            Assert.NotEmpty(items);
        }
    }

    [Fact]
    public void CreateItemsForScope_Respects_Specific_Item_Type_Filters()
    {
        foreach (var scope in EmbySearchScopePolicy.AllScopes.Where(scope => scope.RequireItemTypeMatch))
        {
            var allowedTypes = scope.IncludeItemTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var items = DevelopmentSearchFixture.CreateItemsForScope(scope.Key);

            Assert.All(items, item => Assert.Contains(item.Type, allowedTypes));
        }
    }

    [Fact]
    public void CreateItemsForScope_LiveTv_Covers_Far_Right_Scope()
    {
        var items = DevelopmentSearchFixture.CreateItemsForScope("livetv");

        var item = Assert.Single(items);
        Assert.Equal("TvChannel", item.Type);
        Assert.Equal("News 24", item.Name);
    }
}
