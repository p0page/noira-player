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

    [Fact]
    public void GetInitialFocusTarget_Focuses_Start_Button_When_Stream_Can_Start()
    {
        Assert.Equal(
            ManualDirectStreamInitialFocusTarget.StartButton,
            ManualDirectStreamInputPolicy.GetInitialFocusTarget(canStart: true));
    }

    [Fact]
    public void GetInitialFocusTarget_Focuses_Stream_Url_Box_When_Stream_Cannot_Start()
    {
        Assert.Equal(
            ManualDirectStreamInitialFocusTarget.StreamUrlBox,
            ManualDirectStreamInputPolicy.GetInitialFocusTarget(canStart: false));
    }

    [Fact]
    public void ShouldKeepInitialFocusPending_Keeps_Pending_When_Page_Is_Not_Loaded()
    {
        Assert.True(ManualDirectStreamInputPolicy.ShouldKeepInitialFocusPending(
            applied: false,
            pageLoaded: false,
            attempts: 5,
            maxAttempts: 5));
    }

    [Fact]
    public void ShouldKeepInitialFocusPending_Clears_Pending_When_Focus_Applies()
    {
        Assert.False(ManualDirectStreamInputPolicy.ShouldKeepInitialFocusPending(
            applied: true,
            pageLoaded: true,
            attempts: 0,
            maxAttempts: 5));
    }

    [Fact]
    public void ShouldKeepInitialFocusPending_Retries_Loaded_Page_Until_Max_Attempts()
    {
        Assert.True(ManualDirectStreamInputPolicy.ShouldKeepInitialFocusPending(
            applied: false,
            pageLoaded: true,
            attempts: 4,
            maxAttempts: 5));
    }

    [Fact]
    public void ShouldKeepInitialFocusPending_Clears_Loaded_Page_After_Max_Attempts()
    {
        Assert.False(ManualDirectStreamInputPolicy.ShouldKeepInitialFocusPending(
            applied: false,
            pageLoaded: true,
            attempts: 5,
            maxAttempts: 5));
    }
}
