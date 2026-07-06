using NextGenEmby.Core.Playback;
using Xunit;

namespace NextGenEmby.Core.Tests.Playback;

public sealed class ManualDirectStreamInputPolicyTests
{
    [Fact]
    public void Accept_Starts_When_Stream_Can_Start()
    {
        Assert.True(ManualDirectStreamInputPolicy.ShouldStartFromTextBox(
            ManualDirectStreamInput.Accept,
            canStart: true));
    }

    [Fact]
    public void Accept_Does_Not_Start_When_Stream_Cannot_Start()
    {
        Assert.False(ManualDirectStreamInputPolicy.ShouldStartFromTextBox(
            ManualDirectStreamInput.Accept,
            canStart: false));
    }

    [Fact]
    public void Other_Input_Does_Not_Start()
    {
        Assert.False(ManualDirectStreamInputPolicy.ShouldStartFromTextBox(
            ManualDirectStreamInput.Other,
            canStart: true));
    }
}
