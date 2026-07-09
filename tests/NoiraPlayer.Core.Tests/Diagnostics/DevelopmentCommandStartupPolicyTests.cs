using NoiraPlayer.Core.Diagnostics;
using Xunit;

namespace NoiraPlayer.Core.Tests.Diagnostics;

public sealed class DevelopmentCommandStartupPolicyTests
{
    [Fact]
    public void Decide_Runs_Command_Without_Session_When_Command_File_Exists()
    {
        var decision = DevelopmentCommandStartupPolicy.Decide(
            hasSession: false,
            hasCommandFile: true);

        Assert.False(decision.ShouldNavigateHome);
        Assert.True(decision.ShouldRunCommand);
    }

    [Fact]
    public void Decide_Navigates_Home_And_Runs_Command_When_Session_And_Command_File_Exist()
    {
        var decision = DevelopmentCommandStartupPolicy.Decide(
            hasSession: true,
            hasCommandFile: true);

        Assert.True(decision.ShouldNavigateHome);
        Assert.True(decision.ShouldRunCommand);
    }

    [Fact]
    public void Decide_Navigates_Home_Without_Running_Command_When_Only_Session_Exists()
    {
        var decision = DevelopmentCommandStartupPolicy.Decide(
            hasSession: true,
            hasCommandFile: false);

        Assert.True(decision.ShouldNavigateHome);
        Assert.False(decision.ShouldRunCommand);
    }

    [Fact]
    public void Decide_Stays_On_Login_Without_Command_When_No_Session_Exists()
    {
        var decision = DevelopmentCommandStartupPolicy.Decide(
            hasSession: false,
            hasCommandFile: false);

        Assert.False(decision.ShouldNavigateHome);
        Assert.False(decision.ShouldRunCommand);
    }
}
