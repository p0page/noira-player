using NextGenEmby.Core.Media;
using Xunit;

namespace NextGenEmby.Core.Tests.Media;

public sealed class PosterFallbackInitialsTests
{
    [Theory]
    [InlineData("\"Friends\"", "F")]
    [InlineData(".MP4", "M")]
    [InlineData("#Guilty", "G")]
    [InlineData("  《风声》", "风")]
    [InlineData("0.0 MHz", "0")]
    public void Create_Skips_Leading_Punctuation_For_Useful_Media_Initials(
        string value,
        string expected)
    {
        Assert.Equal(expected, PosterFallbackInitials.Create(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"...\"")]
    public void Create_Returns_Question_Mark_When_Title_Has_No_Usable_Initial(string value)
    {
        Assert.Equal("?", PosterFallbackInitials.Create(value));
    }
}
