using NoiraPlayer.Core.Emby;
using Xunit;

namespace NoiraPlayer.Core.Tests.Emby;

public sealed class EmbyWebPathPolicyTests
{
    [Theory]
    [InlineData("Users/user-1/Views?Fields=ImageTags", true)]
    [InlineData("Users/user-1/Items/Resume?IncludeItemTypes=Movie%2CEpisode&Limit=24", true)]
    [InlineData("Users/user-1/Items/Latest?ParentId=library-1&Limit=24", true)]
    [InlineData("Users/user-1/Items/item-1?Fields=Overview", true)]
    [InlineData("Shows/NextUp?UserId=user-1&Limit=24", true)]
    [InlineData("Shows/NextUp?UserId=other-user&Limit=24", false)]
    [InlineData("Shows/NextUp?Limit=24", false)]
    [InlineData("Users/other-user/Items/Resume?Limit=24", false)]
    [InlineData("Users/user-1/Items/item-1/Images", false)]
    [InlineData("https://outside.invalid/Users/user-1/Views", false)]
    [InlineData("../Users/user-1/Views", false)]
    [InlineData("Users/user-1/Items", true)]
    [InlineData("Users/user-1/Views?UserId=other", false)]
    [InlineData("Users/user-1/Items?UserId=user-1", false)]
    [InlineData("Users/user-1/Items/item-1?UserId=user-1&UserId=other", false)]
    [InlineData("Users/user-1/Views?userid=other", false)]
    [InlineData("Users/user-1/Items?User%49d=user-1", false)]
    [InlineData("Users/user-1/Views/", false)]
    [InlineData("Users/user-1/Items/", false)]
    [InlineData("Users/user-1//Items", false)]
    [InlineData("Users/user-1/Items/item-1/Images/Primary", false)]
    [InlineData("//outside.invalid", false)]
    [InlineData("Users/user-1/Views#fragment", false)]
    [InlineData(@"Users\user-1\Views", false)]
    [InlineData("Users/user-1/Items/item%5Cid", false)]
    [InlineData("Users/user-1/Items/%2e%2e", false)]
    [InlineData("Users/user-1/Items/.%2e", false)]
    [InlineData("Users/user-1/Items/item%2FImages", false)]
    [InlineData("Users/user-1/Items/%252e%252e", false)]
    [InlineData("Users/user-1/Items/%252F", false)]
    [InlineData("Users/user-1/Items/%255C", false)]
    [InlineData("Users/user-1/Items/item\u001f", false)]
    [InlineData("Users/user-1/Items/item%00", false)]
    [InlineData("Users/user-1/Items/%", false)]
    [InlineData("Users/user-1/Items/%2", false)]
    [InlineData("Users/user-1/Items/%GG", false)]
    [InlineData("Shows/NextUp?UserId=", false)]
    [InlineData("Shows/NextUp?UserId=user-1&UserId=user-1", false)]
    [InlineData("Shows/NextUp?UserId=user-1&userid=user-1", false)]
    [InlineData("Shows/NextUp?UserId=user-1&User%49d=user-1", false)]
    [InlineData("Users/user-1/Views?User%2549d=other", false)]
    [InlineData("Shows/NextUp?UserId=user-1&User%2549d=other", false)]
    [InlineData("Shows/NextUp?userid=user-1", true)]
    [InlineData("Shows/NextUp?User%49d=user-1", true)]
    [InlineData("Shows/NextUp?UserId=user%2D1", true)]
    [InlineData("Shows/NextUp?UserId=User-1", false)]
    [InlineData("Shows/NextUp?UserId=user-1&Limit=%", false)]
    [InlineData("", false)]
    [InlineData("/Users/user-1/Views", false)]
    public void IsAllowed_Enforces_The_Bounded_Web_Metadata_Route_Set(string path, bool expected)
    {
        var session = new EmbySession { UserId = "user-1" };

        Assert.Equal(expected, EmbyWebPathPolicy.IsAllowed(session, path));
    }

    [Theory]
    [InlineData("user 1", "Shows/NextUp?UserId=user+1", true)]
    [InlineData("user+1", "Shows/NextUp?UserId=user%2B1", true)]
    [InlineData("user+1", "Shows/NextUp?UserId=user+1", false)]
    [InlineData("user 1", "Shows/NextUp?UserId=user%2B1", false)]
    public void IsAllowed_Form_Decodes_The_NextUp_UserId_Before_Ordinal_Matching(
        string sessionUserId,
        string path,
        bool expected)
    {
        var session = new EmbySession { UserId = sessionUserId };

        Assert.Equal(expected, EmbyWebPathPolicy.IsAllowed(session, path));
    }
}
