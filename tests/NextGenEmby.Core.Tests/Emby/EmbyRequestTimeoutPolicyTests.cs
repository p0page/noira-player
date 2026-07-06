using System;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

public sealed class EmbyRequestTimeoutPolicyTests
{
    [Fact]
    public void Interactive_Request_Timeout_Is_Short_Enough_For_TV_Recovery()
    {
        var timeout = EmbyRequestTimeoutPolicy.InteractiveRequestTimeout;

        Assert.True(timeout >= TimeSpan.FromSeconds(5));
        Assert.True(timeout <= TimeSpan.FromSeconds(15));
    }
}
